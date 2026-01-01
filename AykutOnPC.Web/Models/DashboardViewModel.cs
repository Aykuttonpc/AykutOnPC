using AykutOnPC.Core.Entities;

namespace AykutOnPC.Web.Models;

public class DashboardViewModel
{
    public IEnumerable<Build> RecentBuilds { get; set; } = new List<Build>();
    public IEnumerable<Spec> TopSpecs { get; set; } = new List<Spec>();
    
    // Pagination
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }

    // Dynamic Settings
    public Dictionary<string, string> SiteSettings { get; set; } = new();
}
