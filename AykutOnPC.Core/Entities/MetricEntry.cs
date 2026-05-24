using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

/// <summary>
/// Time-series numeric data point attached to a chart widget (Line/Bar/Heatmap/etc.).
/// Append-only — historical entries are never mutated, only added or deleted.
/// Written manually via the widget UI or automatically when a linked checklist
/// item is toggled done (F4 auto-feed).
/// </summary>
public class MetricEntry
{
    public long Id { get; set; }

    public int WidgetId { get; set; }
    public Widget? Widget { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public double Value { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
