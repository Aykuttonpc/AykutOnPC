using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using AykutOnPC.Core.Configuration;
using Microsoft.Extensions.Options;

namespace AykutOnPC.Infrastructure.Services;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GitHubService> _logger;
    private readonly GitHubSettings _settings;
    private const string CacheKeyPrefix = "github_repos_";

    public GitHubService(HttpClient httpClient, IMemoryCache cache, ILogger<GitHubService> logger, IOptions<GitHubSettings> settings)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _settings = settings.Value;

        _httpClient.BaseAddress = new Uri(_settings.ApiUrl);

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);
        }

        if (!string.IsNullOrEmpty(_settings.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.Token);
        }
    }

    public async Task<IEnumerable<Build>> GetRepositoriesAsync(string username, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{username}";

        if (_cache.TryGetValue(cacheKey, out IEnumerable<Build>? cachedBuilds) && cachedBuilds is not null)
        {
            return cachedBuilds;
        }

        try
        {
            var repos = await _httpClient.GetFromJsonAsync<List<GitHubRepoDto>>(
                $"/users/{Uri.EscapeDataString(username)}/repos?sort=updated&per_page=100",
                cancellationToken);

            if (repos is null)
            {
                _cache.Set(cacheKey, Enumerable.Empty<Build>(), TimeSpan.FromMinutes(10));
                return Enumerable.Empty<Build>();
            }

            var builds = repos.Select(r => new Build
            {
                Title       = r.Name        ?? "Unnamed",
                Description = r.Description ?? "No description available.",
                TechStack   = r.Language    ?? "Unknown",
                RepoUrl     = r.HtmlUrl,
                LiveUrl     = r.Homepage,
                CreatedAt   = r.CreatedAt
            }).ToList();

            _cache.Set(cacheKey, (IEnumerable<Build>)builds, TimeSpan.FromMinutes(_settings.CacheDurationMinutes));
            return builds;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch GitHub repositories for user '{Username}'.", username);
            _cache.Set(cacheKey, Enumerable.Empty<Build>(), TimeSpan.FromHours(_settings.ErrorCacheDurationHours));
            return Enumerable.Empty<Build>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching GitHub repositories for user '{Username}'.", username);
            _cache.Set(cacheKey, Enumerable.Empty<Build>(), TimeSpan.FromMinutes(_settings.CacheDurationMinutes / 2));
            return Enumerable.Empty<Build>();
        }
    }

    // Internal DTO to deserialize GitHub JSON
    private class GitHubRepoDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string? Description { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("language")]
        public string? Language { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
