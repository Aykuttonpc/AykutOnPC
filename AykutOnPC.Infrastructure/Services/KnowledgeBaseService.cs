using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public class KnowledgeBaseService(AppDbContext context) : IKnowledgeBaseService
{
    public async Task<IEnumerable<KnowledgeEntry>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.KnowledgeEntries.OrderByDescending(k => k.LastUpdated).ToListAsync(cancellationToken);

    public async Task<KnowledgeEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.KnowledgeEntries.FindAsync([id], cancellationToken);

    public async Task CreateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default)
    {
        entry.LastUpdated = DateTime.UtcNow;
        context.KnowledgeEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default)
    {
        entry.LastUpdated = DateTime.UtcNow;
        context.Update(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.KnowledgeEntries.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            context.KnowledgeEntries.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => await context.KnowledgeEntries.AnyAsync(e => e.Id == id, cancellationToken);
}
