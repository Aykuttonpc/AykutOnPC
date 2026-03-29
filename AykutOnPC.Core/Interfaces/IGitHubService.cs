using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IGitHubService
{
    Task<IEnumerable<Build>> GetRepositoriesAsync(string username, CancellationToken cancellationToken = default);
}
