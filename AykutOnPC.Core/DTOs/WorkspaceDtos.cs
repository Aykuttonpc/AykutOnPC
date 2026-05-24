using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.DTOs;

// ─── Read-side ─────────────────────────────────────────────────────────

/// <summary>Lightweight row for the board switcher dropdown.</summary>
public class BoardListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Standard";
    public int SortOrder { get; set; }
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>Full payload for /api/dashboard — board metadata + all widgets + items.</summary>
public class BoardDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Standard";
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public string? GoalText { get; set; }
    public string? RetroNotes { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public List<WidgetDto> Widgets { get; set; } = new();
}

public class WidgetDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridW { get; set; }
    public int GridH { get; set; }
    public string? ConfigJson { get; set; }
    public List<WidgetItemDto> Items { get; set; } = new();
}

public class WidgetItemDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool? IsDone { get; set; }
    public DateTime? DoneAtUtc { get; set; }
    public int SortOrder { get; set; }
}

public class MetricPointDto
{
    public DateTime RecordedAtUtc { get; set; }
    public double Value { get; set; }
    public string? Note { get; set; }
}

// ─── Write-side ────────────────────────────────────────────────────────

public class CreateBoardDto
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"Standard" or "Sprint". Defaults to Standard if missing/invalid.</summary>
    public string? Kind { get; set; }

    // Sprint-only — required when Kind == Sprint
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    [StringLength(500)]
    public string? GoalText { get; set; }
}

public class UpdateBoardDto
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    [StringLength(500)]
    public string? GoalText { get; set; }

    [StringLength(5000)]
    public string? RetroNotes { get; set; }
}

public class CreateWidgetDto
{
    [Required, StringLength(30)]
    public string Type { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Title { get; set; }

    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridW { get; set; } = 3;
    public int GridH { get; set; } = 3;

    public string? ConfigJson { get; set; }
}

public class UpdateWidgetDto
{
    [StringLength(200)]
    public string? Title { get; set; }

    public string? ConfigJson { get; set; }
}

/// <summary>One row of the batch layout payload sent by Gridstack after drag/resize.</summary>
public class WidgetLayoutDto
{
    public int Id { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridW { get; set; }
    public int GridH { get; set; }
}

public class AddWidgetItemDto
{
    [Required, StringLength(500, MinimumLength = 1)]
    public string Label { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Value { get; set; }

    public bool? IsDone { get; set; }
}

public class UpdateWidgetItemDto
{
    [StringLength(500, MinimumLength = 1)]
    public string? Label { get; set; }

    [StringLength(2000)]
    public string? Value { get; set; }

    public bool? IsDone { get; set; }
}

public class WidgetItemOrderDto
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
}

public class AddMetricEntryDto
{
    public DateTime? RecordedAtUtc { get; set; }
    public double Value { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
