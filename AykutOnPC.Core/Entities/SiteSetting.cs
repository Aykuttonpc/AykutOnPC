using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

public class SiteSetting
{
    public int Id { get; set; }

    [Required]
    public string Key { get; set; } = string.Empty; // e.g. "GitHubUsername", "HeroTitle"

    public string Value { get; set; } = string.Empty; // e.g. "Aykuttonpc"

    public string Description { get; set; } = string.Empty; // Helper text for Admin
}
