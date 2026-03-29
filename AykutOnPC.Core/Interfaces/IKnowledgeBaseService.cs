using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IKnowledgeBaseService
{
    Task<IEnumerable<KnowledgeEntry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeEntry entry, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
