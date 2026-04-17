using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;
using System.Text;

namespace AykutOnPC.Infrastructure.Services;

public class AiService : IAiService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AiService> _logger;
    private readonly Kernel _kernel;
    private readonly AiSettings _settings;

    public AiService(
        AppDbContext context,
        Kernel kernel,
        IOptions<AiSettings> aiOptions,
        ILogger<AiService> logger)
    {
        _context  = context;
        _kernel   = kernel;
        _logger   = logger;
        _settings = aiOptions.Value;

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            _logger.LogWarning("AI API key is not configured. Chat will not function.");
    }

    public async Task<string> GetAnswerAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            return _settings.ErrorMessages.ApiNotConfigured;

        try
        {
            var chatHistory = await BuildChatHistoryAsync(userMessage, cancellationToken);
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);

            return response.Content ?? _settings.ErrorMessages.EmptyResponse;
        }
        catch (Exception ex) when (ex.Message.Contains("429") || ex.InnerException?.Message.Contains("429") == true)
        {
            _logger.LogWarning("AI API rate limit hit.");
            return _settings.ErrorMessages.RateLimitHit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AI response failed. Type={ExType} | Message={Message} | Inner={Inner}",
                ex.GetType().Name, ex.Message, ex.InnerException?.Message);
            return _settings.ErrorMessages.GeneralError;
        }
    }

    public async IAsyncEnumerable<string> GetStreamingAnswerAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            yield return _settings.ErrorMessages.ApiNotConfigured;
            yield break;
        }

        // ── Phase 1: build prompt (DB call) ─────────────────────────
        // C# doesn't allow `yield return` inside a catch block, so we hold the result
        // in a local + sentinel string and inspect after the try/catch.
        ChatHistory? chatHistory = null;
        string? prefatalMessage = null;
        try
        {
            chatHistory = await BuildChatHistoryAsync(userMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI streaming failed during prompt assembly.");
            prefatalMessage = _settings.ErrorMessages.GeneralError;
        }
        if (prefatalMessage is not null)
        {
            yield return prefatalMessage;
            yield break;
        }

        // ── Phase 2: open the model stream ──────────────────────────
        IAsyncEnumerator<StreamingChatMessageContent>? enumerator = null;
        string? openErrorMessage = null;
        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            enumerator = chatService
                .GetStreamingChatMessageContentsAsync(chatHistory!, cancellationToken: cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI streaming failed to start.");
            openErrorMessage = _settings.ErrorMessages.GeneralError;
        }
        if (openErrorMessage is not null)
        {
            yield return openErrorMessage;
            yield break;
        }

        // ── Phase 3: drain tokens, mapping per-chunk exceptions to friendly text ──
        try
        {
            while (true)
            {
                bool hasNext;
                string? chunk = null;
                string? chunkErrorMessage = null;
                try
                {
                    hasNext = await enumerator!.MoveNextAsync();
                    chunk = hasNext ? enumerator.Current.Content : null;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.InnerException?.Message.Contains("429") == true)
                {
                    _logger.LogWarning("AI streaming rate limit hit.");
                    chunkErrorMessage = "\n\n" + _settings.ErrorMessages.RateLimitHit;
                    hasNext = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI streaming failed mid-stream.");
                    chunkErrorMessage = "\n\n" + _settings.ErrorMessages.GeneralError;
                    hasNext = false;
                }

                if (chunkErrorMessage is not null)
                {
                    yield return chunkErrorMessage;
                    yield break;
                }
                if (!hasNext) yield break;
                if (!string.IsNullOrEmpty(chunk))
                    yield return chunk;
            }
        }
        finally
        {
            if (enumerator is not null)
                await enumerator.DisposeAsync();
        }
    }

    private async Task<ChatHistory> BuildChatHistoryAsync(string userMessage, CancellationToken cancellationToken)
    {
        var knowledgeEntries = await _context.KnowledgeEntries
            .AsNoTracking()
            .Take(_settings.ContextLimit)
            .ToListAsync(cancellationToken);

        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine(_settings.SystemPrompt);
        contextBuilder.AppendLine("\n--- CONTEXT START ---");

        foreach (var entry in knowledgeEntries)
        {
            contextBuilder.AppendLine($"Topic: {entry.Topic}");
            contextBuilder.AppendLine($"Info: {entry.Content}");
            contextBuilder.AppendLine("-");
        }
        contextBuilder.AppendLine("--- CONTEXT END ---\n");

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(contextBuilder.ToString());
        chatHistory.AddUserMessage(userMessage);
        return chatHistory;
    }
}
