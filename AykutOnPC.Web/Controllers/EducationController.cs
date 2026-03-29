using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class EducationController(IEducationService educationService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var educations = await educationService.GetAllAsync(cancellationToken);
        return View(educations);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEducationDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var education = new Education
        {
            Institution = dto.Institution,
            Degree = dto.Degree,
            FieldOfStudy = dto.FieldOfStudy,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Description = dto.Description,
            Grade = dto.Grade
        };

        await educationService.CreateAsync(education, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();

        var education = await educationService.GetByIdAsync(id.Value, cancellationToken);
        if (education is null) return NotFound();
        return View(education);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateEducationDto dto, CancellationToken cancellationToken)
    {
        if (id != dto.Id) return NotFound();

        if (!ModelState.IsValid)
            return View(dto);

        try
        {
            var education = new Education
            {
                Id = dto.Id,
                Institution = dto.Institution,
                Degree = dto.Degree,
                FieldOfStudy = dto.FieldOfStudy,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Description = dto.Description,
                Grade = dto.Grade
            };

            await educationService.UpdateAsync(education, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await educationService.ExistsAsync(id, cancellationToken))
                return NotFound();
            throw;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await educationService.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
