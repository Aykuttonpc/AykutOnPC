using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AykutOnPC.Infrastructure.Data;

/// <summary>
/// Design-time DbContext factory used by `dotnet ef migrations`.
/// The full Program.cs is unsafe to bootstrap at design time (requires JWT key,
/// DB connection, etc. that are only set in real runtime/CI environments), so
/// the EF tool needs an isolated entry point. Connection string is purely a
/// placeholder — migrations are SQL generation, not actual queries.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=AykutOnPC_Db;Username=postgres;Password=design-time";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new AppDbContext(options);
    }
}
