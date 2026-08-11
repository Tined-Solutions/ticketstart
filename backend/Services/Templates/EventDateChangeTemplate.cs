using System.Text;

namespace TicketeraOnline.Api.Services.Templates;

/// <summary>
/// HTML email template notifying ticket buyers that an event's date has changed.
/// Includes the new date, old date, event name, and refund-request contact.
/// Follows the same static Render(pattern) as TicketConfirmationTemplate and
/// RefundNotificationTemplate.
/// </summary>
public static class EventDateChangeTemplate
{
    /// <summary>
    /// Renders the event date change notification email body.
    /// </summary>
    /// <param name="eventName">Name of the event that changed</param>
    /// <param name="oldDate">Previous event date</param>
    /// <param name="newDate">Updated event date</param>
    /// <param name="refundContactEmail">Email address for refund requests</param>
    /// <returns>HTML email body</returns>
    public static string Render(
        string eventName,
        DateTime oldDate,
        DateTime newDate,
        string refundContactEmail)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8' />");
        html.AppendLine("<title>Cambio de fecha del evento</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; color: #333; line-height: 1.5; }");
        html.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        html.AppendLine(".change { background: #fff3cd; padding: 15px; border-radius: 8px; margin-bottom: 20px; }");
        html.AppendLine(".refund { background: #f5f5f5; padding: 15px; border-radius: 8px; margin-top: 20px; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class='container'>");

        html.AppendLine("<h1>Cambio de fecha del evento</h1>");

        html.AppendLine($"<p>Hola,</p>");
        html.AppendLine($"<p>El evento <strong>{HtmlEncoder.Escape(eventName)}</strong> cambió de fecha.</p>");

        html.AppendLine("<div class='change'>");
        html.AppendLine($"<p><strong>Fecha anterior:</strong> {oldDate.ToUniversalTime():dd/MM/yyyy HH:mm}</p>");
        html.AppendLine($"<p><strong>Nueva fecha:</strong> {newDate.ToUniversalTime():dd/MM/yyyy HH:mm}</p>");
        html.AppendLine("</div>");

        html.AppendLine($"<p>Lamentamos las molestias. Si la nueva fecha no te funciona, podés solicitar un reembolso.</p>");

        html.AppendLine("<div class='refund'>");
        html.AppendLine("<h2>Solicitar reembolso</h2>");
        html.AppendLine($"<p>Contactanos en <a href=\"mailto:{HtmlEncoder.Escape(refundContactEmail)}\">{HtmlEncoder.Escape(refundContactEmail)}</a> para solicitar el reembolso de tus entradas.</p>");
        html.AppendLine("</div>");

        html.AppendLine("<p>Gracias por tu comprensión.</p>");
        html.AppendLine("<p>— El equipo de Ticketera</p>");
        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }
}
