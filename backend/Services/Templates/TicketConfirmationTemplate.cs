using System.Globalization;
using System.Text;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services.Templates;

/// <summary>
/// HTML email template for ticket purchase confirmations.
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
    /// <returns>HTML email body</returns>
    public static string Render(
        Event eventDetails,
        IEnumerable<(Ticket Ticket, string ContentId)> tickets,
        decimal totalAmount,
        string recipientEmail)
    {
        var ticketList = tickets.ToList();
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8' />");
        html.AppendLine("<title>Tus entradas</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; color: #333; line-height: 1.5; }");
        html.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        html.AppendLine(".event { background: #f5f5f5; padding: 15px; border-radius: 8px; margin-bottom: 20px; }");
        html.AppendLine(".ticket { border: 1px solid #ddd; padding: 15px; margin-bottom: 15px; border-radius: 8px; }");
        html.AppendLine(".qr-code { margin: 10px 0; }");
        html.AppendLine(".summary { background: #e8f4e8; padding: 15px; border-radius: 8px; margin-top: 20px; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class='container'>");

        html.AppendLine("<h1>¡Tu compra está confirmada!</h1>");

        html.AppendLine("<div class='event'>");
        html.AppendLine($"<h2>{HtmlEncoder.Escape(eventDetails.Name)}</h2>");
        html.AppendLine($"<p><strong>Fecha:</strong> {eventDetails.Date.ToUniversalTime():dd/MM/yyyy HH:mm}</p>");
        html.AppendLine($"<p><strong>Ubicación:</strong> {HtmlEncoder.Escape(eventDetails.Location)}</p>");
        if (!string.IsNullOrWhiteSpace(eventDetails.Description))
        {
            html.AppendLine($"<p>{HtmlEncoder.Escape(eventDetails.Description)}</p>");
        }
        html.AppendLine("</div>");

        html.AppendLine("<p>¡Hola!</p>");
        html.AppendLine($"<p>Compraste <strong>{ticketList.Count}</strong> entrada(s). Tus códigos QR están más abajo.</p>");

        for (var i = 0; i < ticketList.Count; i++)
        {
            var (ticket, contentId) = ticketList[i];
            var ticketTypeName = ticket.TicketType?.Name ?? "Entrada";
            var ticketPrice = ticket.TicketType?.Price.ToString("0.00", CultureInfo.InvariantCulture) ?? "0.00";

            html.AppendLine("<div class='ticket'>");
            html.AppendLine($"<h3>Entrada {i + 1}: {HtmlEncoder.Escape(ticketTypeName)}</h3>");
            html.AppendLine($"<p><strong>Precio:</strong> ${ticketPrice}</p>");
            html.AppendLine("<div class='qr-code'>");
            html.AppendLine($"<img src=\"cid:{HtmlEncoder.Escape(contentId)}\" alt=\"Código QR de la entrada {i + 1}\" width=\"200\" height=\"200\" />");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
        }

        html.AppendLine("<div class='summary'>");
        html.AppendLine("<h2>Confirmación de compra</h2>");
        html.AppendLine($"<p><strong>Total de entradas:</strong> {ticketList.Count}</p>");
        html.AppendLine($"<p><strong>Monto total:</strong> ${totalAmount.ToString("0.00", CultureInfo.InvariantCulture)}</p>");
        html.AppendLine($"<p><strong>Email de confirmación:</strong> {HtmlEncoder.Escape(recipientEmail)}</p>");
        html.AppendLine("</div>");

        html.AppendLine("<p>¡Gracias por tu compra!</p>");
        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

}
