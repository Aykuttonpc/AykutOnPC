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
    public DbSet<Board> Boards { get; set; }
    public DbSet<Widget> Widgets { get; set; }
    public DbSet<WidgetItem> WidgetItems { get; set; }
    public DbSet<MetricEntry> MetricEntries { get; set; }
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

        // ── Workspace (Boards/Widgets/WidgetItems/MetricEntries) ──────────────
        // Tree: User → Board → Widget → WidgetItem + MetricEntry. Cascade deletes
        // keep cleanup atomic (delete a board → all widgets + their content go too).
        // ConfigJson / MetaJson live in jsonb so per-widget settings stay flexible
        // without schema churn for every new widget type.
        modelBuilder.Entity<Board>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Kind).IsRequired().HasMaxLength(20);
            // Board list view: order by UserId + active first + sort order
            entity.HasIndex(e => new { e.UserId, e.ArchivedAtUtc, e.SortOrder }).HasDatabaseName("IX_Boards_User_Archived_Sort");
        });

        modelBuilder.Entity<Widget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(30);
            entity.Property(e => e.ConfigJson).HasColumnType("jsonb");
            entity.HasOne(e => e.Board)
                  .WithMany(b => b.Widgets)
                  .HasForeignKey(e => e.BoardId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Hot path: "render this board" → fetch widgets ordered by sort then grid
            entity.HasIndex(e => new { e.BoardId, e.ArchivedAtUtc, e.SortOrder }).HasDatabaseName("IX_Widgets_Board_Archived_Sort");
        });

        modelBuilder.Entity<WidgetItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(500);
            entity.Property(e => e.MetaJson).HasColumnType("jsonb");
            entity.HasOne(e => e.Widget)
                  .WithMany(w => w.Items)
                  .HasForeignKey(e => e.WidgetId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WidgetId, e.SortOrder }).HasDatabaseName("IX_WidgetItems_Widget_Sort");
        });

        modelBuilder.Entity<MetricEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Widget)
                  .WithMany()
                  .HasForeignKey(e => e.WidgetId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Time-series query: "last 30 days of values for this widget"
            entity.HasIndex(e => new { e.WidgetId, e.RecordedAtUtc }).HasDatabaseName("IX_MetricEntries_Widget_RecordedAt");
        });
    }
}
