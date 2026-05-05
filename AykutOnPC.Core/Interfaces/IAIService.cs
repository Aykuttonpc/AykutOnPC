using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IAiService
{
    /// <summary>
    /// Returns the full assistant answer once the model has finished generating.
    /// Use for non-interactive callers (curl, tests) and as a fallback when streaming fails.
    /// </summary>
    /// <param name="userMessage">The current user turn (already validated by the caller).</param>
    /// <param name="history">Prior turns of the same conversation, oldest first. Empty for fresh conversations.</param>
    Task<ChatTurnResult> AnswerAsync(
        string userMessage,
        IReadOnlyList<ChatLog> history,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the assistant answer chunk-by-chunk. The last yielded item carries the
    /// terminal classification and the assembled full content (token text is null in the terminal).
    /// </summary>
    IAsyncEnumerable<ChatStreamChunk> StreamAnswerAsync(
        string userMessage,
        IReadOnlyList<ChatLog> history,
        CancellationToken cancellationToken = default);
}

/// <summary>One-shot result of <see cref="IAiService.AnswerAsync"/>.</summary>
public sealed record ChatTurnResult(string Content, ChatErrorKind Kind, string ModelId);

/// <summary>
/// One frame of a streaming response. Either:
/// • <see cref="Token"/> is non-null and <see cref="FinalKind"/> is null (mid-stream token), or
/// • <see cref="Token"/> is null and <see cref="FinalKind"/> is non-null (terminal frame, includes <see cref="FullContent"/>).
/// </summary>
public sealed record ChatStreamChunk(
    string? Token,
    ChatErrorKind? FinalKind,
    string? FullContent,
    string? ModelId);
