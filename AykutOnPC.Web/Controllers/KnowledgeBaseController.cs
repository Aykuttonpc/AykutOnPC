using AykutOnPC.Core.Entities;
using AykutOnPC.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class KnowledgeBaseController : Controller
{
    private readonly AppDbContext _context;

    public KnowledgeBaseController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.KnowledgeEntries.OrderByDescending(k => k.LastUpdated).ToListAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KnowledgeEntry entry)
    {
        if (ModelState.IsValid)
        {
            entry.LastUpdated = DateTime.Now;
            _context.Add(entry);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(entry);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var entry = await _context.KnowledgeEntries.FindAsync(id);
        if (entry == null) return NotFound();
        return View(entry);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, KnowledgeEntry entry)
    {
        if (id != entry.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                entry.LastUpdated = DateTime.Now;
                _context.Update(entry);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.KnowledgeEntries.Any(e => e.Id == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(entry);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var entry = await _context.KnowledgeEntries.FindAsync(id);
        if (entry != null)
        {
            _context.KnowledgeEntries.Remove(entry);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
