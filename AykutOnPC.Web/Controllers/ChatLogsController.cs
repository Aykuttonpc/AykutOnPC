using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ChatLogsController(IChatLogService chatLogs) : Controller
{
    public async Task<IActionResult> Index(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? kind,
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = new ChatLogQueryDto
        {
            FromUtc  = fromUtc,
            ToUtc    = toUtc,
            Kind     = string.IsNullOrWhiteSpace(kind) ? null : kind,
            Search   = string.IsNullOrWhiteSpace(search) ? null : search,
            Page     = page,
            PageSize = 50
        };

        var result = await chatLogs.SearchAsync(query, cancellationToken);
        var stats  = await chatLogs.GetStatsAsync(cancellationToken);

        ViewBag.Query = query;
        ViewBag.Stats = stats;

        return View(result);
    }

    public async Task<IActionResult> Detail(Guid conversationId, CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty) return NotFound();

        var conversation = await chatLogs.GetConversationAsync(conversationId, cancellationToken);
        if (conversation.Count == 0) return NotFound();

        ViewBag.ConversationId = conversationId;
        return View(conversation);
    }
}
