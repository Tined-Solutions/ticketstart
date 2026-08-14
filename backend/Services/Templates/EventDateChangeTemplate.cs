using System.Text;

namespace TicketeraOnline.Api.Services.Templates;

/// <summary>
/// HTML email template notifying ticket buyers that an event's date has changed.
/// Includes the new date, old date, event name, and refund-request contact.
/// Renders only the per-email body content; the shared layout (brand header,
/// greeting, footer) is provided by <see cref="EmailLayout"/>.
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
    /// <param name="recipientName">Optional recipient name for the greeting</param>
    /// <returns>HTML email body</returns>
    public static string Render(
        string eventName,
        DateTime oldDate,
        DateTime newDate,
        string refundContactEmail,
        string? recipientName = null)
    {
        var html = new StringBuilder();

        html.AppendLine($"<p>El evento <strong>{HtmlEncoder.Escape(eventName)}</strong> cambió de fecha.</p>");

        html.AppendLine("<div class=\"block\">");
        html.AppendLine($"<p><strong>Fecha anterior:</strong> {oldDate.ToUniversalTime():dd/MM/yyyy HH:mm}</p>");
        html.AppendLine($"<p><strong>Nueva fecha:</strong> {newDate.ToUniversalTime():dd/MM/yyyy HH:mm}</p>");
        html.AppendLine("</div>");

        html.AppendLine("<p><strong>Tu QR ya emitido sigue siendo válido para la nueva fecha.</strong></p>");

        html.AppendLine("<p>Lamentamos las molestias. Si la nueva fecha no te funciona, podés pedir un reembolso.</p>");

        html.AppendLine("<div class=\"block\">");
        html.AppendLine("<h2>Solicitar un reembolso</h2>");
        html.AppendLine($"<p>Escribinos a <a href=\"mailto:{HtmlEncoder.Escape(refundContactEmail)}\">{HtmlEncoder.Escape(refundContactEmail)}</a> para pedir el reembolso de tus entradas.</p>");
        html.AppendLine("</div>");

        html.AppendLine("<p>Gracias por tu comprensión.</p>");

        return EmailLayout.Render("Tu evento cambió de fecha", recipientName, html.ToString());
    }
}