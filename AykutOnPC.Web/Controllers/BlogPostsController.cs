using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Controllers;

[Authorize(Roles = "Admin")]
public class BlogPostsController(IBlogPostService blogPostService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var posts = await blogPostService.GetAllForAdminAsync(cancellationToken);
        return View(posts);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateBlogPostDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(dto);

        var post = new BlogPost
        {
            Title       = dto.Title,
            Slug        = dto.Slug ?? string.Empty,
            Excerpt     = dto.Excerpt,
            Content     = dto.Content,
            Tags        = dto.Tags ?? string.Empty,
            IsPublished = dto.IsPublished
        };

        await blogPostService.CreateAsync(post, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();
        var post = await blogPostService.GetByIdAsync(id.Value, cancellationToken);
        if (post is null) return NotFound();

        var dto = new UpdateBlogPostDto
        {
            Id          = post.Id,
            Title       = post.Title,
            Slug        = post.Slug,
            Excerpt     = post.Excerpt,
            Content     = post.Content,
            Tags        = post.Tags,
            IsPublished = post.IsPublished
        };
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateBlogPostDto dto, CancellationToken cancellationToken)
    {
        if (id != dto.Id) return NotFound();
        if (!ModelState.IsValid) return View(dto);

        try
        {
            var post = new BlogPost
            {
                Id          = dto.Id,
                Title       = dto.Title,
                Slug        = dto.Slug ?? string.Empty,
                Excerpt     = dto.Excerpt,
                Content     = dto.Content,
                Tags        = dto.Tags ?? string.Empty,
                IsPublished = dto.IsPublished
            };
            await blogPostService.UpdateAsync(post, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await blogPostService.ExistsAsync(id, cancellationToken)) return NotFound();
            throw;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await blogPostService.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
