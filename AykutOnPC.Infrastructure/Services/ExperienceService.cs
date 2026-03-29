using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public class ExperienceService(AppDbContext context) : IExperienceService
{
    public async Task<IEnumerable<Experience>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Experiences.OrderByDescending(e => e.StartDate).ToListAsync(cancellationToken);

    public async Task<Experience?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.Experiences.FindAsync([id], cancellationToken);

    public async Task CreateAsync(Experience experience, CancellationToken cancellationToken = default)
    {
        context.Experiences.Add(experience);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Experience experience, CancellationToken cancellationToken = default)
    {
        context.Update(experience);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Experiences.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            context.Experiences.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => await context.Experiences.AnyAsync(e => e.Id == id, cancellationToken);
}
