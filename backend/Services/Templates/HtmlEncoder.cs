namespace TicketeraOnline.Api.Services.Templates;

/// <summary>
/// Minimal HTML entity encoder for email templates.
/// </summary>
public static class HtmlEncoder
{
    /// <summary>
    /// Escapes characters that have special meaning in HTML.
    /// </summary>
    /// <param name="input">Raw text</param>
    /// <returns>HTML-safe text</returns>
    public static string Escape(string input)
    {
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
