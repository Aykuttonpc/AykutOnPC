using AykutOnPC.Core.Configuration;
using AykutOnPC.Infrastructure.Data;
using AykutOnPC.Web.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Seed Data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var seedOptions = services.GetRequiredService<IOptions<SeedDataSettings>>();
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

    // Only Migrate - do NOT call EnsureCreated
    context.Database.Migrate();
    DbInitializer.Initialize(context, seedOptions, logger);

    // One-time migration from SHA256 to BCrypt (safe to run multiple times)
    DbInitializer.MigratePasswordHashes(context, logger);
}

app.UseHttpsRedirection();
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
