using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

/// <summary>
/// One card on a <see cref="Board"/>. Grid coordinates use Gridstack.js semantics
/// (12-column grid by default). Per-type settings live in <see cref="ConfigJson"/>
/// to keep the schema generic; list-style widgets (Checklist, Link) also have
/// <see cref="WidgetItem"/> rows; chart widgets read from <see cref="MetricEntry"/>.
/// </summary>
public class Widget
{
    public int Id { get; set; }

    public int BoardId { get; set; }
    public Board? Board { get; set; }

    /// <summary>Stored as string. See <see cref="WidgetType"/>.</summary>
    [Required, MaxLength(30)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Title { get; set; }

    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridW { get; set; } = 3;
    public int GridH { get; set; } = 3;

    /// <summary>
    /// jsonb column. Shape depends on <see cref="Type"/>; e.g. Counter stores
    /// { "value": 3, "label": "Aktif proje" }; Countdown stores { "targetUtc": "...", "label": "..." }.
    /// </summary>
    public string? ConfigJson { get; set; }

    public int SortOrder { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<WidgetItem> Items { get; set; } = new();
}

/// <summary>
/// Widget render type. String-backed so adding a new kind doesn't risk silent
/// renumbering. Keep names short and identifier-safe — they appear in the
/// Type column verbatim.
/// </summary>
public enum WidgetType
{
    // Base list/info widgets (F1)
    Checklist,
    Counter,
    Countdown,
    Progress,
    Note,
    Link,

    // Chart / analytics widgets (F2)
    Line,
    Bar,
    Heatmap,
    Donut,
    Kpi,
    Streak,

    // Sprint-board widget (F3)
    Burndown
}
