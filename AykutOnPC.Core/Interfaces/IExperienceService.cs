using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IExperienceService
{
    Task<IEnumerable<Experience>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Experience?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(Experience experience, CancellationToken cancellationToken = default);
    Task UpdateAsync(Experience experience, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
