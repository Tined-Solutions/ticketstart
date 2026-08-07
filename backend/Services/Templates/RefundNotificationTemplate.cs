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
        html.AppendLine("<title>Notificación de reembolso</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; color: #333; line-height: 1.5; }");
        html.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        html.AppendLine(".refund { background: #fff3cd; padding: 15px; border-radius: 8px; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class='container'>");

        html.AppendLine("<h1>Notificación de reembolso</h1>");

        html.AppendLine("<div class='refund'>");
        html.AppendLine($"<p>Procesamos un reembolso de <strong>${amount.ToString("0.00", CultureInfo.InvariantCulture)}</strong>.</p>");
        html.AppendLine($"<p><strong>Motivo:</strong> {HtmlEncoder.Escape(reason)}</p>");
        html.AppendLine("<p>El reembolso debería acreditarse en tu método de pago original dentro de 5 a 10 días hábiles.</p>");
        html.AppendLine("</div>");

        html.AppendLine("<p>Lamentamos las molestias.</p>");
        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

}
