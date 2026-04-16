using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IVisitorAnalyticsService
{
    /// <summary>
    /// Records a page view. Called from middleware — must be fire-and-forget
    /// to avoid adding latency to the request pipeline.
    /// </summary>
    Task RecordAsync(PageView pageView, CancellationToken ct = default);

    /// <summary>Returns aggregated stats for the admin dashboard.</summary>
    Task<VisitorSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}
