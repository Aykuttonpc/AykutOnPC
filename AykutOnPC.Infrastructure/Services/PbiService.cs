using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public sealed class PbiService(AppDbContext db) : IPbiService
{
    public async Task<WorkspaceSnapshotDto> GetSnapshotAsync(bool includeDone, CancellationToken ct = default)
    {
        var pbiQuery = db.Pbis.AsNoTracking().Include(p => p.PbiLabels).AsQueryable();
        if (!includeDone) pbiQuery = pbiQuery.Where(p => p.State != nameof(PbiState.Done));

        var pbis = await pbiQuery
            .OrderBy(p => p.State).ThenBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new PbiDto
            {
                Id              = p.Id,
                Title           = p.Title,
                Description     = p.Description,
                State           = p.State,
                Priority        = p.Priority,
                DueDateUtc      = p.DueDateUtc,
                SortOrder       = p.SortOrder,
                CreatedAtUtc    = p.CreatedAtUtc,
                UpdatedAtUtc    = p.UpdatedAtUtc,
                CompletedAtUtc  = p.CompletedAtUtc,
                LabelIds        = p.PbiLabels.Select(l => l.LabelId).ToList()
            })
            .ToListAsync(ct);

        var labels = await db.Labels
            .AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .Select(l => new LabelDto { Id = l.Id, Name = l.Name, Color = l.Color, SortOrder = l.SortOrder })
            .ToListAsync(ct);

        return new WorkspaceSnapshotDto { Pbis = pbis, Labels = labels };
    }

    public async Task<Pbi?> CreateAsync(CreatePbiDto dto, CancellationToken ct = default)
    {
        var state = ParseState(dto.State);

        // New cards go to the END of their column so they don't shove the existing sort order.
        var nextSort = await db.Pbis
            .Where(p => p.State == state.ToString())
            .Select(p => (int?)p.SortOrder)
            .MaxAsync(ct);

        var pbi = new Pbi
        {
            Title          = dto.Title.Trim(),
            Description    = dto.Description,
            State          = state.ToString(),
            Priority       = Math.Clamp(dto.Priority, 1, 3),
            DueDateUtc     = dto.DueDateUtc,
            SortOrder      = (nextSort ?? -1) + 1,
            CompletedAtUtc = state == PbiState.Done ? DateTime.UtcNow : null
        };
        db.Pbis.Add(pbi);
        await db.SaveChangesAsync(ct);

        if (dto.LabelIds is { Count: > 0 })
        {
            var existing = await db.Labels.Where(l => dto.LabelIds.Contains(l.Id)).Select(l => l.Id).ToListAsync(ct);
            foreach (var lid in existing.Distinct())
                db.PbiLabels.Add(new PbiLabel { PbiId = pbi.Id, LabelId = lid });
            await db.SaveChangesAsync(ct);
        }

        return pbi;
    }

    public async Task<bool> UpdateAsync(int pbiId, UpdatePbiDto dto, CancellationToken ct = default)
    {
        var pbi = await db.Pbis.Include(p => p.PbiLabels).FirstOrDefaultAsync(p => p.Id == pbiId, ct);
        if (pbi is null) return false;

        if (dto.Title       is not null) pbi.Title       = dto.Title.Trim();
        if (dto.Description is not null) pbi.Description = dto.Description;
        if (dto.Priority    is int pr)   pbi.Priority    = Math.Clamp(pr, 1, 3);
        // DueDateUtc patches always (including to null) — the field is optional and the caller
        // should send what it wants the final value to be.
        pbi.DueDateUtc   = dto.DueDateUtc;
        pbi.UpdatedAtUtc = DateTime.UtcNow;

        // Labels: null = leave alone, [] = clear all, [ids] = replace with this exact set.
        if (dto.LabelIds is not null)
        {
            db.PbiLabels.RemoveRange(pbi.PbiLabels);
            var existing = await db.Labels.Where(l => dto.LabelIds.Contains(l.Id)).Select(l => l.Id).ToListAsync(ct);
            foreach (var lid in existing.Distinct())
                db.PbiLabels.Add(new PbiLabel { PbiId = pbi.Id, LabelId = lid });
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MoveAsync(int pbiId, MovePbiDto dto, CancellationToken ct = default)
    {
        var pbi = await db.Pbis.FirstOrDefaultAsync(p => p.Id == pbiId, ct);
        if (pbi is null) return false;

        var newState   = ParseState(dto.State);
        var prevState  = pbi.State;
        var targetSort = Math.Max(0, dto.SortOrder);

        // Re-index the destination column instead of trusting a single sortOrder value.
        // Otherwise two adjacent drops collide on the same SortOrder and the OrderBy below
        // becomes unstable — the card appears to "jump" on the next snapshot fetch.
        // Approach: load every card in the target state (sans the mover), insert the mover
        // at targetSort, then renumber 0..N-1. Cheap because the column rarely has more
        // than a couple dozen rows for a personal board.
        var column = await db.Pbis
            .Where(p => p.State == newState.ToString() && p.Id != pbiId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync(ct);

        var insertAt = Math.Min(targetSort, column.Count);
        column.Insert(insertAt, pbi);
        for (int i = 0; i < column.Count; i++) column[i].SortOrder = i;

        pbi.State        = newState.ToString();
        pbi.UpdatedAtUtc = DateTime.UtcNow;

        // Done-edge handling: server is the only place we stamp/clear CompletedAtUtc so a
        // misbehaving client can't fake completion times for analytics.
        if (newState == PbiState.Done && prevState != nameof(PbiState.Done))
            pbi.CompletedAtUtc = DateTime.UtcNow;
        else if (newState != PbiState.Done && prevState == nameof(PbiState.Done))
            pbi.CompletedAtUtc = null;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int pbiId, CancellationToken ct = default)
    {
        var rows = await db.Pbis.Where(p => p.Id == pbiId).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    private static PbiState ParseState(string? raw) =>
        Enum.TryParse<PbiState>(raw, ignoreCase: true, out var s) ? s : PbiState.Backlog;
}
