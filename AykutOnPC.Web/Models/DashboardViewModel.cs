using AykutOnPC.Core.Entities;

namespace AykutOnPC.Web.Models;

public class DashboardViewModel
{
    public IEnumerable<Build> RecentBuilds { get; set; } = new List<Build>();
    public IEnumerable<Spec> TopSpecs { get; set; } = new List<Spec>();
    public IEnumerable<Experience> Experiences { get; set; } = new List<Experience>();
    
    // Pagination
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }

    // Configuration
    public string HeroTitle { get; set; } = string.Empty;
    public string HeroSubtitle { get; set; } = string.Empty;
    public string GitHubUsername { get; set; } = string.Empty;
}
