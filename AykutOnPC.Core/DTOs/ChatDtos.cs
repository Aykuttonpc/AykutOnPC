using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.DTOs;

/// <summary>Non-streaming chat response payload returned by /api/chat/ask.</summary>
public class ChatResponseDto
{
    public string Response { get; set; } = string.Empty;

    /// <summary>String form of <see cref="ChatErrorKind"/>. Drives client behavior (chip render, status dot).</summary>
    public string Kind { get; set; } = nameof(ChatErrorKind.Ok);

    public Guid ConversationId { get; set; }

    /// <summary>Optional follow-up suggestions. Populated for empty/safety/validation outcomes.</summary>
    public List<SuggestionDto>? Suggestions { get; set; }
}

/// <summary>One follow-up chip shown to the user.</summary>
public class SuggestionDto
{
    public string Label { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>Admin search filters for ChatLogs view.</summary>
public class ChatLogQueryDto
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Kind { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Generic page wrapper.</summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(Total / (double)PageSize) : 0;
}

/// <summary>Aggregate stats shown above the ChatLogs admin list.</summary>
public class ChatLogStatsDto
{
    public int TotalToday { get; set; }
    public int TotalLast7Days { get; set; }
    public int TotalLast30Days { get; set; }
    public Dictionary<string, int> ByKindLast7Days { get; set; } = new();
    public double AvgLatencyMsLast7Days { get; set; }
    public int UniqueConversationsLast7Days { get; set; }
}
