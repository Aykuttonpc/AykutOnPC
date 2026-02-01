using AykutOnPC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using AykutOnPC.Core.Interfaces;

namespace AykutOnPC.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _context;
    private readonly IGitHubService _gitHubService;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, AppDbContext context, IGitHubService gitHubService, IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _gitHubService = gitHubService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        // 1. Fetch Configuration
        string ghUsername = _configuration["SeedData:GitHubUsername"] ?? "Aykuttonpc";
        string title = _configuration["SeedData:HeroTitle"] ?? "Hello";
        string subtitle = _configuration["SeedData:HeroSubtitle"] ?? "Developer";

        // 2. Fetch Projects using dynamic username
        var allGitHubBuilds = await _gitHubService.GetRepositoriesAsync(ghUsername); 
        
        var specs = await _context.Specs.OrderByDescending(s => s.Proficiency).Take(8).ToListAsync();
        var experiences = await _context.Experiences.OrderByDescending(e => e.StartDate).ToListAsync();

        var viewModel = new DashboardViewModel
        {
            RecentBuilds = allGitHubBuilds,
            TopSpecs = specs,
            Experiences = experiences,
            
            // Static Config Mapped to ViewModel
            GitHubUsername = ghUsername,
            HeroTitle = title,
            HeroSubtitle = subtitle
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
