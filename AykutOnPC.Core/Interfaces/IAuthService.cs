using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IAuthService
{
    Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<User> RegisterUserAsync(string username, string password, CancellationToken cancellationToken = default);
    string GenerateJwtToken(User user, string role = null);
    Task<bool> UserExistsAsync(string username, CancellationToken cancellationToken = default);
}
