using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public class ProfileService(AppDbContext context) : IProfileService
{
    public async Task<Profile> GetOrCreateProfileAsync(CancellationToken cancellationToken = default)
    {
        var profile = await context.Profiles.FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            profile = new Profile { FullName = "Aykut" };
            context.Profiles.Add(profile);
            await context.SaveChangesAsync(cancellationToken);
        }
        return profile;
    }

    public async Task UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        context.Update(profile);
        await context.SaveChangesAsync(cancellationToken);
    }
}
