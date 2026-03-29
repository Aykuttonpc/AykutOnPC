using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface ISpecService
{
    Task<IEnumerable<Spec>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Spec>> GetTopAsync(int count, CancellationToken cancellationToken = default);
    Task<Spec?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(Spec spec, CancellationToken cancellationToken = default);
    Task UpdateAsync(Spec spec, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
