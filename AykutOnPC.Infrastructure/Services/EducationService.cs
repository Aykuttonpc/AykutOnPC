using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public class EducationService(AppDbContext context) : IEducationService
{
    public async Task<IEnumerable<Education>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Educations.OrderByDescending(e => e.StartDate).ToListAsync(cancellationToken);

    public async Task<Education?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.Educations.FindAsync([id], cancellationToken);

    public async Task CreateAsync(Education education, CancellationToken cancellationToken = default)
    {
        context.Educations.Add(education);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Education education, CancellationToken cancellationToken = default)
    {
        context.Update(education);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Educations.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            context.Educations.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => await context.Educations.AnyAsync(e => e.Id == id, cancellationToken);
}
