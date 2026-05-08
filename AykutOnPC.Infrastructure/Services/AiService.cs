using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace AykutOnPC.Infrastructure.Services;

public class AiService : IAiService
{
    private readonly IKnowledgeBaseService _kb;
    private readonly ILogger<AiService>    _logger;
    private readonly Kernel                _kernel;
    private readonly AiSettings            _settings;

    public AiService(
        IKnowledgeBaseService kb,
        Kernel kernel,
        IOptions<AiSettings> aiOptions,
        ILogger<AiService> logger)
    {
        _kb       = kb;
        _kernel   = kernel;
        _logger   = logger;
        _settings = aiOptions.Value;

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            _logger.LogWarning("AI API key is not configured. Chat will not function.");
    }

    public async Task<ChatTurnResult> AnswerAsync(
        string userMessage,
        IReadOnlyList<ChatLog> history,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            return new ChatTurnResult(_settings.ErrorMessages.ApiNotConfigured, ChatErrorKind.NotConfigured, _settings.ModelId);

        try
        {
            var chatHistory = await BuildChatHistoryAsync(userMessage, history, cancellationToken);
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);

            var content = response.Content;
            if (string.IsNullOrWhiteSpace(content))
                return new ChatTurnResult(_settings.ErrorMessages.EmptyResponse, ChatErrorKind.Empty, _settings.ModelId);

            return new ChatTurnResult(content, ChatErrorKind.Ok, _settings.ModelId);
        }
        catch (Exception ex)
        {
            var kind    = Classify(ex);
            var message = MapKindToMessage(kind);
            _logger.Log(kind == ChatErrorKind.RateLimit ? LogLevel.Warning : LogLevel.Error,
                ex, "AI answer failed kind={Kind}", kind);
            return new ChatTurnResult(message, kind, _settings.ModelId);
        }
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamAnswerAsync(
        string userMessage,
        IReadOnlyList<ChatLog> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            yield return Terminal(ChatErrorKind.NotConfigured, _settings.ErrorMessages.ApiNotConfigured);
            yield break;
        }

