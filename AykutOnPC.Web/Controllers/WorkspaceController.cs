using System.Security.Claims;
using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

/// <summary>
/// Admin-only workspace dashboard. The Razor view at GET /Workspace is a thin
/// hosting page; all data lives behind /api/workspace/* and is loaded over fetch.
/// One controller hosts both surfaces so the auth attribute applies uniformly.
/// </summary>
[Authorize(Roles = "Admin")]
public class WorkspaceController(IBoardService boards, IWidgetService widgets) : Controller
{
    // ─── View ───────────────────────────────────────────────────────

    public async Task<IActionResult> Index(int? boardId, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        // Either jump to the requested board or land on the default. EnsureDefaultBoardAsync
        // mints a fresh "My Workspace" board on first visit so the view never has to handle
        // a null-board state.
        var targetId = boardId ?? await boards.EnsureDefaultBoardAsync(userId.Value, ct);
        ViewBag.InitialBoardId = targetId;
        return View();
    }

    // ─── Boards API ─────────────────────────────────────────────────

    [HttpGet("/api/workspace/boards")]
    public async Task<IActionResult> ListBoards([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var list = await boards.ListAsync(userId.Value, includeArchived, ct);
        return Ok(list);
    }

    [HttpGet("/api/workspace/boards/{boardId:int}")]
    public async Task<IActionResult> GetBoard(int boardId, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var detail = await boards.GetDetailAsync(boardId, userId.Value, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("/api/workspace/boards")]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBoardDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var board = await boards.CreateAsync(userId.Value, dto, ct);
        return Ok(new { board.Id, board.Name, board.Kind, board.SortOrder });
    }

    [HttpPatch("/api/workspace/boards/{boardId:int}")]
    public async Task<IActionResult> UpdateBoard(int boardId, [FromBody] UpdateBoardDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var ok = await boards.UpdateAsync(boardId, userId.Value, dto, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("/api/workspace/boards/{boardId:int}/archive")]
    public async Task<IActionResult> ArchiveBoard(int boardId, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();
        var ok = await boards.ArchiveAsync(boardId, userId.Value, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("/api/workspace/boards/{boardId:int}")]
    public async Task<IActionResult> DeleteBoard(int boardId, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();
        var ok = await boards.DeleteAsync(boardId, userId.Value, ct);
        return ok ? NoContent() : NotFound();
    }

    // ─── Widgets API ────────────────────────────────────────────────

    [HttpPost("/api/workspace/boards/{boardId:int}/widgets")]
    public async Task<IActionResult> CreateWidget(int boardId, [FromBody] CreateWidgetDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var widget = await widgets.CreateAsync(boardId, userId.Value, dto, ct);
        if (widget is null) return BadRequest(new { error = "Unknown widget type or board not found." });

        return Ok(new
        {
            widget.Id, widget.Type, widget.Title,
            widget.GridX, widget.GridY, widget.GridW, widget.GridH,
            widget.ConfigJson
        });
    }

    [HttpPatch("/api/workspace/widgets/{widgetId:int}")]
    public async Task<IActionResult> UpdateWidget(int widgetId, [FromBody] UpdateWidgetDto dto, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();
        var ok = await widgets.UpdateAsync(widgetId, userId.Value, dto, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("/api/workspace/boards/{boardId:int}/widgets/layout")]
    public async Task<IActionResult> UpdateLayout(int boardId, [FromBody] List<WidgetLayoutDto> layout, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var changed = await widgets.UpdateLayoutAsync(boardId, userId.Value, layout, ct);
        return Ok(new { changed });
    }

    [HttpDelete("/api/workspace/widgets/{widgetId:int}")]
    public async Task<IActionResult> DeleteWidget(int widgetId, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();
        var ok = await widgets.DeleteAsync(widgetId, userId.Value, ct);
        return ok ? NoContent() : NotFound();
    }

    // ─── Items API ──────────────────────────────────────────────────

    [HttpPost("/api/workspace/widgets/{widgetId:int}/items")]
    public async Task<IActionResult> AddItem(int widgetId, [FromBody] AddWidgetItemDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var item = await widgets.AddItemAsync(widgetId, userId.Value, dto, ct);
        if (item is null) return NotFound();

        return Ok(new { item.Id, item.Label, item.Value, item.IsDone, item.DoneAtUtc, item.SortOrder });
    }

    [HttpPatch("/api/workspace/widgets/{widgetId:int}/items/{itemId:int}")]
    public async Task<IActionResult> UpdateItem(int widgetId, int itemId, [FromBody] UpdateWidgetItemDto dto, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var ok = await widgets.UpdateItemAsync(widgetId, itemId, userId.Value, dto, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("/api/workspace/widgets/{widgetId:int}/items/{itemId:int}")]
    public async Task<IActionResult> DeleteItem(int widgetId, int itemId, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();
        var ok = await widgets.DeleteItemAsync(widgetId, itemId, userId.Value, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("/api/workspace/widgets/{widgetId:int}/items/reorder")]
    public async Task<IActionResult> ReorderItems(int widgetId, [FromBody] List<WidgetItemOrderDto> order, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();
        var changed = await widgets.ReorderItemsAsync(widgetId, userId.Value, order, ct);
        return Ok(new { changed });
    }

    // ─── Metric API ─────────────────────────────────────────────────

    [HttpPost("/api/workspace/widgets/{widgetId:int}/metrics")]
    public async Task<IActionResult> AddMetric(int widgetId, [FromBody] AddMetricEntryDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var entry = await widgets.AddMetricAsync(widgetId, userId.Value, dto, ct);
        return entry is null ? NotFound() : Ok(new { entry.Id, entry.RecordedAtUtc, entry.Value, entry.Note });
    }

    [HttpGet("/api/workspace/widgets/{widgetId:int}/metrics")]
    public async Task<IActionResult> GetMetrics(int widgetId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return Forbid();

        var points = await widgets.GetMetricsAsync(widgetId, userId.Value, fromUtc, toUtc, ct);
        return Ok(points);
    }

    // ─── helpers ───────────────────────────────────────────────────

    /// <summary>
    /// User.Id comes from the JWT NameIdentifier claim minted in AuthService — every authenticated
    /// admin request carries it. Returns null if the claim is missing or not parseable so callers
    /// can short-circuit to Forbid().
    /// </summary>
    private int? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
