using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public class KnowledgeBaseService(
    AppDbContext context,
    IEmbeddingService embeddings,
    IMemoryCache cache,
    ILogger<KnowledgeBaseService> logger) : IKnowledgeBaseService
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
        entry.Embedding = await ComputeEmbeddingAsync(entry, cancellationToken);
        context.KnowledgeEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    public async Task UpdateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default)
    {
        entry.LastUpdated = DateTime.UtcNow;
        // Re-embed because Topic/Content/Keywords may have changed. If embed fails we keep
        // the prior vector rather than wiping it — partial degradation beats silent failure.
        var newEmbedding = await ComputeEmbeddingAsync(entry, cancellationToken);
        if (newEmbedding is not null) entry.Embedding = newEmbedding;
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

    public async Task<IReadOnlyList<KnowledgeEntry>> SearchSimilarAsync(
        string queryText, int topK, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queryText) || topK <= 0)
            return Array.Empty<KnowledgeEntry>();

        var queryEmbedding = await embeddings.GenerateAsync(queryText, cancellationToken);
        if (queryEmbedding is null)
        {
            logger.LogWarning("KB SearchSimilar fell back to empty: embedding service returned null");
            return Array.Empty<KnowledgeEntry>();
        }

        var queryVector = new Vector(queryEmbedding);

        // Cosine distance via pgvector's `<=>` operator. HNSW index defined in the migration
        // makes this O(log N). Lower distance = more similar.
        return await context.KnowledgeEntries
            .AsNoTracking()
            .Where(e => e.Embedding != null)
            .OrderBy(e => e.Embedding!.CosineDistance(queryVector))
            .Take(topK)
            .ToListAsync(cancellationToken);
    }

    public async Task<(int Succeeded, int Failed)> BackfillMissingEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        var missing = await context.KnowledgeEntries
            .Where(e => e.Embedding == null)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);

        if (missing.Count == 0) return (0, 0);

        logger.LogInformation("KB backfill: {Count} entries need embeddings", missing.Count);

        var texts = missing.Select(BuildEmbeddingText).ToList();
        var vectors = await embeddings.GenerateBatchAsync(texts, cancellationToken);

        var ok = 0;
        var fail = 0;
        for (var i = 0; i < missing.Count; i++)
        {
            if (vectors[i] is { } vec)
            {
                missing[i].Embedding = new Vector(vec);
                ok++;
            }
            else
            {
                fail++;
                logger.LogWarning("Backfill failed for KnowledgeEntry id={Id} topic={Topic}",
                    missing[i].Id, missing[i].Topic);
            }
        }

        if (ok > 0) await context.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);

        logger.LogInformation("KB backfill done — succeeded={Succeeded} failed={Failed}", ok, fail);
        return (ok, fail);
    }

    private async Task<Vector?> ComputeEmbeddingAsync(KnowledgeEntry entry, CancellationToken ct)
    {
        var text = BuildEmbeddingText(entry);
        var raw = await embeddings.GenerateAsync(text, ct);
        return raw is null ? null : new Vector(raw);
    }

    /// <summary>
    /// Concatenated text that goes into the embedding model. Topic carries the most
    /// semantic weight (titles tend to be focused), so it leads; Content gives detail;
    /// Keywords help bridge synonym gaps the model wouldn't otherwise know.
    /// </summary>
    private static string BuildEmbeddingText(KnowledgeEntry entry)
    {
        var parts = new List<string>(3) { entry.Topic, entry.Content };
        if (!string.IsNullOrWhiteSpace(entry.Keywords))
            parts.Add(entry.Keywords);
        return string.Join("\n", parts);
    }
}
