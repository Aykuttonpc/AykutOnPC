using AykutOnPC.Core.Entities;
using AykutOnPC.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class SpecsController : Controller
{
    private readonly AppDbContext _context;

    public SpecsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Specs.OrderByDescending(s => s.Proficiency).ToListAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Spec spec)
    {
        if (ModelState.IsValid)
        {
            _context.Add(spec);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(spec);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var spec = await _context.Specs.FindAsync(id);
        if (spec == null) return NotFound();
        return View(spec);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Spec spec)
    {
        if (id != spec.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(spec);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Specs.Any(e => e.Id == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(spec);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var spec = await _context.Specs.FindAsync(id);
        if (spec != null)
        {
            _context.Specs.Remove(spec);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
