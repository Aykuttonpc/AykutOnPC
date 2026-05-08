using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IKnowledgeBaseService
{
    Task<IEnumerable<KnowledgeEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cached read used by the chat path on every turn. Cache invalidates on Create/Update/Delete.
    /// Returns up to <paramref name="limit"/> entries; the chat prompt typically fits ~50 well.
    /// </summary>
    Task<IReadOnlyList<KnowledgeEntry>> GetCachedForChatAsync(int limit, CancellationToken cancellationToken = default);

    Task<KnowledgeEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <paramref name="topK"/> entries most similar to <paramref name="queryText"/> by
    /// cosine distance over the embedding column. Used by the chat path to assemble a focused
    /// prompt instead of stuffing every KB entry. Returns empty list when embedding compute
    /// fails — the caller should fall back to <see cref="GetCachedForChatAsync"/>.
    /// </summary>
    Task<IReadOnlyList<KnowledgeEntry>> SearchSimilarAsync(string queryText, int topK, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes embeddings for any KB entry where <c>Embedding IS NULL</c>. Used by the
    /// `--backfill-embeddings` CLI command after migration. Returns (succeeded, failed) counts.
    /// </summary>
    Task<(int Succeeded, int Failed)> BackfillMissingEmbeddingsAsync(CancellationToken cancellationToken = default);
}
