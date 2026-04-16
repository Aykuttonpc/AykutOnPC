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

        var uniqueToday = await db.PageViews
            .Where(p => p.VisitedAtUtc >= todayUtc)
            .Select(p => p.HashedIp)
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

        // Daily trend: last 30 days
        var dailyRaw = await db.PageViews
            .Where(p => p.VisitedAtUtc >= monthAgo)
            .GroupBy(p => p.VisitedAtUtc.Date)
            .Select(g => new
            {
                Date            = g.Key,
                Visits          = g.Count(),
                UniqueVisitors  = g.Select(p => p.HashedIp).Distinct().Count()
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
