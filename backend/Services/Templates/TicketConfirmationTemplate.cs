using System.Globalization;
using System.Text;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services.Templates;

/// <summary>
/// HTML email template for ticket purchase confirmations. Renders only the
/// per-email body content; the shared layout (brand header, greeting, footer)
/// is provided by <see cref="EmailLayout"/>.
/// </summary>
public static class TicketConfirmationTemplate
{
    /// <summary>
    /// Renders the ticket confirmation email body.
    /// </summary>
    /// <param name="eventDetails">Event information</param>
    /// <param name="tickets">Tickets with their Content-ID references for inline QR images</param>
    /// <param name="totalAmount">Total purchase amount</param>
    /// <param name="recipientEmail">Purchaser email address</param>
    /// <param name="recipientName">Optional recipient name for the greeting</param>
    /// <returns>HTML email body</returns>
    public static string Render(
        Event eventDetails,
        IEnumerable<(Ticket Ticket, string ContentId)> tickets,
        decimal totalAmount,
        string recipientEmail,
        string? recipientName = null)
    {
        var ticketList = tickets.ToList();
        var html = new StringBuilder();

        html.AppendLine("<p>¡Tu compra está confirmada!</p>");

        html.AppendLine("<div class=\"block\">");
        html.AppendLine($"<h2>{HtmlEncoder.Escape(eventDetails.Name)}</h2>");
        html.AppendLine($"<p><strong>Fecha:</strong> {eventDetails.Date.ToUniversalTime():dd/MM/yyyy HH:mm}</p>");
        html.AppendLine($"<p><strong>Ubicación:</strong> {HtmlEncoder.Escape(eventDetails.Location)}</p>");
        if (!string.IsNullOrWhiteSpace(eventDetails.Description))
        {
            html.AppendLine($"<p>{HtmlEncoder.Escape(eventDetails.Description)}</p>");
        }
        html.AppendLine("</div>");

        html.AppendLine($"<p>Compraste <strong>{ticketList.Count}</strong> entrada(s). Tus códigos QR están más abajo, listos para usar.</p>");

        for (var i = 0; i < ticketList.Count; i++)
        {
            var (ticket, contentId) = ticketList[i];
            var ticketTypeName = ticket.TicketType?.Name ?? "Entrada";
            var ticketPrice = ticket.TicketType?.Price.ToString("0.00", CultureInfo.InvariantCulture) ?? "0.00";

            html.AppendLine("<div class=\"ticket\">");
            html.AppendLine($"<h3>Entrada {i + 1}: {HtmlEncoder.Escape(ticketTypeName)}</h3>");
            html.AppendLine($"<p><strong>Precio:</strong> ${ticketPrice}</p>");
            html.AppendLine("<div class=\"qr-code\">");
            html.AppendLine($"<img src=\"cid:{HtmlEncoder.Escape(contentId)}\" alt=\"Código QR de la entrada {i + 1}\" width=\"200\" height=\"200\" />");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
        }

        html.AppendLine("<div class=\"block accent\">");
        html.AppendLine("<h2>Confirmación de compra</h2>");
        html.AppendLine($"<p><strong>Total de entradas:</strong> {ticketList.Count}</p>");
        html.AppendLine($"<p><strong>Monto total:</strong> ${totalAmount.ToString("0.00", CultureInfo.InvariantCulture)}</p>");
        html.AppendLine($"<p><strong>Email de confirmación:</strong> {HtmlEncoder.Escape(recipientEmail)}</p>");
        html.AppendLine("</div>");

        html.AppendLine("<p>¡Gracias por tu compra!</p>");

        return EmailLayout.Render("Tus entradas", recipientName, html.ToString());
    }
}