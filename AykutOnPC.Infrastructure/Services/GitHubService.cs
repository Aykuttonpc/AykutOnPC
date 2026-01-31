using System.Net.Http.Json;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;

namespace AykutOnPC.Infrastructure.Services;

public class GitHubService(HttpClient httpClient) : IGitHubService
{
    public async Task<IEnumerable<Build>> GetRepositoriesAsync(string username)
    {
        try
        {
            // Add User-Agent as required by GitHub API
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AykutOnPC-App");

            var repos = await httpClient.GetFromJsonAsync<List<GitHubRepoDto>>($"https://api.github.com/users/{username}/repos?sort=updated&per_page=100");
            
            if (repos == null) return Enumerable.Empty<Build>();

            return repos.Select(r => new Build(
                r.Name ?? "Unnamed", 
                r.Description ?? "No description available.", 
                r.Language ?? "Unknown")
            {
                RepoUrl = r.HtmlUrl,
                CreatedAt = r.CreatedAt,
                LiveUrl = r.Homepage
            });
        }
        catch
        {
            // Fallback to empty list or handled error in production
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
