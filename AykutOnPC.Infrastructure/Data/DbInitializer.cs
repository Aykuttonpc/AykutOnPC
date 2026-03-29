using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AykutOnPC.Infrastructure.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context, IOptions<SeedDataSettings> seedOptions, ILogger logger)
    {
        var seedData = seedOptions.Value;

        // Seed Admin User
        if (!context.Users.Any())
        {
            var adminPassword = seedData.AdminUser.Password;
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning("Admin password is not configured in SeedData:AdminUser:Password. Skipping admin user seed.");
                return;
            }

            context.Users.Add(new User
            {
                Username = seedData.AdminUser.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12),
                Role = "Admin"
            });
            context.SaveChanges();
            logger.LogInformation("Admin user '{Username}' seeded successfully.", seedData.AdminUser.Username);
        }

        // Seed Specs (Skills)
        if (!context.Specs.Any())
        {
            if (seedData.Specs.Count > 0)
            {
                context.Specs.AddRange(
                    seedData.Specs.Select(s => new Spec(s.Name, s.Category, s.Proficiency))
                );
            }
            else
            {
                context.Specs.AddRange(
                    new Spec("C#", "Language", 95),
                    new Spec(".NET Core", "Framework", 90),
                    new Spec("SQL Server", "Database", 85),
                    new Spec("Docker", "Tool", 80),
                    new Spec("System Design", "Concept", 75)
                );
            }
            context.SaveChanges();
            logger.LogInformation("Specs seeded successfully.");
        }
    }

    /// <summary>
    /// Migrates existing SHA256 hashes to BCrypt. Run once during upgrade.
    /// </summary>
    public static void MigratePasswordHashes(AppDbContext context, ILogger logger)
    {
        var users = context.Users.ToList();
        foreach (var user in users)
        {
            // BCrypt hashes start with "$2a$", "$2b$", or "$2y$"
            if (!user.PasswordHash.StartsWith("$2"))
            {
                // The old SHA256 hash cannot be reversed.
                // Set a temporary password and force reset.
                var tempPassword = Guid.NewGuid().ToString("N")[..16];
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 12);
                logger.LogWarning(
                    "User '{Username}' had legacy SHA256 hash. Password reset to temporary value. User must change password.",
                    user.Username);
            }
        }
        context.SaveChanges();
    }
}
