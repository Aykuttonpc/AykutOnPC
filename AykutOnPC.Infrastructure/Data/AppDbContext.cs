using AykutOnPC.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Spec> Specs { get; set; }
    public DbSet<KnowledgeEntry> KnowledgeEntries { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Experience> Experiences { get; set; }
    public DbSet<Education> Educations { get; set; }
    public DbSet<Profile> Profiles { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}
