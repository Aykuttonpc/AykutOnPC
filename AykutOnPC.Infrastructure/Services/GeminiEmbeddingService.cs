using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AykutOnPC.Infrastructure.Services;

/// <summary>
/// Calls Gemini's native :embedContent endpoint. Native (not OpenAI-compat) because
/// the OpenAI-compat embed shim returns dimensions we can't easily pin to 768 and
/// adds a layer that hides Gemini-specific errors.
///
/// Retry policy: transient (5xx, 429, network) are retried with exponential backoff
/// up to <c>MaxRetries</c>. Non-transient (400, 403) fail fast.
/// </summary>
public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(
        HttpClient http,
        IOptions<EmbeddingSettings> options,
        ILogger<GeminiEmbeddingService> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<float[]?> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("EmbeddingSettings.ApiKey is empty — embeddings disabled.");
            return null;
        }

        // Native shape: POST /v1beta/models/{model}:embedContent?key=API_KEY
        // Body:  { "model": "models/{model}", "content": { "parts": [{ "text": "..." }] } }
        var url = $"models/{_settings.ModelId}:embedContent?key={_settings.ApiKey}";
        var body = new EmbedRequest
        {
            Model = $"models/{_settings.ModelId}",
            Content = new EmbedContent
            {
                Parts = [new EmbedPart { Text = text }]
            }
        };

        for (var attempt = 1; attempt <= _settings.MaxRetries; attempt++)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(url, body, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: cancellationToken);
                    return payload?.Embedding?.Values;
                }

                if (!IsTransient(response.StatusCode) || attempt == _settings.MaxRetries)
                {
                    var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "Embedding call failed (status={Status} attempt={Attempt}): {Body}",
                        response.StatusCode, attempt, Truncate(errBody, 400));
                    return null;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (attempt == _settings.MaxRetries)
                {
                    _logger.LogWarning(ex, "Embedding call threw on attempt {Attempt}", attempt);
                    return null;
                }
            }

            // Exponential backoff: 200ms, 400ms, 800ms, …
            var delayMs = (int)(200 * Math.Pow(2, attempt - 1));
            await Task.Delay(delayMs, cancellationToken);
        }

        return null;
    }

    public async Task<IReadOnlyList<float[]?>> GenerateBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = new float[]?[texts.Count];
        for (var i = 0; i < texts.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;
            results[i] = await GenerateAsync(texts[i], cancellationToken);
            if (i < texts.Count - 1 && _settings.BatchDelayMs > 0)
                await Task.Delay(_settings.BatchDelayMs, cancellationToken);
        }
        return results;
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests ||
        status == HttpStatusCode.RequestTimeout ||
        (int)status >= 500;

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    // Wire types kept private — not domain concepts, just JSON shape.
    private sealed class EmbedRequest
    {
        [JsonPropertyName("model")]   public string Model   { get; set; } = string.Empty;
        [JsonPropertyName("content")] public EmbedContent Content { get; set; } = new();
    }
    private sealed class EmbedContent
    {
        [JsonPropertyName("parts")] public List<EmbedPart> Parts { get; set; } = new();
    }
    private sealed class EmbedPart
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }
    private sealed class EmbedResponse
    {
        [JsonPropertyName("embedding")] public EmbedValue? Embedding { get; set; }
    }
    private sealed class EmbedValue
    {
        [JsonPropertyName("values")] public float[] Values { get; set; } = Array.Empty<float>();
    }
}
