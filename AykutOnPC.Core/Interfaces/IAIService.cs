namespace AykutOnPC.Core.Interfaces;

public interface IAiService
{
    /// <summary>
    /// Returns the full assistant answer once the model has finished generating.
    /// Use for non-interactive callers (cron, tests, batch).
    /// </summary>
    Task<string> GetAnswerAsync(string userMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the assistant answer token-by-token as the model produces it.
    /// Used by the chat widget over Server-Sent Events for ChatGPT-style typing UX.
    /// Yields plain text chunks (already decoded from the model's stream events).
    /// </summary>
    IAsyncEnumerable<string> GetStreamingAnswerAsync(string userMessage, CancellationToken cancellationToken = default);
}
