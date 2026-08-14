using System.Globalization;
using System.Text;

namespace TicketeraOnline.Api.Services.Templates;

/// <summary>
/// HTML email template for refund notifications. Renders only the per-email
/// body content; the shared layout (brand header, greeting, footer) is
/// provided by <see cref="EmailLayout"/>.
/// </summary>
public static class RefundNotificationTemplate
{
    /// <summary>
    /// Renders the refund notification email body.
    /// </summary>
    /// <param name="amount">Refund amount</param>
    /// <param name="reason">Human-readable refund reason</param>
    /// <param name="recipientName">Optional recipient name for the greeting</param>
    /// <returns>HTML email body</returns>
    public static string Render(decimal amount, string reason, string? recipientName = null)
    {
        var html = new StringBuilder();

        html.AppendLine("<div class=\"block\">");
        html.AppendLine($"<p>Procesamos un reembolso de <strong>${amount.ToString("0.00", CultureInfo.InvariantCulture)}</strong>.</p>");
        html.AppendLine($"<p><strong>Motivo:</strong> {HtmlEncoder.Escape(reason)}</p>");
        html.AppendLine("<p>El reembolso debería acreditarse en tu método de pago original dentro de 5 a 10 días hábiles.</p>");
        html.AppendLine("</div>");

        html.AppendLine("<p>Lamentamos las molestias.</p>");

        return EmailLayout.Render("Te reembolsamos tu compra", recipientName, html.ToString());
    }
}