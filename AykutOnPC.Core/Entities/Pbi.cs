using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

/// <summary>
/// Product Backlog Item — a single card on the personal Kanban board at /Workspace.
/// One user, one board, four columns (Backlog → Todo → Doing → Done). State drives
/// the column; SortOrder positions the card within its column.
/// </summary>
public class Pbi
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Markdown allowed — rendered on the card detail panel only.</summary>
    [MaxLength(8000)]
    public string? Description { get; set; }

    /// <summary>One of <see cref="PbiState"/>, stored as string for forward compat.</summary>
    [Required, MaxLength(20)]
    public string State { get; set; } = nameof(PbiState.Backlog);

    /// <summary>1 = P1 high, 2 = P2 normal (default), 3 = P3 low. Drives the left strip color.</summary>
    public int Priority { get; set; } = 2;

    public DateTime? DueDateUtc { get; set; }

    /// <summary>Position within the card's current column. Updated by drag/drop reorder.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Server-stamped when State transitions to Done; cleared if reopened.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    public List<PbiLabel> PbiLabels { get; set; } = new();
}

public enum PbiState
{
    Backlog,
    Todo,
    Doing,
    Done
}
