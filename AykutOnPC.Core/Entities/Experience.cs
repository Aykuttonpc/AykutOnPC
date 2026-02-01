using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

public class Experience
{
    public int Id { get; set; }

    [Required]
    public string Company { get; set; } = string.Empty;

    [Required]
    public string Position { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    
    public DateTime? EndDate { get; set; } // Null means "Present"

    public string Description { get; set; } = string.Empty;

    public string? CompanyUrl { get; set; }
}
