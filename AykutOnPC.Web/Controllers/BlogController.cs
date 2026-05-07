using System.Net;
using System.Text;
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

    [HttpGet("feed.xml")]
    public async Task<IActionResult> Feed(CancellationToken cancellationToken)
    {
        var posts = await blogPostService.GetPublishedAsync(limit: 20, cancellationToken);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var sb = new StringBuilder(2048);
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
        sb.Append("""<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">""").Append('\n');
        sb.Append("  <channel>\n");
        sb.Append("    <title>AykutOnPC Blog</title>\n");
        sb.Append("    <link>").Append(baseUrl).Append("/blog</link>\n");
        sb.Append("    <description>Aykut Çinçik'in blog yazıları</description>\n");
        sb.Append("    <language>tr</language>\n");
        sb.Append("    <atom:link href=\"").Append(baseUrl).Append("/blog/feed.xml\" rel=\"self\" type=\"application/rss+xml\" />\n");

        foreach (var post in posts)
        {
            var url = $"{baseUrl}/blog/{post.Slug}";
            sb.Append("    <item>\n");
            sb.Append("      <title>").Append(WebUtility.HtmlEncode(post.Title)).Append("</title>\n");
            sb.Append("      <link>").Append(url).Append("</link>\n");
            sb.Append("      <guid isPermaLink=\"true\">").Append(url).Append("</guid>\n");
            sb.Append("      <pubDate>").Append((post.PublishedAtUtc ?? post.CreatedAtUtc).ToString("R")).Append("</pubDate>\n");
            if (!string.IsNullOrWhiteSpace(post.Excerpt))
                sb.Append("      <description>").Append(WebUtility.HtmlEncode(post.Excerpt)).Append("</description>\n");
            sb.Append("    </item>\n");
        }

        sb.Append("  </channel>\n");
        sb.Append("</rss>");
        return Content(sb.ToString(), "application/rss+xml", Encoding.UTF8);
    }
}
