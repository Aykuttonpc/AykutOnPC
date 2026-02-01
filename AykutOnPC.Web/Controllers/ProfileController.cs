using AykutOnPC.Core.Entities;
using AykutOnPC.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ProfileController : Controller
{
    private readonly AppDbContext _context;

    public ProfileController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync();
        if (profile == null)
        {
            profile = new Profile { FullName = "Aykut" };
            _context.Profiles.Add(profile);
            await _context.SaveChangesAsync();
        }
        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Profile profile)
    {
        if (ModelState.IsValid)
        {
            _context.Update(profile);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Profil başarıyla güncellendi!";
            return RedirectToAction(nameof(Index));
        }
        
        return View("Index", profile);
    }
}
