using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

/// <summary>
/// Generic content row for list-style widgets:
///   • Checklist  — Label + IsDone (+ DoneAtUtc when toggled true)
///   • Link       — Label + Value (URL)
///   • Other types may use it for free-form bullets via <see cref="MetaJson"/>
/// </summary>
public class WidgetItem
{
    public int Id { get; set; }

    public int WidgetId { get; set; }
    public Widget? Widget { get; set; }

    [Required, MaxLength(500)]
    public string Label { get; set; } = string.Empty;

    /// <summary>URL for Link widgets, free-form value otherwise.</summary>
    [MaxLength(2000)]
    public string? Value { get; set; }

    /// <summary>Null for non-checklist widgets; true/false for checklists.</summary>
    public bool? IsDone { get; set; }

    /// <summary>Server-set when <see cref="IsDone"/> transitions to true. Drives streak/heatmap charts.</summary>
    public DateTime? DoneAtUtc { get; set; }

    public int SortOrder { get; set; }

    /// <summary>jsonb. Optional per-item metadata (icon, color, tag, etc.).</summary>
    public string? MetaJson { get; set; }
}
