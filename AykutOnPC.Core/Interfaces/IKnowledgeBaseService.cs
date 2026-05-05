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
}
