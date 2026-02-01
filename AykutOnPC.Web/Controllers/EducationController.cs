using AykutOnPC.Core.Entities;
using AykutOnPC.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class EducationController : Controller
{
    private readonly AppDbContext _context;

    public EducationController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Educations.OrderByDescending(e => e.StartDate).ToListAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Education education)
    {
        if (ModelState.IsValid)
        {
            _context.Add(education);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(education);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var education = await _context.Educations.FindAsync(id);
        if (education == null) return NotFound();
        return View(education);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Education education)
    {
        if (id != education.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(education);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Educations.Any(e => e.Id == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(education);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var education = await _context.Educations.FindAsync(id);
        if (education != null)
        {
            _context.Educations.Remove(education);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
