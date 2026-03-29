namespace AykutOnPC.Infrastructure.HttpHandlers;

/// <summary>
/// Interceptor to redirect Semantic Kernel (OpenAI) calls to any OpenAI-compatible target API (e.g. Groq) transparently.
/// </summary>
public class OpenAIToTargetHandler : DelegatingHandler
{
    private readonly string _targetEndpoint;

    public OpenAIToTargetHandler(string targetEndpoint)
    {
        _targetEndpoint = targetEndpoint.TrimEnd('/');
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri != null && request.RequestUri.Host == "api.openai.com")
        {
            // Replaces "https://api.openai.com/v1" with the target environment endpoint
            var newUrl = request.RequestUri.ToString().Replace("https://api.openai.com/v1", _targetEndpoint);
            request.RequestUri = new Uri(newUrl);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
