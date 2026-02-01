using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

public class Education
{
    public int Id { get; set; }

    [Required]
    public string Institution { get; set; } = string.Empty;

    [Required]
    public string Degree { get; set; } = string.Empty;

    public string? FieldOfStudy { get; set; }

    public DateTime StartDate { get; set; }
    
    public DateTime? EndDate { get; set; } // Null means "Present"

    public string? Description { get; set; }

    public string? Grade { get; set; }
}
