using AykutOnPC.Core.Entities;
using AykutOnPC.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ExperienceController : Controller
{
    private readonly AppDbContext _context;

    public ExperienceController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Experiences.OrderByDescending(e => e.StartDate).ToListAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Experience experience)
    {
        if (ModelState.IsValid)
        {
            _context.Add(experience);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(experience);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var experience = await _context.Experiences.FindAsync(id);
        if (experience == null) return NotFound();
        return View(experience);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Experience experience)
    {
        if (id != experience.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(experience);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Experiences.Any(e => e.Id == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(experience);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var experience = await _context.Experiences.FindAsync(id);
        if (experience != null)
        {
            _context.Experiences.Remove(experience);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
