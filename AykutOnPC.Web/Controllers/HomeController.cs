using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Entities;
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
            var reposTask   = gitHubService.GetRepositoriesAsync(ghUsername, cancellationToken);
            var specs       = await specService.GetTopAsync(8, cancellationToken);
            var experiences = await experienceService.GetAllAsync(cancellationToken);
            var educations  = await educationService.GetAllAsync(cancellationToken);
            var profile     = await profileService.GetOrCreateProfileAsync(cancellationToken);

            return View(new DashboardViewModel
            {
                RecentBuilds  = await reposTask,
                TopSpecs      = specs,
                Experiences   = experiences,
                Educations    = educations,
                UserProfile   = profile,
                GitHubUsername = ghUsername,
                HeroTitle     = _seedData.HeroTitle,
                HeroSubtitle  = _seedData.HeroSubtitle
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database may not be ready yet. Rendering page with defaults.");

            return View(new DashboardViewModel
            {
                RecentBuilds   = Enumerable.Empty<Build>(),
                TopSpecs       = Enumerable.Empty<Spec>(),
                Experiences    = Enumerable.Empty<Experience>(),
                Educations     = Enumerable.Empty<Education>(),
                UserProfile    = new Profile { FullName = "Aykut" },
                GitHubUsername = ghUsername,
                HeroTitle      = _seedData.HeroTitle,
                HeroSubtitle   = _seedData.HeroSubtitle
            });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
