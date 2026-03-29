using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ExperienceController(IExperienceService experienceService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var experiences = await experienceService.GetAllAsync(cancellationToken);
        return View(experiences);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateExperienceDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var experience = new Experience
        {
            Company = dto.Company,
            Position = dto.Position,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Description = dto.Description,
            CompanyUrl = dto.CompanyUrl
        };

        await experienceService.CreateAsync(experience, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();

        var experience = await experienceService.GetByIdAsync(id.Value, cancellationToken);
        if (experience is null) return NotFound();
        
        var dto = new UpdateExperienceDto
        {
            Id = experience.Id,
            Company = experience.Company,
            Position = experience.Position,
            StartDate = experience.StartDate,
            EndDate = experience.EndDate,
            Description = experience.Description,
            CompanyUrl = experience.CompanyUrl
        };
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateExperienceDto dto, CancellationToken cancellationToken)
    {
        if (id != dto.Id) return NotFound();

        if (!ModelState.IsValid)
            return View(dto);

        try
        {
            var experience = new Experience
            {
                Id = dto.Id,
                Company = dto.Company,
                Position = dto.Position,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Description = dto.Description,
                CompanyUrl = dto.CompanyUrl
            };

            await experienceService.UpdateAsync(experience, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await experienceService.ExistsAsync(id, cancellationToken))
                return NotFound();
            throw;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await experienceService.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
