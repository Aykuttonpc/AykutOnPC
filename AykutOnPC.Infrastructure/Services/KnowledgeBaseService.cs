using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AykutOnPC.Infrastructure.Services;

public class KnowledgeBaseService(AppDbContext context, IMemoryCache cache) : IKnowledgeBaseService
{
    private const string CacheKey  = "kb:chat:entries";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<IEnumerable<KnowledgeEntry>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.KnowledgeEntries.OrderByDescending(k => k.LastUpdated).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<KnowledgeEntry>> GetCachedForChatAsync(int limit, CancellationToken cancellationToken = default)
    {
        // Single shared cache entry for the chat prompt context. KB changes rarely; admin
        // mutations invalidate explicitly via Create/Update/Delete below, so a 10-minute
        // TTL is a safety net rather than the primary correctness mechanism.
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<KnowledgeEntry>? cached) && cached is not null)
        {
            return limit > 0 && cached.Count > limit ? cached.Take(limit).ToList() : cached;
        }

        var fresh = await context.KnowledgeEntries
            .AsNoTracking()
            .OrderByDescending(k => k.LastUpdated)
            .Take(Math.Max(limit, 50))
            .ToListAsync(cancellationToken);

        cache.Set(CacheKey, (IReadOnlyList<KnowledgeEntry>)fresh, CacheTtl);

        return limit > 0 && fresh.Count > limit ? fresh.Take(limit).ToList() : fresh;
    }

    public async Task<KnowledgeEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.KnowledgeEntries.FindAsync([id], cancellationToken);

    public async Task CreateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default)
    {
        entry.LastUpdated = DateTime.UtcNow;
        context.KnowledgeEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    public async Task UpdateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default)
    {
        entry.LastUpdated = DateTime.UtcNow;
        context.Update(entry);
        await context.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.KnowledgeEntries.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            context.KnowledgeEntries.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
            cache.Remove(CacheKey);
        }
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => await context.KnowledgeEntries.AnyAsync(e => e.Id == id, cancellationToken);
}
