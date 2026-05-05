using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.DTOs;

public class CreateEducationDto
{
    [Required(ErrorMessage = "Institution is required.")]
    [StringLength(200)]
    public string Institution { get; set; } = string.Empty;

    [Required(ErrorMessage = "Degree is required.")]
    [StringLength(200)]
    public string Degree { get; set; } = string.Empty;

    [StringLength(200)]
    public string? FieldOfStudy { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(20)]
    public string? Grade { get; set; }
}

public class UpdateEducationDto : CreateEducationDto
{
    public int Id { get; set; }
}

public class CreateExperienceDto
{
    [Required(ErrorMessage = "Company is required.")]
    [StringLength(200)]
    public string Company { get; set; } = string.Empty;

    [Required(ErrorMessage = "Position is required.")]
    [StringLength(200)]
    public string Position { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Url(ErrorMessage = "Invalid URL format.")]
    public string? CompanyUrl { get; set; }
}

public class UpdateExperienceDto : CreateExperienceDto
{
    public int Id { get; set; }
}

public class CreateSpecDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Range(0, 100, ErrorMessage = "Proficiency must be between 0 and 100.")]
    public int Proficiency { get; set; }

    public string? IconClass { get; set; }
}

public class UpdateSpecDto : CreateSpecDto
{
    public int Id { get; set; }
}

public class CreateKnowledgeEntryDto
{
    [Required(ErrorMessage = "Topic is required.")]
    [StringLength(200)]
    public string Topic { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content is required.")]
    public string Content { get; set; } = string.Empty;

    [StringLength(500)]
    public string Keywords { get; set; } = string.Empty;
}

public class UpdateKnowledgeEntryDto : CreateKnowledgeEntryDto
{
    public int Id { get; set; }
}

public class ChatRequestDto
{
    [Required(ErrorMessage = "Message cannot be empty.")]
    [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional. When supplied, the server links this turn to a prior conversation for memory.</summary>
    public Guid? ConversationId { get; set; }
}
