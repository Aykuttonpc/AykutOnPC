using AykutOnPC.Core.Configuration;
using AykutOnPC.Infrastructure.Data;
using AykutOnPC.Web.Commands;
using AykutOnPC.Web.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// 1. Configuration (strongly-typed)
// ──────────────────────────────────────────────
builder.Services.AddAppConfiguration(builder.Configuration);

// ──────────────────────────────────────────────
// 2. Database
// ──────────────────────────────────────────────
builder.Services.AddDatabase(builder.Configuration);

// ──────────────────────────────────────────────
// 3. Caching
// ──────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ──────────────────────────────────────────────
// 4. Authentication & Authorization
// ──────────────────────────────────────────────
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// Fix for Render/Docker: Persist keys to an ephemeral directory to avoid 500 errors on Login/Forms
var securitySettings = builder.Configuration.GetSection(SecuritySettings.SectionName).Get<SecuritySettings>() ?? new SecuritySettings();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(securitySettings.DataProtectionPath))
    .SetApplicationName(securitySettings.ApplicationName);


// ──────────────────────────────────────────────
// 5. Application Services (DI)
// ──────────────────────────────────────────────
builder.Services.AddApplicationServices();

// ──────────────────────────────────────────────
// 6. MVC + API
// ──────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ──────────────────────────────────────────────
// 7. Cross-cutting concerns
// ──────────────────────────────────────────────
builder.Services.AddAppCors(builder.Configuration);
builder.Services.AddAppRateLimiting();
builder.Services.AddAppExceptionHandling();
builder.Services.AddAppHealthChecks();

var app = builder.Build();

// ──────────────────────────────────────────────
// CLI Commands (run-and-exit, no web host)
// Usage: dotnet AykutOnPC.Web.dll --seed-admin
// ──────────────────────────────────────────────
if (args.Contains(SeedAdminCommand.ArgFlag))
{
    var seedLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedAdmin");
    Environment.ExitCode = await SeedAdminCommand.RunAsync(app.Services, seedLogger);
    return;
}

if (args.Contains(BackfillEmbeddingsCommand.ArgFlag))
{
    var backfillLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("BackfillEmbeddings");
    Environment.ExitCode = await BackfillEmbeddingsCommand.RunAsync(app.Services, backfillLogger);
    return;
}

if (args.Contains(EvalRagCommand.ArgFlag))
{
    var evalLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("EvalRag");
    Environment.ExitCode = await EvalRagCommand.RunAsync(app.Services, evalLogger);
    return;
}

// ──────────────────────────────────────────────
// Middleware Pipeline
// ──────────────────────────────────────────────
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// ──────────────────────────────────────────────
// Async Database Initialization (Background)
// ──────────────────────────────────────────────
// We run this in background so Render's port scan doesn't timeout 
// while waiting for the remote database to respond.
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var seedOptions = services.GetRequiredService<IOptions<SeedDataSettings>>();
    var securityOptions = services.GetRequiredService<IOptions<SecuritySettings>>();
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

    bool migrationSucceeded = false;
    int retries = 5;
    while (!migrationSucceeded && retries > 0)
    {
        try
        {
            logger.LogInformation("Background: Attempting to apply migrations... (Remaining retries: {Retries})", retries);
            await context.Database.MigrateAsync();
            migrationSucceeded = true;
            logger.LogInformation("Background: Migrations applied successfully.");
        }
        catch (Exception ex)
        {
            retries--;
            if (retries == 0)
            {
                logger.LogCritical(ex, "Background: Could not apply migrations. Site may be unstable.");
                break;
            }
            logger.LogWarning("Background: Migration failed. Retrying in 10 seconds... Error: {Message}", ex.Message);
            await Task.Delay(10000);
        }
    }

    // After migrations succeed, idempotently insert the admin user if missing.
    // We deliberately DO NOT update an existing admin's password here — that would silently
    // re-write credentials on every container restart. To rotate the password explicitly,
    // run: docker exec aykutonpc-web dotnet AykutOnPC.Web.dll --seed-admin
    if (!migrationSucceeded) return;
    try
    {
        var seed = seedOptions.Value;
        var username = seed.AdminUser.Username;
        var password = seed.AdminUser.Password;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Background: Admin auto-seed skipped — Username/Password not configured.");
            return;
        }

        var exists = await context.Users.AnyAsync(u => u.Username == username);
        if (exists)
        {
            logger.LogInformation("Background: Admin '{Username}' already exists — skipping auto-seed.", username);
            return;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: securityOptions.Value.BCryptWorkFactor);
        context.Users.Add(new AykutOnPC.Core.Entities.User
        {
            Username = username,
            PasswordHash = hash,
            Role = "Admin"
        });
        await context.SaveChangesAsync();
        logger.LogInformation("Background: Admin '{Username}' seeded successfully.", username);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Background: Admin auto-seed failed (non-fatal).");
    }
});

// On Render/Docker, HTTPS redirection is usually handled by the proxy.
// Only use it if not behind a proxy or if specifically needed.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

// Visitor Intelligence — server-side, cookie-free, GDPR-safe page tracking
app.UseMiddleware<AykutOnPC.Web.Infrastructure.VisitorTrackingMiddleware>();

app.UseCors("DefaultPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Rate limit the Chat API endpoint
app.MapControllers().RequireRateLimiting("GeneralApiPolicy");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status          = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs  = e.Value.Duration.TotalMilliseconds,
                tags        = e.Value.Tags
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { WriteIndented = false }));
    }
});

app.Run();
