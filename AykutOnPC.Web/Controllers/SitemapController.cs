using System.Text;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

[Route("sitemap.xml")]
public class SitemapController(IBlogPostService blogPostService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var posts = await blogPostService.GetPublishedAsync(limit: null, cancellationToken);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var sb = new StringBuilder(1024);
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
        sb.Append("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""").Append('\n');

        AppendUrl(sb, $"{baseUrl}/",     DateTime.UtcNow, "weekly",  "1.0");
        AppendUrl(sb, $"{baseUrl}/blog", DateTime.UtcNow, "daily",   "0.8");

        foreach (var post in posts)
            AppendUrl(sb, $"{baseUrl}/blog/{post.Slug}", post.UpdatedAtUtc, "monthly", "0.7");

        sb.Append("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    private static void AppendUrl(StringBuilder sb, string loc, DateTime lastMod, string changefreq, string priority)
    {
        sb.Append("  <url>\n");
        sb.Append("    <loc>").Append(System.Net.WebUtility.HtmlEncode(loc)).Append("</loc>\n");
        sb.Append("    <lastmod>").Append(lastMod.ToString("yyyy-MM-dd")).Append("</lastmod>\n");
        sb.Append("    <changefreq>").Append(changefreq).Append("</changefreq>\n");
        sb.Append("    <priority>").Append(priority).Append("</priority>\n");
        sb.Append("  </url>\n");
    }
}
