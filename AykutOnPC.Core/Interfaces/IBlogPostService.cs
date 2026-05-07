using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IBlogPostService
{
    Task<IReadOnlyList<BlogPost>> GetAllForAdminAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BlogPost>> GetPublishedAsync(int? limit = null, CancellationToken cancellationToken = default);
    Task<BlogPost?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<BlogPost?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task CreateAsync(BlogPost post, CancellationToken cancellationToken = default);
    Task UpdateAsync(BlogPost post, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
