using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.DTOs;

// ─── Read-side ─────────────────────────────────────────────────────────

/// <summary>Single board payload — all Pbis (filtered server-side optionally) + all labels.</summary>
public class WorkspaceSnapshotDto
{
    public List<PbiDto> Pbis { get; set; } = new();
    public List<LabelDto> Labels { get; set; } = new();
}

public class PbiDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string State { get; set; } = "Backlog";
    public int Priority { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public List<int> LabelIds { get; set; } = new();
}

public class LabelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748b";
    public int SortOrder { get; set; }
}

// ─── Write-side ────────────────────────────────────────────────────────

public class CreatePbiDto
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(8000)]
    public string? Description { get; set; }

    /// <summary>Defaults to "Backlog" if missing.</summary>
    public string? State { get; set; }

    [Range(1, 3)]
    public int Priority { get; set; } = 2;

    public DateTime? DueDateUtc { get; set; }

    public List<int>? LabelIds { get; set; }
}

public class UpdatePbiDto
{
    [StringLength(300, MinimumLength = 1)]
    public string? Title { get; set; }

    [StringLength(8000)]
    public string? Description { get; set; }

    [Range(1, 3)]
    public int? Priority { get; set; }

    public DateTime? DueDateUtc { get; set; }

    /// <summary>If null, labels are left untouched. If empty list, all labels removed.</summary>
    public List<int>? LabelIds { get; set; }
}

/// <summary>
/// Move payload used by the drag/drop handler. Setting <see cref="State"/> moves the card
/// to a new column; <see cref="SortOrder"/> positions it within the (new) column. Sending
/// both means "drop here". Server stamps <c>CompletedAtUtc</c> when entering Done and clears
/// it when leaving Done.
/// </summary>
public class MovePbiDto
{
    [Required]
    public string State { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

public class CreateLabelDto
{
    [Required, StringLength(40, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(7, MinimumLength = 7)]
    public string Color { get; set; } = "#64748b";
}

public class UpdateLabelDto
{
    [StringLength(40, MinimumLength = 1)]
    public string? Name { get; set; }

    [StringLength(7, MinimumLength = 7)]
    public string? Color { get; set; }

    public int? SortOrder { get; set; }
}
