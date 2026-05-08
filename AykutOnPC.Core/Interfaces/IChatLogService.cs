using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IChatLogService
{
    /// <summary>Persists a single turn. Failures are swallowed and logged — never crash the chat path.</summary>
    Task RecordAsync(ChatLog log, CancellationToken ct = default);

    /// <summary>Returns the last N turns of a conversation, oldest first, for prompt-time memory.</summary>
    Task<List<ChatLog>> GetRecentTurnsAsync(Guid conversationId, int limit, CancellationToken ct = default);

    /// <summary>Returns the next TurnIndex for a conversation (current max + 1, or 0 for new).</summary>
    Task<int> GetNextTurnIndexAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>Paged search for the admin list view.</summary>
    Task<PagedResult<ChatLog>> SearchAsync(ChatLogQueryDto query, CancellationToken ct = default);

    /// <summary>All turns of a single conversation in order, for the admin detail view.</summary>
    Task<List<ChatLog>> GetConversationAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>Aggregate stats panel.</summary>
    Task<ChatLogStatsDto> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Inbox view — all turns where <c>Kind = Ok</c>, unreviewed first, newest within
    /// each bucket. Drives the admin "questions waiting for me" workflow.
    /// </summary>
    Task<PagedResult<ChatLog>> GetInboxAsync(bool? unreviewedOnly, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Single ChatLog by Id — for the inbox detail view.</summary>
    Task<ChatLog?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Mark reviewed / unreviewed and optionally save an admin note.</summary>
    Task MarkReviewedAsync(long id, bool reviewed, string? adminNote, CancellationToken ct = default);

    /// <summary>Link a ChatLog turn to a created KnowledgeEntry — closes the feedback loop.</summary>
    Task LinkToKnowledgeEntryAsync(long id, int knowledgeEntryId, CancellationToken ct = default);

    /// <summary>Count of unreviewed Ok-kind turns — used by the layout nav badge.</summary>
    Task<int> CountUnreviewedAsync(CancellationToken ct = default);
}
