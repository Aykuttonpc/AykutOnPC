namespace AykutOnPC.Core.Configuration;

/// <summary>
/// Configuration for the embedding model used by RAG retrieval. Defaults target
/// Gemini gemini-embedding-001 forced to 768 dim via outputDimensionality (the
/// model's native default is 3072). DB column is fixed at vector(768), so
/// changing <see cref="Dimensions"/> requires a migration.
/// </summary>
public class EmbeddingSettings
{
    public const string SectionName = "EmbeddingSettings";

    /// <summary>API key — falls back to AiSettings.ApiKey (GEMINI_API_KEY) if blank.</summary>
    public string ApiKey { get; set; } = string.Empty;

    // Google retired `text-embedding-004` from v1beta in 2026 — replaced with
    // `gemini-embedding-001` (3072 dim default, but supports outputDimensionality
    // to match any schema width). Old name returns 404 NOT_FOUND.
    public string ModelId { get; set; } = "gemini-embedding-001";

    /// <summary>Native Gemini endpoint (NOT the OpenAI-compat one — embed body shape differs).</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Must match the DB column dimension. Changing this needs a migration.</summary>
    public int Dimensions { get; set; } = 768;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Backfill / batch pacing — request spacing in milliseconds (rate-limit safety).</summary>
    public int BatchDelayMs { get; set; } = 100;

    /// <summary>How many transient retry attempts before giving up on a single embed call.</summary>
    public int MaxRetries { get; set; } = 3;
}
