using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AykutOnPC.Web.Controllers;

public class HomeController(
    ILogger<HomeController> logger,
    IGitHubService gitHubService,
    ISpecService specService,
    IExperienceService experienceService,
    IEducationService educationService,
    IProfileService profileService,
    IOptions<SeedDataSettings> seedDataOptions) : Controller
{
    private readonly SeedDataSettings _seedData = seedDataOptions.Value;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var ghUsername = _seedData.GitHubUsername;

        // GitHub API call can run in parallel with DB calls since it uses HttpClient (no DbContext)
        var reposTask = gitHubService.GetRepositoriesAsync(ghUsername, cancellationToken);

        // DbContext is NOT thread-safe, so DB calls must be sequential
        var specs = await specService.GetTopAsync(8, cancellationToken);
        var experiences = await experienceService.GetAllAsync(cancellationToken);
        var educations = await educationService.GetAllAsync(cancellationToken);
        var profile = await profileService.GetOrCreateProfileAsync(cancellationToken);

        var viewModel = new DashboardViewModel
        {
            RecentBuilds = await reposTask,
            TopSpecs = specs,
            Experiences = experiences,
            Educations = educations,
            UserProfile = profile,
            GitHubUsername = ghUsername,
            HeroTitle = _seedData.HeroTitle,
            HeroSubtitle = _seedData.HeroSubtitle
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
