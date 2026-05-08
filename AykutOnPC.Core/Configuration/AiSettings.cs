namespace AykutOnPC.Core.Configuration;

/// <summary>
/// Configuration for the AI chat completion backend.
/// Supports any OpenAI-compatible endpoint (Gemini, Groq, OpenAI, Azure OpenAI, etc.).
/// Section name kept as "GeminiSettings" for backwards compatibility with existing env vars.
/// </summary>
public class AiSettings
{
    public const string SectionName = "GeminiSettings";

    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gemini-2.5-flash";
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai";
    public string SystemPrompt { get; set; } = "You are an AI assistant for a portfolio website.";
    public int ContextLimit { get; set; } = 50;

    /// <summary>
    /// When true, the chat path retrieves the top-K KB entries by embedding similarity
    /// (RAG). When false or when retrieval returns nothing, falls back to the legacy
    /// "stuff every entry into the prompt" path. Feature flag for fast rollback.
    /// </summary>
    public bool UseRagRetrieval { get; set; } = true;

    /// <summary>How many KB entries the RAG retrieval includes per chat turn.</summary>
    public int RetrievalTopK { get; set; } = 5;

    /// <summary>
    /// How many prior turns of the same conversation to replay to the model. 0 disables memory.
    /// Each pair (user + bot) counts as 2 messages — keep small to control token cost.
    /// </summary>
    public int ConversationMemoryTurns { get; set; } = 6;

    /// <summary>Static suggestion chips shown in the welcome state and after empty/safety/validation outcomes.</summary>
    public List<SuggestionChip> Suggestions { get; set; } = new();

    public AiErrorMessages ErrorMessages { get; set; } = new();
}

public class SuggestionChip
{
    public string Label { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

public class AiErrorMessages
{
    public string ApiNotConfigured { get; set; } = "AI servisi şu an yapılandırılmamış.";
    public string RateLimitHit     { get; set; } = "Kısa bir süreliğine çok meşgulüm. Bir-iki dakika sonra tekrar dene.";
    public string GeneralError     { get; set; } = "Beklenmedik bir hata oldu. Lütfen tekrar dene.";
    public string EmptyResponse    { get; set; } = "Bu sorunu tam anlayamadım. Aşağıdaki başlıklardan birini seçebilir veya farklı şekilde sorabilirsin.";
    public string SafetyBlocked    { get; set; } = "Bu konuda yardımcı olamam. Aykut'un projeleri, yetkinlikleri veya iletişim bilgileri hakkında soru sorabilirsin.";
    public string NetworkError     { get; set; } = "Şu an bağlantım zayıf. Lütfen bir kaç saniye sonra tekrar dene.";
    public string ValidationError  { get; set; } = "Mesajını anlayamadım. Daha açık bir şekilde sorabilir misin?";
}
