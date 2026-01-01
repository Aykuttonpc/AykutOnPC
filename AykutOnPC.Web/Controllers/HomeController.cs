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
        // 1. Fetch Site Settings
        var settings = await _context.SiteSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
        
        // Fetch fallback from Config if DB is empty
        string ghUsername = settings.ContainsKey("GitHubUsername") 
            ? settings["GitHubUsername"] 
            : _configuration["SeedData:GitHubUsername"] ?? string.Empty;

        // 2. Fetch Projects using dynamic username
        var allGitHubBuilds = await _gitHubService.GetRepositoriesAsync(ghUsername); 
        
        var viewModel = new DashboardViewModel
        {
            RecentBuilds = allGitHubBuilds,
            TopSpecs = await _context.Specs.OrderByDescending(s => s.Proficiency).Take(5).ToListAsync(),
            SiteSettings = settings // Pass all settings to view
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
