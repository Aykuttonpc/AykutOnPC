using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class KnowledgeBaseController(IKnowledgeBaseService knowledgeBaseService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var entries = await knowledgeBaseService.GetAllAsync(cancellationToken);
        return View(entries);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateKnowledgeEntryDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var entry = new KnowledgeEntry
        {
            Topic = dto.Topic,
            Content = dto.Content,
            Keywords = dto.Keywords
        };

        await knowledgeBaseService.CreateAsync(entry, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();

        var entry = await knowledgeBaseService.GetByIdAsync(id.Value, cancellationToken);
        if (entry is null) return NotFound();
        
        var dto = new UpdateKnowledgeEntryDto
        {
            Id = entry.Id,
            Topic = entry.Topic,
            Content = entry.Content,
            Keywords = entry.Keywords
        };
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateKnowledgeEntryDto dto, CancellationToken cancellationToken)
    {
        if (id != dto.Id) return NotFound();

        if (!ModelState.IsValid)
            return View(dto);

        try
        {
            var entry = new KnowledgeEntry
            {
                Id = dto.Id,
                Topic = dto.Topic,
                Content = dto.Content,
                Keywords = dto.Keywords
            };

            await knowledgeBaseService.UpdateAsync(entry, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await knowledgeBaseService.ExistsAsync(id, cancellationToken))
                return NotFound();
            throw;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await knowledgeBaseService.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
