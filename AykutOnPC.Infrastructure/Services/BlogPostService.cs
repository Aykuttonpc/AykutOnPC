using System.Text;
using System.Text.RegularExpressions;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Infrastructure.Services;

public class BlogPostService(AppDbContext context) : IBlogPostService
{
    public async Task<IReadOnlyList<BlogPost>> GetAllForAdminAsync(CancellationToken cancellationToken = default)
        => await context.BlogPosts
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BlogPost>> GetPublishedAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = context.BlogPosts
            .AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAtUtc);

        return limit is { } l && l > 0
            ? await query.Take(l).ToListAsync(cancellationToken)
            : await query.ToListAsync(cancellationToken);
    }

    public Task<BlogPost?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<BlogPost?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => context.BlogPosts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsPublished && p.Slug == slug, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => context.BlogPosts.AnyAsync(p => p.Id == id, cancellationToken);

    public async Task CreateAsync(BlogPost post, CancellationToken cancellationToken = default)
    {
        post.Slug = await EnsureUniqueSlugAsync(
            string.IsNullOrWhiteSpace(post.Slug) ? Slugify(post.Title) : Slugify(post.Slug),
            currentId: null,
            cancellationToken);

        var now = DateTime.UtcNow;
        post.CreatedAtUtc = now;
        post.UpdatedAtUtc = now;
        if (post.IsPublished && post.PublishedAtUtc is null)
            post.PublishedAtUtc = now;

        context.BlogPosts.Add(post);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BlogPost post, CancellationToken cancellationToken = default)
    {
        var existing = await context.BlogPosts.FirstOrDefaultAsync(p => p.Id == post.Id, cancellationToken)
            ?? throw new InvalidOperationException($"BlogPost {post.Id} not found.");

        var newSlug = string.IsNullOrWhiteSpace(post.Slug) ? Slugify(post.Title) : Slugify(post.Slug);
        if (!string.Equals(existing.Slug, newSlug, StringComparison.Ordinal))
            newSlug = await EnsureUniqueSlugAsync(newSlug, currentId: post.Id, cancellationToken);

        existing.Title       = post.Title;
        existing.Slug        = newSlug;
        existing.Excerpt     = post.Excerpt;
        existing.Content     = post.Content;
        existing.Tags        = post.Tags;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        // First publish stamps PublishedAtUtc; subsequent unpublish/republish preserves the original timestamp
        // so the public ordering and RSS feed don't shuffle on edits.
        if (post.IsPublished && !existing.IsPublished && existing.PublishedAtUtc is null)
            existing.PublishedAtUtc = DateTime.UtcNow;
        existing.IsPublished = post.IsPublished;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null) return;
        context.BlogPosts.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    // ── Slug helpers ──────────────────────────────────────────────

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, int? currentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "post";

        var candidate = baseSlug;
        var suffix = 1;
        while (await context.BlogPosts.AnyAsync(
                   p => p.Slug == candidate && (currentId == null || p.Id != currentId),
                   cancellationToken))
        {
            suffix++;
            candidate = $"{baseSlug}-{suffix}";
        }
        return candidate;
    }

    private static readonly Regex NonSlugChars = new(@"[^a-z0-9\s-]", RegexOptions.Compiled);
    private static readonly Regex MultiHyphen = new(@"-+", RegexOptions.Compiled);
    private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Map common Turkish letters before stripping diacritics so they round-trip predictably.
        var normalized = input
            .Replace('ı', 'i').Replace('İ', 'I')
            .Replace('ş', 's').Replace('Ş', 'S')
            .Replace('ğ', 'g').Replace('Ğ', 'G')
            .Replace('ü', 'u').Replace('Ü', 'U')
            .Replace('ö', 'o').Replace('Ö', 'O')
            .Replace('ç', 'c').Replace('Ç', 'C');

        normalized = normalized.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        var slug = sb.ToString().ToLowerInvariant();
        slug = NonSlugChars.Replace(slug, "");
        slug = MultiSpace.Replace(slug, "-");
        slug = MultiHyphen.Replace(slug, "-").Trim('-');

        return slug.Length > 200 ? slug[..200] : slug;
    }
}
