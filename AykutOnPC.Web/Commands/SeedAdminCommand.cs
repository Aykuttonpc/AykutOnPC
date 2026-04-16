using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Entities;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AykutOnPC.Web.Commands;

public static class SeedAdminCommand
{
    public const string ArgFlag = "--seed-admin";

    public static async Task<int> RunAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<AppDbContext>();
        var seed = sp.GetRequiredService<IOptions<SeedDataSettings>>().Value;
        var security = sp.GetRequiredService<IOptions<SecuritySettings>>().Value;

        var username = seed.AdminUser.Username;
        var password = seed.AdminUser.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            logger.LogError("SeedData:AdminUser:Username is empty. Cannot seed admin.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogError("SeedData:AdminUser:Password is empty. Set ADMIN_PASSWORD env var or appsettings.");
            return 1;
        }

        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Migration failed while seeding admin. Aborting.");
            return 2;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: security.BCryptWorkFactor);
        var existing = await context.Users.SingleOrDefaultAsync(u => u.Username == username);

        if (existing is not null)
        {
            existing.PasswordHash = hash;
            existing.Role = "Admin";
            await context.SaveChangesAsync();
            logger.LogInformation("Admin '{Username}' already existed; password and role refreshed.", username);
            return 0;
        }

        context.Users.Add(new User
        {
            Username = username,
            PasswordHash = hash,
            Role = "Admin"
        });
        await context.SaveChangesAsync();
        logger.LogInformation("Admin '{Username}' created successfully with Admin role.", username);
        return 0;
    }
}
