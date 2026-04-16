using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(contextBuilder.ToString());
            chatHistory.AddUserMessage(userMessage);

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
}
