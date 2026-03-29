using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IProfileService
{
    Task<Profile> GetOrCreateProfileAsync(CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default);
}
