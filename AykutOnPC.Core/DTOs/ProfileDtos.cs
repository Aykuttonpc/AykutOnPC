using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.DTOs;

public class UpdateProfileDto
{
    public int Id { get; set; }

    [StringLength(100)]
    public string? FullName { get; set; }

    [StringLength(100)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }

    [Url(ErrorMessage = "Invalid URL format.")]
    public string? ProfilePictureUrl { get; set; }

    [Url(ErrorMessage = "Invalid URL format.")]
    public string? GitHubUrl { get; set; }

    [Url(ErrorMessage = "Invalid URL format.")]
    public string? LinkedInUrl { get; set; }

    [Url(ErrorMessage = "Invalid URL format.")]
    public string? TwitterUrl { get; set; }

    [Url(ErrorMessage = "Invalid URL format.")]
    public string? InstagramUrl { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string? Email { get; set; }
}
