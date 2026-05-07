using AykutOnPC.Core.Interfaces;
using Ganss.Xss;
using Markdig;

namespace AykutOnPC.Infrastructure.Services;

public class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    public string ToSafeHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = Markdown.ToHtml(markdown, _pipeline);
        return _sanitizer.Sanitize(html);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        // HtmlSanitizer's defaults already block <script>, <iframe>, on*= handlers, javascript: URLs.
        // We only need to allow the structural tags Markdig emits and a couple of useful extras.
        var s = new HtmlSanitizer();
        s.AllowedTags.Add("pre");
        s.AllowedTags.Add("code");
        s.AllowedAttributes.Add("class"); // Markdig emits language-* on <code>
        s.AllowedAttributes.Add("id");    // heading anchors
        s.AllowedSchemes.Add("mailto");
        return s;
    }
}
