using AykutOnPC.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AykutOnPC.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Spec> Specs { get; set; }
    public DbSet<KnowledgeEntry> KnowledgeEntries { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Experience> Experiences { get; set; }
    public DbSet<Education> Educations { get; set; }
    public DbSet<Profile> Profiles { get; set; }
    public DbSet<PageView> PageViews { get; set; }
    public DbSet<ChatLog> ChatLogs { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Force every DateTime to UTC on save ─────────────────────────────
        // Npgsql 6+ requires Kind=Utc for `timestamp with time zone` columns.
        // ASP.NET model-binding from <input type="date"> / "datetime-local"
        // produces DateTime with Kind=Unspecified, which made every admin form
        // post (Education/Experience/...) throw at SaveChangesAsync with:
        //   "Cannot write DateTime with Kind=Unspecified to PostgreSQL type
        //    'timestamp with time zone', only UTC is supported."
        // Stamping all incoming DateTime values as UTC at the EF layer fixes
        // it once for every current entity AND every future one — no per-
        // controller normalization required.
        var utcDateTime = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullableDateTime = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue
                    ? (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                    : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcDateTime);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(utcNullableDateTime);
            }
        }

        // Configure entities if needed (Fluent API)

        modelBuilder.Entity<Spec>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<KnowledgeEntry>(entity =>
        {
             entity.HasKey(e => e.Id);
             entity.Property(e => e.Topic).HasMaxLength(200);
        });

        modelBuilder.Entity<PageView>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Composite index for dashboard queries: date range + path lookups
            entity.HasIndex(e => e.VisitedAtUtc).HasDatabaseName("IX_PageViews_VisitedAtUtc");
            entity.HasIndex(e => new { e.VisitedAtUtc, e.Path }).HasDatabaseName("IX_PageViews_VisitedAtUtc_Path");
            entity.HasIndex(e => e.HashedIp).HasDatabaseName("IX_PageViews_HashedIp");
        });

        modelBuilder.Entity<ChatLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Conversation memory lookup: "give me the last N turns of this conversation"
            entity.HasIndex(e => new { e.ConversationId, e.TurnIndex }).HasDatabaseName("IX_ChatLogs_Conv_Turn");
            // Admin dashboard list (recency + filter by kind)
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_ChatLogs_CreatedAtUtc");
            entity.HasIndex(e => e.Kind).HasDatabaseName("IX_ChatLogs_Kind");
        });
    }
}
