using System.ComponentModel.DataAnnotations;

namespace AykutOnPC.Core.Entities;

/// <summary>
/// Records a single page visit. Written server-side — no client JS required.
/// Bot traffic is filtered at the middleware level before this entity is persisted.
/// </summary>
public class PageView
{
    public long Id { get; set; }

    [Required, MaxLength(500)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Referrer { get; set; }

    /// <summary>Hashed IP (SHA-256 + daily salt) — GDPR compliant, no raw IP stored.</summary>
    [MaxLength(64)]
    public string HashedIp { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    /// <summary>Coarse device category derived from User-Agent: Desktop | Mobile | Tablet | Bot</summary>
    [MaxLength(20)]
    public string DeviceType { get; set; } = "Unknown";

    /// <summary>ISO 3166-1 alpha-2 country code resolved from IP via MaxMind GeoLite2 (optional).</summary>
    [MaxLength(2)]
    public string? CountryCode { get; set; }

    /// <summary>UTC timestamp of the visit.</summary>
    public DateTime VisitedAtUtc { get; set; } = DateTime.UtcNow;
}
