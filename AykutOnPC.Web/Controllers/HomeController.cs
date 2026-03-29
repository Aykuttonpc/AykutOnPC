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

        try
        {
            var reposTask = gitHubService.GetRepositoriesAsync(ghUsername, cancellationToken);

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
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database may not be ready yet. Rendering page with defaults.");
            
            var fallback = new DashboardViewModel
            {
                RecentBuilds = Enumerable.Empty<AykutOnPC.Core.Entities.Build>(),
                TopSpecs = Enumerable.Empty<AykutOnPC.Core.Entities.Spec>(),
                Experiences = Enumerable.Empty<AykutOnPC.Core.Entities.Experience>(),
                Educations = Enumerable.Empty<AykutOnPC.Core.Entities.Education>(),
                UserProfile = new AykutOnPC.Core.Entities.Profile { FullName = "Aykut" },
                GitHubUsername = ghUsername,
                HeroTitle = _seedData.HeroTitle ?? "<span class='text-gradient'>Hi, I'm Aykut.</span><br>Building future.",
                HeroSubtitle = _seedData.HeroSubtitle ?? "Full Stack .NET Developer & AI Enthusiast"
            };
            return View(fallback);
        }
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
