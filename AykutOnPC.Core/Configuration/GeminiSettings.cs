namespace AykutOnPC.Core.Configuration;

public class GeminiSettings
{
    public const string SectionName = "GeminiSettings";

    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "llama-3.1-8b-instant";
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1";
}
