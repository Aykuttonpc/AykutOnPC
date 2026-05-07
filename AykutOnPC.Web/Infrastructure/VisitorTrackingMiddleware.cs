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
public sealed class VisitorTrackingMiddleware(
    RequestDelegate next,
    ILogger<VisitorTrackingMiddleware> logger,
    IServiceScopeFactory scopeFactory)
{
    // Paths we never want to track. Three buckets:
    //   1. Operational endpoints (api, health, infra)
    //   2. Static assets (handled by extension check below too, but cheap to short-circuit here)
    //   3. Admin / authenticated areas — none of these represent real visitor interest in the
    //      portfolio content. Without this filter the dashboard counts the owner's own admin
    //      sessions as "visitors", which dwarfs real public traffic.
    private static readonly string[] ExcludedPrefixes =
    [
        // 1. Operational
        "/api/", "/health", "/_", "/favicon",
        // 2. Static asset folders
        "/lib/", "/css/", "/js/", "/fonts/", "/images/",
        // 3. Admin & auth surfaces (non-public)
        "/admin", "/account", "/profile", "/knowledgebase",
        "/specs", "/experience", "/education", "/chatlogs"
    ];

    // Exact (non-prefix) paths we drop as well — bot bait files etc.
    private static readonly HashSet<string> ExcludedExactPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/robots.txt", "/sitemap.xml", "/ads.txt", "/.well-known"
    };

    // Coarse bot detector — covers the vast majority of crawler traffic
    private static readonly Regex BotPattern = new(
        @"(bot|crawler|spider|slurp|baiduspider|googlebot|bingbot|yandex|duckduck|facebot|ia_archiver|scrapy|wget|curl|python-requests)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Daily salt rotates at midnight UTC — makes IPs untraceable across days
    private static string DailySalt => DateTime.UtcNow.ToString("yyyyMMdd");

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);   // Process request first — capture status codes

        // Only track successful HTML responses for GET requests
        if (context.Request.Method != HttpMethods.Get) return;
        if (context.Response.StatusCode is 301 or 302 or 304 or 404 or >= 500) return;
        if (IsExcluded(context.Request.Path)) return;

        // Authenticated admin users are the site owner — exclude their traffic entirely so the
        // dashboard reflects real visitors, not "I clicked Refresh 40 times" sessions. This
        // catches any future admin pages we forget to add to ExcludedPrefixes too.
        if (context.User.Identity?.IsAuthenticated == true) return;

        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (IsBot(userAgent)) return;

        // Snapshot every HttpContext-derived value BEFORE the fire-and-forget.
        // Once the response completes, ASP.NET recycles HttpContext (and its request
        // services scope) for the next request — accessing Request.Path/Headers/etc
        // inside Task.Run race-conditions with the next request and silently fails.
        var path    = context.Request.Path.ToString().ToLowerInvariant();
        var referer = TruncateSafe(context.Request.Headers.Referer.ToString(), 512);
        var ip      = GetClientIp(context);

        // Fire-and-forget — analytics must not slow down the response.
        // We use IServiceScopeFactory (singleton, root-bound) instead of the request-
        // scoped IServiceProvider, because the latter is disposed when the response
        // completes and cannot create child scopes.
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var analyticsService = scope.ServiceProvider.GetRequiredService<IVisitorAnalyticsService>();

                var pageView = new PageView
                {
                    Path        = path,
                    Referrer    = referer,
                    HashedIp    = HashIp(ip),
                    UserAgent   = TruncateSafe(userAgent, 512),
                    DeviceType  = DetectDevice(userAgent),
                    VisitedAtUtc = DateTime.UtcNow
                };

                await analyticsService.RecordAsync(pageView);
            }
            catch (Exception ex)
            {
                // Visible in production logs — silent failure used to hide real bugs.
                logger.LogWarning(ex, "VisitorTracking background task failed for path {Path}", path);
            }
        });
    }

    private static bool IsExcluded(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (ExcludedExactPaths.Contains(value)) return true;
        foreach (var prefix in ExcludedPrefixes)
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        // Exclude static file extensions
        return value.Contains('.') &&
               (value.EndsWith(".css") || value.EndsWith(".js") || value.EndsWith(".png") ||
                value.EndsWith(".jpg") || value.EndsWith(".svg") || value.EndsWith(".woff2") ||
                value.EndsWith(".ico") || value.EndsWith(".webp") || value.EndsWith(".map") ||
                value.EndsWith(".txt") || value.EndsWith(".xml"));
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
