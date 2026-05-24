using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public sealed class WidgetService(AppDbContext db) : IWidgetService
{
    // ─── Widget CRUD ────────────────────────────────────────────────

    public async Task<Widget?> CreateAsync(int boardId, int userId, CreateWidgetDto dto, CancellationToken ct = default)
    {
        if (!Enum.TryParse<WidgetType>(dto.Type, ignoreCase: true, out var parsedType))
            return null;

        // Authorize via the parent board, not just by board id — same pattern keeps unrelated
        // users from poking at someone else's board even when multi-tenancy lands later.
        var ownsBoard = await db.Boards.AnyAsync(b => b.Id == boardId && b.UserId == userId, ct);
        if (!ownsBoard) return null;

        var nextSort = await db.Widgets
            .Where(w => w.BoardId == boardId)
            .Select(w => (int?)w.SortOrder)
            .MaxAsync(ct);

        var widget = new Widget
        {
            BoardId   = boardId,
            Type      = parsedType.ToString(),
            Title     = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),
            GridX     = Math.Max(0, dto.GridX),
            GridY     = Math.Max(0, dto.GridY),
            GridW     = Math.Clamp(dto.GridW, 1, 12),
            GridH     = Math.Clamp(dto.GridH, 1, 20),
            ConfigJson = dto.ConfigJson,
            SortOrder = (nextSort ?? -1) + 1
        };
        db.Widgets.Add(widget);
        await db.SaveChangesAsync(ct);
        return widget;
    }

    public async Task<bool> UpdateAsync(int widgetId, int userId, UpdateWidgetDto dto, CancellationToken ct = default)
    {
        var widget = await LoadOwnedWidgetAsync(widgetId, userId, ct);
        if (widget is null) return false;

        if (dto.Title is not null)      widget.Title      = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
        if (dto.ConfigJson is not null) widget.ConfigJson = dto.ConfigJson;
        widget.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> UpdateLayoutAsync(int boardId, int userId, IReadOnlyList<WidgetLayoutDto> layout, CancellationToken ct = default)
    {
        if (layout.Count == 0) return 0;

        var ownsBoard = await db.Boards.AnyAsync(b => b.Id == boardId && b.UserId == userId, ct);
        if (!ownsBoard) return 0;

        // Fetch only the rows actually in the payload; cheaper than loading the whole board.
        var ids = layout.Select(l => l.Id).Distinct().ToList();
        var widgets = await db.Widgets
            .Where(w => w.BoardId == boardId && ids.Contains(w.Id))
            .ToListAsync(ct);

        var byId = layout.ToDictionary(l => l.Id);
        var now  = DateTime.UtcNow;
        var changed = 0;

        foreach (var w in widgets)
        {
            if (!byId.TryGetValue(w.Id, out var pos)) continue;
            if (w.GridX == pos.GridX && w.GridY == pos.GridY && w.GridW == pos.GridW && w.GridH == pos.GridH)
                continue; // no-op — skip the UpdatedAtUtc bump

            w.GridX = pos.GridX;
            w.GridY = pos.GridY;
            w.GridW = Math.Clamp(pos.GridW, 1, 12);
            w.GridH = Math.Clamp(pos.GridH, 1, 20);
            w.UpdatedAtUtc = now;
            changed++;
        }

        if (changed > 0) await db.SaveChangesAsync(ct);
        return changed;
    }

    public async Task<bool> DeleteAsync(int widgetId, int userId, CancellationToken ct = default)
    {
        var widget = await LoadOwnedWidgetAsync(widgetId, userId, ct);
        if (widget is null) return false;

        db.Widgets.Remove(widget); // cascades to items + metrics
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ─── Widget items ───────────────────────────────────────────────

    public async Task<WidgetItem?> AddItemAsync(int widgetId, int userId, AddWidgetItemDto dto, CancellationToken ct = default)
    {
        var widget = await LoadOwnedWidgetAsync(widgetId, userId, ct);
        if (widget is null) return null;

        var nextSort = await db.WidgetItems
            .Where(i => i.WidgetId == widgetId)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(ct);

        var item = new WidgetItem
        {
            WidgetId  = widgetId,
            Label     = dto.Label.Trim(),
            Value     = dto.Value,
            IsDone    = dto.IsDone,
            DoneAtUtc = dto.IsDone == true ? DateTime.UtcNow : null,
            SortOrder = (nextSort ?? -1) + 1
        };
        db.WidgetItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<bool> UpdateItemAsync(int widgetId, int itemId, int userId, UpdateWidgetItemDto dto, CancellationToken ct = default)
    {
        var widget = await LoadOwnedWidgetAsync(widgetId, userId, ct);
        if (widget is null) return false;

        var item = await db.WidgetItems.FirstOrDefaultAsync(i => i.Id == itemId && i.WidgetId == widgetId, ct);
        if (item is null) return false;

        if (dto.Label is not null) item.Label = dto.Label.Trim();
        if (dto.Value is not null) item.Value = dto.Value;

        // IsDone toggle drives DoneAtUtc — server-managed so the client can't fake the timestamp.
        // Only stamp DoneAtUtc when transitioning from "not done" to "done"; clearing the flag
        // wipes the timestamp so streak/heatmap charts see a re-do as a fresh event.
        if (dto.IsDone is bool isDone)
        {
            if (isDone && item.IsDone != true) item.DoneAtUtc = DateTime.UtcNow;
            else if (!isDone)                  item.DoneAtUtc = null;
            item.IsDone = isDone;
        }

        await db.SaveChangesAsync(ct);

        // F4 auto-feed: when a checklist item is freshly checked, append a MetricEntry to any
        // chart widget on the same board that opted in by linking this checklist via ConfigJson.
        // Cheap query — only fires on the IsDone=true transition.
        if (dto.IsDone == true && item.DoneAtUtc is DateTime doneAt)
            await PropagateChecklistAutoFeedAsync(widget, doneAt, ct);

        return true;
    }

    public async Task<bool> DeleteItemAsync(int widgetId, int itemId, int userId, CancellationToken ct = default)
    {
        var widget = await LoadOwnedWidgetAsync(widgetId, userId, ct);
        if (widget is null) return false;

        var rows = await db.WidgetItems
            .Where(i => i.Id == itemId && i.WidgetId == widgetId)
            .ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public async Task<int> ReorderItemsAsync(int widgetId, int userId, IReadOnlyList<WidgetItemOrderDto> order, CancellationToken ct = default)
    {
        var widget = await LoadOwnedWidgetAsync(widgetId, userId, ct);
        if (widget is null) return 0;

        var ids = order.Select(o => o.Id).ToList();
        var items = await db.WidgetItems
            .Where(i => i.WidgetId == widgetId && ids.Contains(i.Id))
            .ToListAsync(ct);

        var bySort = order.ToDictionary(o => o.Id, o => o.SortOrder);
        var changed = 0;
        foreach (var item in items)
        {
            if (!bySort.TryGetValue(item.Id, out var s) || item.SortOrder == s) continue;
            item.SortOrder = s;
            changed++;
        }
        if (changed > 0) await db.SaveChangesAsync(ct);
        return changed;
    }

    // ─── Metric entries ─────────────────────────────────────────────

    public async Task<MetricEntry?> AddMetricAsync(int widgetId, int userId, AddMetricEntryDto dto, CancellationToken ct = default)
    {
        var widget = await LoadOwnedWidgetAsync(widgetId, userId, ct);
        if (widget is null) return null;

        var entry = new MetricEntry
        {
            WidgetId      = widgetId,
            RecordedAtUtc = dto.RecordedAtUtc ?? DateTime.UtcNow,
            Value         = dto.Value,
            Note          = dto.Note
        };
        db.MetricEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<List<MetricPointDto>> GetMetricsAsync(int widgetId, int userId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var widget = await LoadOwnedWidgetAsync(widgetId, userId, ct);
        if (widget is null) return new();

        var q = db.MetricEntries.AsNoTracking().Where(m => m.WidgetId == widgetId);
        if (fromUtc is { } from) q = q.Where(m => m.RecordedAtUtc >= from);
        if (toUtc   is { } to)   q = q.Where(m => m.RecordedAtUtc <= to);

        return await q
            .OrderBy(m => m.RecordedAtUtc)
            .Select(m => new MetricPointDto
            {
                RecordedAtUtc = m.RecordedAtUtc,
                Value         = m.Value,
                Note          = m.Note
            })
            .ToListAsync(ct);
    }

    // ─── Internals ──────────────────────────────────────────────────

    private async Task<Widget?> LoadOwnedWidgetAsync(int widgetId, int userId, CancellationToken ct)
    {
        return await db.Widgets
            .Include(w => w.Board)
            .FirstOrDefaultAsync(w => w.Id == widgetId && w.Board!.UserId == userId, ct);
    }

    /// <summary>
    /// F4: when a checklist item flips done, push a MetricEntry(value=1) to every chart widget
    /// on the same board whose ConfigJson opts in via "sourceChecklistWidgetId": &lt;widgetId&gt;.
    /// One DB write per matched chart widget; rare event so no batching needed.
    /// </summary>
    private async Task PropagateChecklistAutoFeedAsync(Widget checklistWidget, DateTime doneAt, CancellationToken ct)
    {
        if (checklistWidget.Type != nameof(WidgetType.Checklist)) return;

        // Pull every chart-style widget on the same board; filter client-side because EF can't
        // peek inside jsonb easily and the board count is small (single-digit widgets typical).
        var chartWidgets = await db.Widgets
            .Where(w => w.BoardId == checklistWidget.BoardId
                     && w.ArchivedAtUtc == null
                     && w.Id != checklistWidget.Id
                     && (w.Type == nameof(WidgetType.Line)
                      || w.Type == nameof(WidgetType.Bar)
                      || w.Type == nameof(WidgetType.Heatmap)
                      || w.Type == nameof(WidgetType.Streak)
                      || w.Type == nameof(WidgetType.Kpi)))
            .ToListAsync(ct);

        var marker = $"\"sourceChecklistWidgetId\":{checklistWidget.Id}";
        var matched = chartWidgets.Where(w => w.ConfigJson?.Contains(marker) == true).ToList();
        if (matched.Count == 0) return;

        foreach (var chart in matched)
        {
            db.MetricEntries.Add(new MetricEntry
            {
                WidgetId      = chart.Id,
                RecordedAtUtc = doneAt,
                Value         = 1,
                Note          = $"auto: checklist #{checklistWidget.Id} done"
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
