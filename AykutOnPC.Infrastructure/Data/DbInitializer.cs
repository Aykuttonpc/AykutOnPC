using System.Security.Cryptography;
using System.Text;
using AykutOnPC.Core.Entities;
using Microsoft.Extensions.Configuration;

namespace AykutOnPC.Infrastructure.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context, IConfiguration configuration)
    {
        context.Database.EnsureCreated();

        // Seed Admin User
        if (!context.Users.Any())
        {
            var adminUser = configuration.GetSection("SeedData:AdminUser");
            var password = adminUser["Password"] ?? "admin123";
            
            context.Users.Add(new User
            {
                Username = adminUser["Username"] ?? "aykut",
                PasswordHash = HashPassword(password)
            });
            context.Users.Add(new User 
            { 
               Username = "ai_mcp_user", 
               PasswordHash = HashPassword("McpConnect@2026") 
            }); 
            context.SaveChanges();
        }

        // Seed Specs (Skills)
        if (!context.Specs.Any())
        {
            context.Specs.AddRange(
                new Spec("C#", "Language", 95),
                new Spec(".NET Core", "Framework", 90),
                new Spec("SQL Server", "Database", 85),
                new Spec("Docker", "Tool", 80),
                new Spec("System Design", "Concept", 75)
            );
            context.SaveChanges();
        }
    }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
    
    public static bool VerifyPassword(string inputPassword, string storedHash)
    {
        var hashOfInput = HashPassword(inputPassword);
        return hashOfInput == storedHash;
    }
}
