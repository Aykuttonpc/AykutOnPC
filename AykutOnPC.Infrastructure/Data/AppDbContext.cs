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
    public DbSet<BlogPost> BlogPosts { get; set; }
    public DbSet<Pbi> Pbis { get; set; }
    public DbSet<Label> Labels { get; set; }
    public DbSet<PbiLabel> PbiLabels { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // pgvector — required for KnowledgeEntry.Embedding (vector(768)) + HNSW index.
        // Migration generates "CREATE EXTENSION IF NOT EXISTS vector".
        modelBuilder.HasPostgresExtension("vector");

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
             // 768-dim embedding (Gemini text-embedding-004 default). Index added
             // manually in the migration as raw SQL — EF doesn't know HNSW yet.
             entity.Property(e => e.Embedding).HasColumnType("vector(768)");
        });

        modelBuilder.Entity<PageView>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Composite index for dashboard queries: date range + path lookups
            entity.HasIndex(e => e.VisitedAtUtc).HasDatabaseName("IX_PageViews_VisitedAtUtc");
            entity.HasIndex(e => new { e.VisitedAtUtc, e.Path }).HasDatabaseName("IX_PageViews_VisitedAtUtc_Path");
            entity.HasIndex(e => e.HashedIp).HasDatabaseName("IX_PageViews_HashedIp");
            // Unique-visitor count uses COALESCE(VisitorId, HashedIp); index on VisitorId
            // keeps the DISTINCT scan cheap.
            entity.HasIndex(e => e.VisitorId).HasDatabaseName("IX_PageViews_VisitorId");
        });

        modelBuilder.Entity<ChatLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Conversation memory lookup: "give me the last N turns of this conversation"
            entity.HasIndex(e => new { e.ConversationId, e.TurnIndex }).HasDatabaseName("IX_ChatLogs_Conv_Turn");
            // Admin dashboard list (recency + filter by kind)
            entity.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_ChatLogs_CreatedAtUtc");
            entity.HasIndex(e => e.Kind).HasDatabaseName("IX_ChatLogs_Kind");
            // Inbox sort: unreviewed first, newest within each bucket
            entity.HasIndex(e => new { e.IsReviewed, e.CreatedAtUtc }).HasDatabaseName("IX_ChatLogs_Reviewed_CreatedAt");
        });

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("IX_BlogPosts_Slug");
            // Public listing: "give me published posts ordered by publish date"
            entity.HasIndex(e => new { e.IsPublished, e.PublishedAtUtc }).HasDatabaseName("IX_BlogPosts_Published_PublishedAt");
        });

        // ── Workspace Kanban (Pbis + Labels + PbiLabels) ──────────────────────
        // Single-user, single-board Azure DevOps-style Kanban. State drives the column
        // (Backlog/Todo/Doing/Done), SortOrder positions within the column. Labels are
        // M:N via PbiLabels; junction has a composite PK so the same label can't be
        // attached twice. Cascade on Pbi delete → junction rows go; Label delete cascades
        // its own junction rows but leaves the Pbi alone.
        modelBuilder.Entity<Pbi>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(300);
            entity.Property(e => e.State).IsRequired().HasMaxLength(20);
            // Column render is hot path: "give me every Pbi grouped by state, sorted within"
            entity.HasIndex(e => new { e.State, e.SortOrder }).HasDatabaseName("IX_Pbis_State_Sort");
            entity.HasIndex(e => e.CompletedAtUtc).HasDatabaseName("IX_Pbis_CompletedAt");
        });

        modelBuilder.Entity<Label>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(40);
            entity.Property(e => e.Color).IsRequired().HasMaxLength(7);
            entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("IX_Labels_Name");
        });

        modelBuilder.Entity<PbiLabel>(entity =>
        {
            entity.HasKey(e => new { e.PbiId, e.LabelId });
            entity.HasOne(e => e.Pbi)
                  .WithMany(p => p.PbiLabels)
                  .HasForeignKey(e => e.PbiId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Label)
                  .WithMany(l => l.PbiLabels)
                  .HasForeignKey(e => e.LabelId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.LabelId).HasDatabaseName("IX_PbiLabels_LabelId");
        });
    }
}
