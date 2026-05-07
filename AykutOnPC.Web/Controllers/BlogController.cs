using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

[Route("blog")]
public class BlogController(IBlogPostService blogPostService, IMarkdownRenderer markdownRenderer) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var posts = await blogPostService.GetPublishedAsync(limit: null, cancellationToken);
        return View(posts);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();

        var post = await blogPostService.GetPublishedBySlugAsync(slug, cancellationToken);
        if (post is null) return NotFound();

        ViewData["RenderedHtml"] = markdownRenderer.ToSafeHtml(post.Content);
        return View(post);
    }
}
