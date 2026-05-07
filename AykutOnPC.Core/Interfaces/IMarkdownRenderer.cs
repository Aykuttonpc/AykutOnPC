namespace AykutOnPC.Core.Interfaces;

public interface IMarkdownRenderer
{
    /// <summary>Render markdown to sanitized HTML safe for direct injection into a Razor view via @Html.Raw.</summary>
    string ToSafeHtml(string markdown);
}
