using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

/// <summary>
/// Admin-only personal Kanban board at /Workspace. The Razor view is a thin host;
/// all data flows over /api/workspace/* with cookie auth. Single board, single user —
/// no userId scoping needed today (Admin authorize attribute is the gate).
/// </summary>
[Authorize(Roles = "Admin")]
public class WorkspaceController(IPbiService pbis, ILabelService labels) : Controller
{
    // ── View ─────────────────────────────────────────────────────────

    public IActionResult Index() => View();

    // ── Snapshot ─────────────────────────────────────────────────────

    [HttpGet("/api/workspace/snapshot")]
    public async Task<IActionResult> Snapshot([FromQuery] bool includeDone = true, CancellationToken ct = default)
    {
        var snap = await pbis.GetSnapshotAsync(includeDone, ct);
        return Ok(snap);
    }

    // ── Pbi CRUD ─────────────────────────────────────────────────────

    [HttpPost("/api/workspace/pbis")]
    public async Task<IActionResult> CreatePbi([FromBody] CreatePbiDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var pbi = await pbis.CreateAsync(dto, ct);
        return pbi is null ? BadRequest() : Ok(new { pbi.Id, pbi.State, pbi.SortOrder });
    }

    [HttpPatch("/api/workspace/pbis/{pbiId:int}")]
    public async Task<IActionResult> UpdatePbi(int pbiId, [FromBody] UpdatePbiDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var ok = await pbis.UpdateAsync(pbiId, dto, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("/api/workspace/pbis/{pbiId:int}/move")]
    public async Task<IActionResult> MovePbi(int pbiId, [FromBody] MovePbiDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var ok = await pbis.MoveAsync(pbiId, dto, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("/api/workspace/pbis/{pbiId:int}")]
    public async Task<IActionResult> DeletePbi(int pbiId, CancellationToken ct)
    {
        var ok = await pbis.DeleteAsync(pbiId, ct);
        return ok ? NoContent() : NotFound();
    }

    // ── Label CRUD ───────────────────────────────────────────────────

    [HttpGet("/api/workspace/labels")]
    public async Task<IActionResult> ListLabels(CancellationToken ct) => Ok(await labels.ListAsync(ct));

    [HttpPost("/api/workspace/labels")]
    public async Task<IActionResult> CreateLabel([FromBody] CreateLabelDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var label = await labels.CreateAsync(dto, ct);
        return label is null
            ? Conflict(new { error = "Etiket adı zaten kullanılıyor veya rengi geçersiz (#rrggbb formatında olmalı)." })
            : Ok(new { label.Id, label.Name, label.Color, label.SortOrder });
    }

    [HttpPatch("/api/workspace/labels/{labelId:int}")]
    public async Task<IActionResult> UpdateLabel(int labelId, [FromBody] UpdateLabelDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var ok = await labels.UpdateAsync(labelId, dto, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("/api/workspace/labels/{labelId:int}")]
    public async Task<IActionResult> DeleteLabel(int labelId, CancellationToken ct)
    {
        var ok = await labels.DeleteAsync(labelId, ct);
        return ok ? NoContent() : NotFound();
    }
}
