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

public class GeminiService : IAIService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GeminiService> _logger;
    private readonly Kernel _kernel;
    private readonly GeminiSettings _settings;

    public GeminiService(
        AppDbContext context,
        Kernel kernel,
        IOptions<GeminiSettings> geminiOptions,
        ILogger<GeminiService> logger)
    {
        _context = context;
        _kernel = kernel;
        _logger = logger;
        _settings = geminiOptions.Value;

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("Gemini/Groq API Key is not configured. AI chat will not function.");
        }
    }

    public async Task<string> GetAnswerAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return "AI service is not configured. Please set the API key.";
        }

        try
        {
            // 1. Build RAG context from knowledge base
            var knowledgeEntries = await _context.KnowledgeEntries
                .AsNoTracking()
                .Take(50) 
                .ToListAsync(cancellationToken);

            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("You are an AI assistant for a portfolio website called 'AykutOnPC'.");
            contextBuilder.AppendLine("Answer questions based strictly on the following context. If the answer is not in the context, be polite and say you don't know, but you can take a note.");
            contextBuilder.AppendLine("Keep answers concise, professional, yet friendly. Provide your answers in Turkish unless the user speaks English.");
            contextBuilder.AppendLine("\n--- CONTEXT START ---");

            foreach (var entry in knowledgeEntries)
            {
                contextBuilder.AppendLine($"Topic: {entry.Topic}");
                contextBuilder.AppendLine($"Info: {entry.Content}");
                contextBuilder.AppendLine("-");
            }
            contextBuilder.AppendLine("--- CONTEXT END ---\n");

            // 2. Use Semantic Kernel Chat Completion natively
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(contextBuilder.ToString());
            chatHistory.AddUserMessage(userMessage);

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);

            return response.Content ?? "I understood your question, but I have no words to express the answer.";
        }
        catch (Exception ex) when (ex.Message.Contains("429"))
        {
            _logger.LogWarning("AI API rate limit hit.");
            return "Kısa bir süre için çok meşgulüm (Rate Limit). Lütfen birazdan tekrar dene.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting AI response: {Message}", ex.Message);
            return "Kısa süreli bir iletişim sorunu yaşıyorum. Lütfen daha sonra tekrar dene.";
        }
    }
}
