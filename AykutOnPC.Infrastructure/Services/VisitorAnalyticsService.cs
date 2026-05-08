using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AykutOnPC.Infrastructure.Services;

public sealed class VisitorAnalyticsService(
    AppDbContext db,
    ILogger<VisitorAnalyticsService> logger) : IVisitorAnalyticsService
{
    public async Task RecordAsync(PageView pageView, CancellationToken ct = default)
    {
        try
        {
            db.PageViews.Add(pageView);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Analytics failure must NEVER crash the main request
            logger.LogWarning(ex, "Failed to record PageView for path {Path}", pageView.Path);
        }
    }

    public async Task<VisitorSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var now      = DateTime.UtcNow;
        var todayUtc = now.Date;
        var weekAgo  = todayUtc.AddDays(-6);
        var monthAgo = todayUtc.AddDays(-29);

        var totalToday = await db.PageViews
            .CountAsync(p => p.VisitedAtUtc >= todayUtc, ct);

        var totalWeek = await db.PageViews
            .CountAsync(p => p.VisitedAtUtc >= weekAgo, ct);

        var totalMonth = await db.PageViews
            .CountAsync(p => p.VisitedAtUtc >= monthAgo, ct);

        // Unique visitor count uses COALESCE(VisitorId, HashedIp): same person across
        // mobile/WiFi/IPv6 rotations carries the same VisitorId UUID (set client-side).
        // Falls back to HashedIp for first page load (cookie not yet round-tripped) or
        // JS-disabled clients. Translates to:
        //   COUNT(DISTINCT COALESCE("VisitorId"::text, "HashedIp"))
        var uniqueToday = await db.PageViews
            .Where(p => p.VisitedAtUtc >= todayUtc)
            .Select(p => p.VisitorId.HasValue ? p.VisitorId.Value.ToString() : p.HashedIp)
            .Distinct()
            .CountAsync(ct);

        var topPages = await db.PageViews
            .Where(p => p.VisitedAtUtc >= monthAgo)
            .GroupBy(p => p.Path)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new TopPageDto(g.Key, g.Count()))
            .ToListAsync(ct);

        var deviceRaw = await db.PageViews
            .Where(p => p.VisitedAtUtc >= monthAgo)
            .GroupBy(p => p.DeviceType)
            .Select(g => new { DeviceType = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalDevices = deviceRaw.Sum(d => d.Count);
        var deviceBreakdown = deviceRaw
            .Select(d => new DeviceBreakdownDto(
                d.DeviceType,
                d.Count,
                totalDevices > 0 ? Math.Round((double)d.Count / totalDevices * 100, 1) : 0))
            .OrderByDescending(d => d.Count)
            .ToList();

        // Daily trend: last 30 days. UniqueVisitors uses the same COALESCE strategy
        // as uniqueToday above so the daily series and the "today" pill agree.
        var dailyRaw = await db.PageViews
            .Where(p => p.VisitedAtUtc >= monthAgo)
            .GroupBy(p => p.VisitedAtUtc.Date)
            .Select(g => new
            {
                Date            = g.Key,
                Visits          = g.Count(),
                UniqueVisitors  = g.Select(p => p.VisitorId.HasValue ? p.VisitorId.Value.ToString() : p.HashedIp)
                                   .Distinct()
                                   .Count()
            })
            .OrderBy(g => g.Date)
            .ToListAsync(ct);

        var dailyTrend = dailyRaw
            .Select(d => new DailyVisitDto(DateOnly.FromDateTime(d.Date), d.Visits, d.UniqueVisitors))
            .ToList();

        return new VisitorSummaryDto(
            totalToday,
            totalWeek,
            totalMonth,
            uniqueToday,
            topPages,
            deviceBreakdown,
            dailyTrend);
    }
}
