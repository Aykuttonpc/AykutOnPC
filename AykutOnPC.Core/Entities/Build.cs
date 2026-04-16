namespace AykutOnPC.Core.Entities;

/// <summary>
/// Represents a GitHub repository surfaced on the portfolio homepage.
/// Not persisted to the database — hydrated at runtime via IGitHubService.
/// </summary>
public class Build
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Primary language of the repository (e.g. "C#", "TypeScript").</summary>
    public string TechStack { get; set; } = string.Empty;

    public string? RepoUrl { get; set; }
    public string? LiveUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
