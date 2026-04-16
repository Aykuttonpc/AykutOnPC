using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(IVisitorAnalyticsService analyticsService) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Renders the Visitor Intelligence dashboard.
    /// Data is loaded via AJAX from the /admin/analytics/data endpoint
    /// to keep the initial page load instant.
    /// </summary>
    public IActionResult Analytics()
    {
        return View();
    }

    /// <summary>
    /// JSON endpoint consumed by the Analytics dashboard via fetch().
    /// Returns aggregated visitor stats for the last 30 days.
    /// </summary>
    [HttpGet("/admin/analytics/data")]
    public async Task<IActionResult> AnalyticsData(CancellationToken ct)
    {
        var summary = await analyticsService.GetSummaryAsync(ct);
        return Json(summary);
    }
}
