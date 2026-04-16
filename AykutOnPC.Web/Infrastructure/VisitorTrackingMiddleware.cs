using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AykutOnPC.Web.Infrastructure;

/// <summary>
/// Intercepts every HTTP request and records a PageView asynchronously.
/// Design decisions:
///   - Fire-and-forget via Task.Run to add ZERO latency to request pipeline.
///   - IP is SHA-256 hashed with a daily rotating salt — GDPR compliant.
///   - Bot traffic filtered by User-Agent before hitting the DB.
///   - Only tracks HTML page requests (excludes /api, /health, static assets).
/// </summary>
public sealed class VisitorTrackingMiddleware(RequestDelegate next, ILogger<VisitorTrackingMiddleware> logger)
{
    // Paths we never want to track
    private static readonly string[] ExcludedPrefixes =
        ["/api/", "/health", "/_", "/favicon", "/lib/", "/css/", "/js/", "/fonts/", "/images/"];

    // Coarse bot detector — covers the vast majority of crawler traffic
    private static readonly Regex BotPattern = new(
        @"(bot|crawler|spider|slurp|baiduspider|googlebot|bingbot|yandex|duckduck|facebot|ia_archiver|scrapy|wget|curl|python-requests)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Daily salt rotates at midnight UTC — makes IPs untraceable across days
    private static string DailySalt => DateTime.UtcNow.ToString("yyyyMMdd");

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        await next(context);   // Process request first — capture status codes

        // Only track successful HTML responses for GET requests
        if (context.Request.Method != HttpMethods.Get) return;
        if (context.Response.StatusCode is 301 or 302 or 304 or 404 or >= 500) return;
        if (IsExcluded(context.Request.Path)) return;

        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (IsBot(userAgent)) return;

        // Fire-and-forget — analytics must not slow down the response
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var analyticsService = scope.ServiceProvider.GetRequiredService<IVisitorAnalyticsService>();

                var pageView = new PageView
                {
                    Path        = context.Request.Path.ToString().ToLowerInvariant(),
                    Referrer    = TruncateSafe(context.Request.Headers.Referer.ToString(), 512),
                    HashedIp    = HashIp(GetClientIp(context)),
                    UserAgent   = TruncateSafe(userAgent, 512),
                    DeviceType  = DetectDevice(userAgent),
                    VisitedAtUtc = DateTime.UtcNow
                };

                await analyticsService.RecordAsync(pageView);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "VisitorTracking background task failed silently.");
            }
        });
    }

    private static bool IsExcluded(PathString path)
    {
        var value = path.Value ?? string.Empty;
        foreach (var prefix in ExcludedPrefixes)
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        // Exclude static file extensions
        return value.Contains('.') &&
               (value.EndsWith(".css") || value.EndsWith(".js") || value.EndsWith(".png") ||
                value.EndsWith(".jpg") || value.EndsWith(".svg") || value.EndsWith(".woff2") ||
                value.EndsWith(".ico") || value.EndsWith(".webp") || value.EndsWith(".map"));
    }

    private static bool IsBot(string userAgent) =>
        string.IsNullOrWhiteSpace(userAgent) || BotPattern.IsMatch(userAgent);

    private static string GetClientIp(HttpContext context)
    {
        // Respect X-Forwarded-For set by Nginx (ForwardedHeaders middleware already validated it)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string HashIp(string ip)
    {
        var raw = Encoding.UTF8.GetBytes($"{ip}:{DailySalt}");
        var hash = SHA256.HashData(raw);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string DetectDevice(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";
        if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            return "Mobile";
        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
            return "Tablet";
        return "Desktop";
    }

    private static string? TruncateSafe(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? null : value[..Math.Min(value.Length, maxLength)];
}
