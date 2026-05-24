using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

/// <summary>
/// A workspace board — Azure DevOps-style dashboard with a grid of widgets.
/// Single-user today (UserId still tracked for future multi-tenancy).
/// Two kinds:
///   • <see cref="BoardKind.Standard"/> — long-lived area ("İş", "Spor", "Öğrenme")
///   • <see cref="BoardKind.Sprint"/>   — time-boxed period with goal/retro and an auto-attached burndown widget
/// </summary>
public class Board
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stored as string for forward compat (same pattern as <see cref="ChatErrorKind"/>).</summary>
    [Required, MaxLength(20)]
    public string Kind { get; set; } = nameof(BoardKind.Standard);

    /// <summary>Position in the board switcher / sidebar list. Lower = first.</summary>
    public int SortOrder { get; set; }

    // ── Sprint-only fields (null for Standard boards) ─────────────────
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    [MaxLength(500)]
    public string? GoalText { get; set; }

    [MaxLength(5000)]
    public string? RetroNotes { get; set; }

    /// <summary>Soft delete / archive. Sprint boards are typically archived after retro.</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Widget> Widgets { get; set; } = new();
}

public enum BoardKind
{
    Standard,
    Sprint
}
