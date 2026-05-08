using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

/// <summary>
/// Visitor Question Inbox — the closed feedback loop for RAG. Visitor asks → bot
/// answers from current KB → Aykut reviews here → if the bot's answer was thin,
/// promote it to a KnowledgeEntry. Future RAG retrievals pick up the new entry
/// automatically (Create triggers embedding compute in KnowledgeBaseService).
/// </summary>
[Authorize(Roles = "Admin")]
[Route("admin/inbox")]
public class InboxController(
    IChatLogService chatLogs,
    IKnowledgeBaseService kb) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(bool unreviewedOnly = true, int page = 1, CancellationToken ct = default)
    {
        var result = await chatLogs.GetInboxAsync(unreviewedOnly, page, pageSize: 25, ct);
        ViewBag.UnreviewedOnly = unreviewedOnly;
        ViewBag.UnreviewedCount = await chatLogs.CountUnreviewedAsync(ct);
        return View(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var log = await chatLogs.GetByIdAsync(id, ct);
        if (log is null) return NotFound();
        return View(log);
    }

    [HttpPost("{id:long}/review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleReview(long id, bool reviewed, string? adminNote, CancellationToken ct)
    {
        await chatLogs.MarkReviewedAsync(id, reviewed, adminNote, ct);
        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Promote a turn into a new KnowledgeEntry. The form on /admin/inbox/{id}
    /// is pre-filled with the bot's answer (Aykut edits before saving). Once
    /// saved, the ChatLog is linked + auto-marked reviewed.
    /// </summary>
    [HttpPost("{id:long}/promote")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToKb(
        long id,
        string topic,
        string content,
        string? keywords,
        CancellationToken ct)
    {
        var log = await chatLogs.GetByIdAsync(id, ct);
        if (log is null) return NotFound();

        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(content))
        {
            TempData["InboxError"] = "Topic ve Content boş olamaz.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var entry = new KnowledgeEntry
        {
            Topic    = topic.Trim(),
            Content  = content.Trim(),
            Keywords = keywords?.Trim() ?? string.Empty
        };
        await kb.CreateAsync(entry, ct);
        await chatLogs.LinkToKnowledgeEntryAsync(id, entry.Id, ct);

        TempData["InboxSuccess"] = $"KB entry #{entry.Id} oluşturuldu ve bağlandı.";
        return RedirectToAction(nameof(Index));
    }
}
