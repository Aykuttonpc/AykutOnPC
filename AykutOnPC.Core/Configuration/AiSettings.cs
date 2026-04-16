namespace AykutOnPC.Core.Configuration;

/// <summary>
/// Configuration for the AI chat completion backend.
/// Supports any OpenAI-compatible endpoint (Groq, OpenAI, Azure OpenAI, etc.).
/// Renamed from GeminiSettings — the app uses Groq/llama, not Gemini.
/// </summary>
public class AiSettings
{
    public const string SectionName = "GeminiSettings"; // appsettings.json key kept for backwards compat

    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "llama-3.3-70b-versatile";
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1";
    public string SystemPrompt { get; set; } = "You are an AI assistant for a portfolio website.";
    public int ContextLimit { get; set; } = 50;
    public AiErrorMessages ErrorMessages { get; set; } = new();
}

public class AiErrorMessages
{
    public string ApiNotConfigured { get; set; } = "AI service is not configured.";
    public string RateLimitHit { get; set; } = "AI API rate limit hit. Please try again shortly.";
    public string GeneralError { get; set; } = "A communication error occurred. Please try again later.";
    public string EmptyResponse { get; set; } = "I understood your question but could not generate a response.";
}
