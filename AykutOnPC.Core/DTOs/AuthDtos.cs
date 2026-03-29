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
}

public class RegisterDto
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
