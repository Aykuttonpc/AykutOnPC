using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public sealed class BoardService(AppDbContext db) : IBoardService
{
    public async Task<List<BoardListItemDto>> ListAsync(int userId, bool includeArchived, CancellationToken ct = default)
    {
        var q = db.Boards.AsNoTracking().Where(b => b.UserId == userId);
        if (!includeArchived) q = q.Where(b => b.ArchivedAtUtc == null);

        return await q
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            .Select(b => new BoardListItemDto
            {
                Id         = b.Id,
                Name       = b.Name,
                Kind       = b.Kind,
                SortOrder  = b.SortOrder,
                StartUtc   = b.StartUtc,
                EndUtc     = b.EndUtc,
                IsArchived = b.ArchivedAtUtc != null
            })
            .ToListAsync(ct);
    }

    public async Task<BoardDetailDto?> GetDetailAsync(int boardId, int userId, CancellationToken ct = default)
    {
        // Single round-trip with split queries so EF doesn't return a cartesian.
        var board = await db.Boards
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Widgets.Where(w => w.ArchivedAtUtc == null))
                .ThenInclude(w => w.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId, ct);

        if (board is null) return null;

        return new BoardDetailDto
        {
            Id            = board.Id,
            Name          = board.Name,
            Kind          = board.Kind,
            StartUtc      = board.StartUtc,
            EndUtc        = board.EndUtc,
            GoalText      = board.GoalText,
            RetroNotes    = board.RetroNotes,
            ArchivedAtUtc = board.ArchivedAtUtc,
            Widgets = board.Widgets
                .OrderBy(w => w.SortOrder).ThenBy(w => w.Id)
                .Select(w => new WidgetDto
                {
                    Id         = w.Id,
                    Type       = w.Type,
                    Title      = w.Title,
                    GridX      = w.GridX,
                    GridY      = w.GridY,
                    GridW      = w.GridW,
                    GridH      = w.GridH,
                    ConfigJson = w.ConfigJson,
                    Items = w.Items.Select(i => new WidgetItemDto
                    {
                        Id        = i.Id,
                        Label     = i.Label,
                        Value     = i.Value,
                        IsDone    = i.IsDone,
                        DoneAtUtc = i.DoneAtUtc,
                        SortOrder = i.SortOrder
                    }).ToList()
                })
                .ToList()
        };
    }

    public async Task<int> EnsureDefaultBoardAsync(int userId, CancellationToken ct = default)
    {
        // First non-archived board wins. If none exist, mint one — the user shouldn't see
        // a blank "no board" page on first visit. The default name is intentionally generic
        // so it doesn't feel auto-generated.
        var existing = await db.Boards
            .Where(b => b.UserId == userId && b.ArchivedAtUtc == null)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is int id) return id;

        var fresh = new Board
        {
            UserId    = userId,
            Name      = "My Workspace",
            Kind      = nameof(BoardKind.Standard),
            SortOrder = 0
        };
        db.Boards.Add(fresh);
        await db.SaveChangesAsync(ct);
        return fresh.Id;
    }

    public async Task<Board> CreateAsync(int userId, CreateBoardDto dto, CancellationToken ct = default)
    {
        var kind = ParseKind(dto.Kind);

        // Append to end of the sort sequence — no manual reordering needed for first creation.
        var nextSort = await db.Boards
            .Where(b => b.UserId == userId)
            .Select(b => (int?)b.SortOrder)
            .MaxAsync(ct);

        var board = new Board
        {
            UserId    = userId,
            Name      = dto.Name.Trim(),
            Kind      = kind.ToString(),
            SortOrder = (nextSort ?? -1) + 1,
            StartUtc  = kind == BoardKind.Sprint ? dto.StartUtc : null,
            EndUtc    = kind == BoardKind.Sprint ? dto.EndUtc   : null,
            GoalText  = kind == BoardKind.Sprint ? dto.GoalText : null
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);

        // Sprint boards get bootstrapped with the standard 3-widget set (F3.2). Done here
        // rather than in the controller so any creation path (UI, future API client) gets
        // the same shape.
        if (kind == BoardKind.Sprint)
            await BootstrapSprintWidgetsAsync(board, ct);

        return board;
    }

    public async Task<bool> UpdateAsync(int boardId, int userId, UpdateBoardDto dto, CancellationToken ct = default)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId, ct);
        if (board is null) return false;

        board.Name       = dto.Name.Trim();
        board.GoalText   = dto.GoalText;
        board.RetroNotes = dto.RetroNotes;
        if (board.Kind == nameof(BoardKind.Sprint))
        {
            board.StartUtc = dto.StartUtc;
            board.EndUtc   = dto.EndUtc;
        }
        board.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ArchiveAsync(int boardId, int userId, CancellationToken ct = default)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId, ct);
        if (board is null) return false;

        board.ArchivedAtUtc = DateTime.UtcNow;
        board.UpdatedAtUtc  = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int boardId, int userId, CancellationToken ct = default)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId, ct);
        if (board is null) return false;

        db.Boards.Remove(board); // cascades to widgets → items + metrics
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ── helpers ─────────────────────────────────────────────────────

    private static BoardKind ParseKind(string? raw) =>
        Enum.TryParse<BoardKind>(raw, ignoreCase: true, out var k) ? k : BoardKind.Standard;

    private async Task BootstrapSprintWidgetsAsync(Board board, CancellationToken ct)
    {
        // Default sprint layout: burndown spanning the top, progress on the right,
        // checklist taking the rest. Numbers match Gridstack defaults (12-col grid).
        var widgets = new List<Widget>
        {
            new()
            {
                BoardId = board.Id,
                Type    = nameof(WidgetType.Burndown),
                Title   = "Sprint Burndown",
                GridX   = 0, GridY = 0, GridW = 8, GridH = 4,
                SortOrder = 0
            },
            new()
            {
                BoardId = board.Id,
                Type    = nameof(WidgetType.Progress),
                Title   = "Sprint Progress",
                GridX   = 8, GridY = 0, GridW = 4, GridH = 4,
                ConfigJson = """{"linkedChecklistWidgetId":null,"percentage":0}""",
                SortOrder = 1
            },
            new()
            {
                BoardId = board.Id,
                Type    = nameof(WidgetType.Checklist),
                Title   = "Sprint Backlog",
                GridX   = 0, GridY = 4, GridW = 12, GridH = 6,
                SortOrder = 2
            }
        };
        db.Widgets.AddRange(widgets);
        await db.SaveChangesAsync(ct);
    }
}
