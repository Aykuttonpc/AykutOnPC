namespace AykutOnPC.Core.Entities;

public class Build(string title, string description, string techStack)
{
    public int Id { get; set; }
    public string Title { get; set; } = title;
    public string Description { get; set; } = description;
    public string TechStack { get; set; } = techStack; // Shared as comma separated or JSON
    public string? RepoUrl { get; set; }
    public string? LiveUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