        // ── Phase 1: build prompt ─────────────────────────
        // C# disallows `yield return` inside try/catch, so the same sentinel pattern as before:
        // hold a "fatal" message in a local and inspect after the try/catch.
        ChatHistory? chatHistory = null;
        ChatErrorKind? prefatalKind = null;
        try
        {
            chatHistory = await BuildChatHistoryAsync(userMessage, history, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI streaming failed during prompt assembly.");
            prefatalKind = ChatErrorKind.Unknown;
        }
        if (prefatalKind is { } pk)
        {
            yield return Terminal(pk, MapKindToMessage(pk));
            yield break;
        }

        // ── Phase 2: open the model stream ────────────────
        IAsyncEnumerator<StreamingChatMessageContent>? enumerator = null;
        ChatErrorKind? openErrorKind = null;
        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            enumerator = chatService
                .GetStreamingChatMessageContentsAsync(chatHistory!, cancellationToken: cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex)
        {
            openErrorKind = Classify(ex);
            _logger.Log(openErrorKind == ChatErrorKind.RateLimit ? LogLevel.Warning : LogLevel.Error,
                ex, "AI streaming failed to open kind={Kind}", openErrorKind);
        }
        if (openErrorKind is { } oek)
        {
            yield return Terminal(oek, MapKindToMessage(oek));
            yield break;
        }

        // ── Phase 3: drain tokens, classify any mid-stream exception ─
        var fullText = new StringBuilder();
        ChatErrorKind terminalKind = ChatErrorKind.Ok;
        try
        {
            while (true)
            {
                bool   hasNext;
                string? chunk = null;
                ChatErrorKind? chunkErrorKind = null;
                try
                {
                    hasNext = await enumerator!.MoveNextAsync();
                    chunk   = hasNext ? enumerator.Current.Content : null;
                }
                catch (Exception ex)
                {
                    chunkErrorKind = Classify(ex);
                    _logger.Log(chunkErrorKind == ChatErrorKind.RateLimit ? LogLevel.Warning : LogLevel.Error,
                        ex, "AI streaming failed mid-stream kind={Kind}", chunkErrorKind);
                    hasNext = false;
                }

                if (chunkErrorKind is { } cek)
                {
                    terminalKind = cek;
                    break;
                }
                if (!hasNext) break;
                if (string.IsNullOrEmpty(chunk)) continue;

                fullText.Append(chunk);
                yield return new ChatStreamChunk(chunk, null, null, _settings.ModelId);
            }
        }
        finally
        {
            if (enumerator is not null)
                await enumerator.DisposeAsync();
        }

        // Empty completion (all chunks empty / safety filter that produced no output).
        if (terminalKind == ChatErrorKind.Ok && fullText.Length == 0)
            terminalKind = ChatErrorKind.Empty;

        if (terminalKind == ChatErrorKind.Ok)
            yield return Terminal(ChatErrorKind.Ok, fullText.ToString(), _settings.ModelId);
        else
        {
            // Mid-stream failure: surface a friendly trailing message AND the kind so the client
            // can render the right chips. We append (don't replace) any partial text it already saw.
            var trailing = MapKindToMessage(terminalKind);
            yield return Terminal(terminalKind, fullText.Length > 0 ? $"{fullText}\n\n{trailing}" : trailing, _settings.ModelId);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<ChatHistory> BuildChatHistoryAsync(
        string userMessage,
        IReadOnlyList<ChatLog> history,
        CancellationToken cancellationToken)
    {
        // RAG path first when enabled. If it returns empty (embedding service down,
        // KB not yet backfilled, or query embed failed) we fall back to the legacy
        // KB-stuffing path so the chat keeps working — graceful degradation beats
        // "the chat is broken because the embedding API is rate-limited".
        IReadOnlyList<KnowledgeEntry> entries;
        if (_settings.UseRagRetrieval)
        {
            entries = await _kb.SearchSimilarAsync(userMessage, _settings.RetrievalTopK, cancellationToken);
            if (entries.Count == 0)
            {
                _logger.LogInformation("RAG retrieval returned 0 entries — falling back to KB stuffing.");
                entries = await _kb.GetCachedForChatAsync(_settings.ContextLimit, cancellationToken);
            }
        }
        else
        {
            entries = await _kb.GetCachedForChatAsync(_settings.ContextLimit, cancellationToken);
        }

        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine(_settings.SystemPrompt);
        contextBuilder.AppendLine("\n--- CONTEXT START ---");

        foreach (var entry in entries)
        {
            contextBuilder.Append("Topic: ").AppendLine(entry.Topic);
            contextBuilder.Append("Info: ").AppendLine(entry.Content);
            contextBuilder.AppendLine("-");
        }
        contextBuilder.AppendLine("--- CONTEXT END ---");

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(contextBuilder.ToString());

        // Replay prior turns from this conversation as alternating user/assistant messages so
        // the model can resolve pronouns ("peki ya React?") without us re-stuffing every detail.
        foreach (var turn in history)
        {
            if (!string.IsNullOrWhiteSpace(turn.UserMessage))
                chatHistory.AddUserMessage(turn.UserMessage);
            if (!string.IsNullOrWhiteSpace(turn.BotResponse) && turn.Kind == nameof(ChatErrorKind.Ok))
                chatHistory.AddAssistantMessage(turn.BotResponse);
        }

        chatHistory.AddUserMessage(userMessage);
        return chatHistory;
    }

    private string MapKindToMessage(ChatErrorKind kind) => kind switch
    {
        ChatErrorKind.NotConfigured => _settings.ErrorMessages.ApiNotConfigured,
        ChatErrorKind.RateLimit     => _settings.ErrorMessages.RateLimitHit,
        ChatErrorKind.Empty         => _settings.ErrorMessages.EmptyResponse,
        ChatErrorKind.Safety        => _settings.ErrorMessages.SafetyBlocked,
        ChatErrorKind.Network       => _settings.ErrorMessages.NetworkError,
        ChatErrorKind.Validation    => _settings.ErrorMessages.ValidationError,
        _                           => _settings.ErrorMessages.GeneralError
    };

    /// <summary>
    /// Heuristic exception classifier. Provider SDKs use string messages (rather than typed
    /// exceptions) for most failure modes, so we sniff the message and inner-exception chain.
    /// </summary>
    private static ChatErrorKind Classify(Exception ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            switch (cur)
            {
                case TaskCanceledException:
                case TimeoutException:
                case HttpRequestException:
                case SocketException:
                    return ChatErrorKind.Network;
            }

            var msg = cur.Message ?? string.Empty;
            if (msg.Contains("429") || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) || msg.Contains("quota", StringComparison.OrdinalIgnoreCase))
                return ChatErrorKind.RateLimit;
            if (msg.Contains("safety", StringComparison.OrdinalIgnoreCase)
             || msg.Contains("content filter", StringComparison.OrdinalIgnoreCase)
             || msg.Contains("blocked", StringComparison.OrdinalIgnoreCase)
             || msg.Contains("prohibited", StringComparison.OrdinalIgnoreCase)
             || msg.Contains("RECITATION", StringComparison.OrdinalIgnoreCase))
                return ChatErrorKind.Safety;
        }
        return ChatErrorKind.Unknown;
    }

    private ChatStreamChunk Terminal(ChatErrorKind kind, string fullContent, string? modelId = null)
        => new(null, kind, fullContent, modelId ?? _settings.ModelId);
}
