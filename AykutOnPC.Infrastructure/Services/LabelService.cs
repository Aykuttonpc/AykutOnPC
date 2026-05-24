using System.Text.RegularExpressions;
using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public sealed class LabelService(AppDbContext db) : ILabelService
{
    private static readonly Regex HexColor = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public async Task<List<LabelDto>> ListAsync(CancellationToken ct = default) =>
        await db.Labels
            .AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .Select(l => new LabelDto { Id = l.Id, Name = l.Name, Color = l.Color, SortOrder = l.SortOrder })
            .ToListAsync(ct);

    public async Task<Label?> CreateAsync(CreateLabelDto dto, CancellationToken ct = default)
    {
        var name  = dto.Name.Trim();
        var color = NormalizeColor(dto.Color);
        if (color is null) return null;

        // Name is unique by index; let the DB enforce, surface as null on collision.
        if (await db.Labels.AnyAsync(l => l.Name == name, ct)) return null;

        var nextSort = await db.Labels.Select(l => (int?)l.SortOrder).MaxAsync(ct);

        var label = new Label
        {
            Name      = name,
            Color     = color,
            SortOrder = (nextSort ?? -1) + 1
        };
        db.Labels.Add(label);
        await db.SaveChangesAsync(ct);
        return label;
    }

    public async Task<bool> UpdateAsync(int labelId, UpdateLabelDto dto, CancellationToken ct = default)
    {
        var label = await db.Labels.FirstOrDefaultAsync(l => l.Id == labelId, ct);
        if (label is null) return false;

        if (dto.Name is not null)
        {
            var name = dto.Name.Trim();
            // Same uniqueness check as create; allow keeping the current name.
            if (name != label.Name && await db.Labels.AnyAsync(l => l.Name == name, ct))
                return false;
            label.Name = name;
        }
        if (dto.Color is not null)
        {
            var c = NormalizeColor(dto.Color);
            if (c is null) return false;
            label.Color = c;
        }
        if (dto.SortOrder is int s) label.SortOrder = s;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int labelId, CancellationToken ct = default)
    {
        // PbiLabels cascade on Label delete (configured in DbContext), so this single hit cleans
        // up both the label and every card's tag attachment to it.
        var rows = await db.Labels.Where(l => l.Id == labelId).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    private static string? NormalizeColor(string raw)
    {
        var v = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        return HexColor.IsMatch(v) ? v : null;
    }
}
