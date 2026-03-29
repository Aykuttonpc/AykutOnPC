using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IEducationService
{
    Task<IEnumerable<Education>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Education?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(Education education, CancellationToken cancellationToken = default);
    Task UpdateAsync(Education education, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
