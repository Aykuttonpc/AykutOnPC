using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class SpecsController(ISpecService specService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var specs = await specService.GetAllAsync(cancellationToken);
        return View(specs);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSpecDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var spec = new Spec(dto.Name, dto.Category, dto.Proficiency)
        {
            IconClass = dto.IconClass
        };

        await specService.CreateAsync(spec, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();

        var spec = await specService.GetByIdAsync(id.Value, cancellationToken);
        if (spec is null) return NotFound();
        
        var dto = new UpdateSpecDto
        {
            Id = spec.Id,
            Name = spec.Name,
            Category = spec.Category,
            Proficiency = spec.Proficiency,
            IconClass = spec.IconClass
        };
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateSpecDto dto, CancellationToken cancellationToken)
    {
        if (id != dto.Id) return NotFound();

        if (!ModelState.IsValid)
            return View(dto);

        try
        {
            var spec = new Spec(dto.Name, dto.Category, dto.Proficiency)
            {
                Id = dto.Id,
                IconClass = dto.IconClass
            };

            await specService.UpdateAsync(spec, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await specService.ExistsAsync(id, cancellationToken))
                return NotFound();
            throw;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await specService.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
