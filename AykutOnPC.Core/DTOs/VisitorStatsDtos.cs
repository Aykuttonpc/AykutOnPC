namespace AykutOnPC.Core.DTOs;

/// <summary>Summary stats for the admin analytics dashboard.</summary>
public record VisitorSummaryDto(
    int TotalVisitsToday,
    int TotalVisitsThisWeek,
    int TotalVisitsThisMonth,
    int UniqueVisitorsToday,
    IReadOnlyList<TopPageDto> TopPages,
    IReadOnlyList<DeviceBreakdownDto> DeviceBreakdown,
    IReadOnlyList<DailyVisitDto> DailyTrend
);

public record TopPageDto(string Path, int VisitCount);

public record DeviceBreakdownDto(string DeviceType, int Count, double Percentage);

public record DailyVisitDto(DateOnly Date, int Visits, int UniqueVisitors);
