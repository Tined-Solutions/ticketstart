using System.Text;

namespace TicketeraOnline.Api.Services.Templates;

/// <summary>
/// Shared HTML layout for transactional emails. Renders the brand header,
/// personalized greeting, the per-email body content, and the standard footer
/// so all templates share one visual identity (DRY). Only the middle content
/// varies per email.
/// </summary>
public static class EmailLayout
{
    /// <summary>
    /// Renders the full HTML email document with the shared layout (doctype,
    /// head, style, brand header, greeting, body, and footer).
    /// </summary>
    /// <param name="title">Browser/tab title for the email document</param>
    /// <param name="recipientName">Optional recipient name used for the greeting; null or blank falls back to a generic greeting</param>
    /// <param name="bodyHtml">Per-email HTML content rendered between the greeting and the footer</param>
    /// <returns>Full HTML document string</returns>
    public static string Render(string title, string? recipientName, string bodyHtml)
    {
        var greeting = string.IsNullOrWhiteSpace(recipientName)
            ? "¡Hola!"
            : $"¡Hola, {HtmlEncoder.Escape(recipientName)}!";

        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"es\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>{HtmlEncoder.Escape(title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, Helvetica, sans-serif; color: #333; line-height: 1.5; margin: 0; padding: 0; background: #f4f4f5; }");
        html.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 24px; background: #ffffff; }");
        html.AppendLine(".brand { font-size: 20px; font-weight: bold; color: #111; margin-bottom: 16px; }");
        html.AppendLine(".greeting { margin: 0 0 16px; font-size: 16px; }");
        html.AppendLine(".block { background: #f5f5f5; padding: 16px; border-radius: 8px; margin: 16px 0; }");
        html.AppendLine(".block.accent { background: #e8f4e8; }");
        html.AppendLine(".ticket { border: 1px solid #ddd; padding: 16px; border-radius: 8px; margin: 12px 0; }");
        html.AppendLine(".qr-code { margin: 12px 0; }");
        html.AppendLine(".footer { margin-top: 24px; padding-top: 16px; border-top: 1px solid #eee; color: #777; font-size: 14px; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class=\"container\">");
        html.AppendLine("<div class=\"brand\">TicketStart</div>");
        html.AppendLine($"<p class=\"greeting\">{greeting}</p>");
        html.Append(bodyHtml);
        html.AppendLine("<div class=\"footer\"><p>— El equipo de TicketStart</p></div>");
        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }
}