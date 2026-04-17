using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Anti-bot honeypot — must be left empty. Hidden in the form via off-screen positioning.
    /// Real users never see it; bots that auto-fill every input get silently rejected.
    /// </summary>
    public string? Website { get; set; }
}

