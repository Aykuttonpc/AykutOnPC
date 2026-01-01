using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

public class User
{
    public int Id { get; set; }
    
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty; // In real world this should be hashed
}
