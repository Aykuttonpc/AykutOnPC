using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public class SpecService(AppDbContext context) : ISpecService
{
    public async Task<IEnumerable<Spec>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Specs.OrderByDescending(s => s.Proficiency).ToListAsync(cancellationToken);

    public async Task<IEnumerable<Spec>> GetTopAsync(int count, CancellationToken cancellationToken = default)
        => await context.Specs.OrderByDescending(s => s.Proficiency).Take(count).ToListAsync(cancellationToken);

    public async Task<Spec?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.Specs.FindAsync([id], cancellationToken);

    public async Task CreateAsync(Spec spec, CancellationToken cancellationToken = default)
    {
        context.Specs.Add(spec);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Spec spec, CancellationToken cancellationToken = default)
    {
        context.Update(spec);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Specs.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            context.Specs.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => await context.Specs.AnyAsync(e => e.Id == id, cancellationToken);
}
