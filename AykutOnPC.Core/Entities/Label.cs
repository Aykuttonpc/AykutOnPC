using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

/// <summary>
/// Tag attached to one or more <see cref="Pbi"/>s. Owner-managed (CRUD'd from the UI).
/// Color stored as hex (#rrggbb) and used as the chip background tint everywhere a label
/// appears: card chip, filter chip, label management modal.
/// </summary>
public class Label
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex like "#3b82f6". Validation enforces leading # + 6 hex chars at the service layer.</summary>
    [Required, MaxLength(7)]
    public string Color { get; set; } = "#64748b";

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<PbiLabel> PbiLabels { get; set; } = new();
}

/// <summary>M:N junction between <see cref="Pbi"/> and <see cref="Label"/>.</summary>
public class PbiLabel
{
    public int PbiId { get; set; }
    public Pbi? Pbi { get; set; }

    public int LabelId { get; set; }
    public Label? Label { get; set; }
}
