using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AykutOnPC.Infrastructure.Services;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GitHubService> _logger;
    private const string CacheKeyPrefix = "github_repos_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public GitHubService(HttpClient httpClient, IMemoryCache cache, ILogger<GitHubService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        // Set User-Agent (Required by GitHub API)
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AykutOnPC-App/1.0");
        }

        // Add Personal Access Token if provided to increase rate limit (from 60 to 5000 requests/hr)
        var token = configuration["GITHUB_TOKEN"];
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
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
                $"https://api.github.com/users/{Uri.EscapeDataString(username)}/repos?sort=updated&per_page=100",
                cancellationToken);

            if (repos is null)
            {
                _cache.Set(cacheKey, Enumerable.Empty<Build>(), TimeSpan.FromMinutes(10));
                return Enumerable.Empty<Build>();
            }

            var builds = repos.Select(r => new Build(
                r.Name ?? "Unnamed",
                r.Description ?? "No description available.",
                r.Language ?? "Unknown")
            {
                RepoUrl = r.HtmlUrl,
                CreatedAt = r.CreatedAt,
                LiveUrl = r.Homepage
            }).ToList();

            _cache.Set(cacheKey, (IEnumerable<Build>)builds, CacheDuration);
            return builds;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch GitHub repositories for user '{Username}'. (Rate Limited or Network issue)", username);
            // Cache the failure for 1 hour to avoid hammering the API
            _cache.Set(cacheKey, Enumerable.Empty<Build>(), TimeSpan.FromHours(1));
            return Enumerable.Empty<Build>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching GitHub repositories for user '{Username}'.", username);
            _cache.Set(cacheKey, Enumerable.Empty<Build>(), TimeSpan.FromMinutes(5));
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
