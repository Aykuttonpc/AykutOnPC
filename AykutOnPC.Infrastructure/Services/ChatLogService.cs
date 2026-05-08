using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AykutOnPC.Infrastructure.Services;

public sealed class ChatLogService(
    AppDbContext db,
    ILogger<ChatLogService> logger) : IChatLogService
{
    public async Task RecordAsync(ChatLog log, CancellationToken ct = default)
    {
        try
        {
            db.ChatLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Logging failure must never crash the chat request — same policy as VisitorAnalyticsService.
            logger.LogWarning(ex, "Failed to record ChatLog (conv={Conv} kind={Kind})", log.ConversationId, log.Kind);
        }
    }

    public async Task<List<ChatLog>> GetRecentTurnsAsync(Guid conversationId, int limit, CancellationToken ct = default)
    {
        if (conversationId == Guid.Empty || limit <= 0) return new();

        // Read newest-first for the index, then return oldest-first for natural prompt ordering.
        var rows = await db.ChatLogs
            .AsNoTracking()
            .Where(c => c.ConversationId == conversationId)
            .OrderByDescending(c => c.TurnIndex)
            .Take(limit)
            .ToListAsync(ct);

        rows.Reverse();
        return rows;
    }

    public async Task<int> GetNextTurnIndexAsync(Guid conversationId, CancellationToken ct = default)
    {
        if (conversationId == Guid.Empty) return 0;

        var max = await db.ChatLogs
            .AsNoTracking()
            .Where(c => c.ConversationId == conversationId)
            .Select(c => (int?)c.TurnIndex)
            .MaxAsync(ct);

        return max.HasValue ? max.Value + 1 : 0;
    }

    public async Task<PagedResult<ChatLog>> SearchAsync(ChatLogQueryDto query, CancellationToken ct = default)
    {
        var page     = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize;

        var q = db.ChatLogs.AsNoTracking().AsQueryable();

        if (query.FromUtc is { } from) q = q.Where(c => c.CreatedAtUtc >= from);
        if (query.ToUtc   is { } to)   q = q.Where(c => c.CreatedAtUtc <= to);
        if (!string.IsNullOrWhiteSpace(query.Kind)) q = q.Where(c => c.Kind == query.Kind);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(c => EF.Functions.ILike(c.UserMessage, $"%{s}%")
                          || (c.BotResponse != null && EF.Functions.ILike(c.BotResponse, $"%{s}%")));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ChatLog>
        {
            Items    = items,
            Total    = total,
            Page     = page,
            PageSize = pageSize
        };
    }

    public async Task<List<ChatLog>> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
        => await db.ChatLogs
            .AsNoTracking()
            .Where(c => c.ConversationId == conversationId)
            .OrderBy(c => c.TurnIndex)
            .ToListAsync(ct);

    public async Task<PagedResult<ChatLog>> GetInboxAsync(
        bool? unreviewedOnly, int page, int pageSize, CancellationToken ct = default)
    {
        page     = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

        // Inbox shows real visitor questions only — filter out short-circuited /
        // safety / not-configured noise. Those are operational, not Q&A material.
        var q = db.ChatLogs.AsNoTracking()
            .Where(c => c.Kind == nameof(ChatErrorKind.Ok) && !c.ShortCircuited);

        if (unreviewedOnly == true) q = q.Where(c => !c.IsReviewed);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(c => c.IsReviewed)            // false first
            .ThenByDescending(c => c.CreatedAtUtc) // newest within each bucket
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ChatLog>
        {
            Items    = items,
            Total    = total,
            Page     = page,
            PageSize = pageSize
        };
    }

    public async Task<ChatLog?> GetByIdAsync(long id, CancellationToken ct = default)
        => await db.ChatLogs.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task MarkReviewedAsync(long id, bool reviewed, string? adminNote, CancellationToken ct = default)
    {
        var log = await db.ChatLogs.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (log is null) return;
        log.IsReviewed = reviewed;
        if (adminNote is not null) log.AdminNote = adminNote;
        await db.SaveChangesAsync(ct);
    }

    public async Task LinkToKnowledgeEntryAsync(long id, int knowledgeEntryId, CancellationToken ct = default)
    {
        var log = await db.ChatLogs.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (log is null) return;
        log.LinkedKnowledgeEntryId = knowledgeEntryId;
        log.IsReviewed = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CountUnreviewedAsync(CancellationToken ct = default)
        => await db.ChatLogs.AsNoTracking()
            .Where(c => c.Kind == nameof(ChatErrorKind.Ok) && !c.ShortCircuited && !c.IsReviewed)
            .CountAsync(ct);

    public async Task<ChatLogStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var now      = DateTime.UtcNow;
        var todayUtc = now.Date;
        var weekAgo  = todayUtc.AddDays(-6);
        var monthAgo = todayUtc.AddDays(-29);

        var totalToday   = await db.ChatLogs.CountAsync(c => c.CreatedAtUtc >= todayUtc, ct);
        var totalWeek    = await db.ChatLogs.CountAsync(c => c.CreatedAtUtc >= weekAgo,  ct);
        var totalMonth   = await db.ChatLogs.CountAsync(c => c.CreatedAtUtc >= monthAgo, ct);

        var byKindRaw = await db.ChatLogs
            .Where(c => c.CreatedAtUtc >= weekAgo)
            .GroupBy(c => c.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var avgLatency = await db.ChatLogs
            .Where(c => c.CreatedAtUtc >= weekAgo && !c.ShortCircuited)
            .Select(c => (double?)c.LatencyMs)
            .AverageAsync(ct) ?? 0;

        var uniqueConvs = await db.ChatLogs
            .Where(c => c.CreatedAtUtc >= weekAgo)
            .Select(c => c.ConversationId)
            .Distinct()
            .CountAsync(ct);

        return new ChatLogStatsDto
        {
            TotalToday                   = totalToday,
            TotalLast7Days               = totalWeek,
            TotalLast30Days              = totalMonth,
            ByKindLast7Days              = byKindRaw.ToDictionary(x => x.Kind, x => x.Count),
            AvgLatencyMsLast7Days        = Math.Round(avgLatency, 0),
            UniqueConversationsLast7Days = uniqueConvs
        };
    }
}
