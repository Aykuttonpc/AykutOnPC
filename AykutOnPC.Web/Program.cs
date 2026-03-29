using AykutOnPC.Core.Configuration;
using AykutOnPC.Infrastructure.Data;
using AykutOnPC.Web.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

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
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/keys"))
    .SetApplicationName("AykutOnPC");


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

// Seed Data & Migrations with Retry Logic for Docker
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var seedOptions = services.GetRequiredService<IOptions<SeedDataSettings>>();
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

    bool migrationSucceeded = false;
    int retries = 5;
    while (!migrationSucceeded && retries > 0)
    {
        try
        {
            logger.LogInformation("Attempting to apply migrations... (Remaining retries: {Retries})", retries);
            context.Database.Migrate();
            migrationSucceeded = true;
            logger.LogInformation("Migrations applied successfully.");
        }
        catch (Exception ex)
        {
            retries--;
            if (retries == 0)
            {
                logger.LogCritical(ex, "Could not apply migrations after multiple attempts. Application is exiting.");
                throw;
            }
            logger.LogWarning("Migration failed. Database might not be ready. Retrying in 5 seconds... Error: {Message}", ex.Message);
            Thread.Sleep(5000);
        }
    }

    DbInitializer.Initialize(context, seedOptions, logger);
    DbInitializer.MigratePasswordHashes(context, logger);
}

// On Render/Docker, HTTPS redirection is usually handled by the proxy.
// Only use it if not behind a proxy or if specifically needed.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

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

app.MapHealthChecks("/health");

app.Run();
