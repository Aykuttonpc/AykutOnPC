using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

public class Profile
{
    public int Id { get; set; }

    public string? FullName { get; set; } = "Aykut";

    public string? Title { get; set; }

    public string? Bio { get; set; }

    public string? ProfilePictureUrl { get; set; }

    // Social Links
    public string? GitHubUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? Email { get; set; }
}
