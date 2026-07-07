using System.Globalization;
using System.Text;

namespace TicketeraOnline.Api.Services.Templates;

/// <summary>
/// HTML email template for refund notifications.
/// </summary>
public static class RefundNotificationTemplate
{
    /// <summary>
    /// Renders the refund notification email body.
    /// </summary>
    /// <param name="amount">Refund amount</param>
    /// <param name="reason">Human-readable refund reason</param>
    /// <returns>HTML email body</returns>
    public static string Render(decimal amount, string reason)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8' />");
        html.AppendLine("<title>Refund notification</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; color: #333; line-height: 1.5; }");
        html.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        html.AppendLine(".refund { background: #fff3cd; padding: 15px; border-radius: 8px; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class='container'>");

        html.AppendLine("<h1>Refund notification</h1>");

        html.AppendLine("<div class='refund'>");
        html.AppendLine($"<p>We have processed a refund of <strong>${amount.ToString("0.00", CultureInfo.InvariantCulture)}</strong>.</p>");
        html.AppendLine($"<p><strong>Reason:</strong> {HtmlEncoder.Escape(reason)}</p>");
        html.AppendLine("<p>The refund should appear in your original payment method within 5-10 business days.</p>");
        html.AppendLine("</div>");

        html.AppendLine("<p>We apologize for any inconvenience.</p>");
        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

}
