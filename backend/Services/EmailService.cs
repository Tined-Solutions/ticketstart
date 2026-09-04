using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services.Templates;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for transactional email delivery via Resend.
/// Generates HTML templates, embeds QR code images, and retries on failure.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IResendClient _resendClient;
    private readonly ITicketService _ticketService;
    private readonly ILogger<EmailService> _logger;
    private readonly BrevoOptions _options;
    private readonly IConfiguration _configuration;

    private string ResolvedFrom =>
        string.IsNullOrEmpty(_options.FromName)
            ? _options.FromEmail
            : $"\"{_options.FromName}\" <{_options.FromEmail}>";

    public EmailService(
        IResendClient resendClient,
        ITicketService ticketService,
        ILogger<EmailService> logger,
        IOptions<BrevoOptions> options,
        IConfiguration configuration)
    {
        _resendClient = resendClient;
        _ticketService = ticketService;
        _logger = logger;
        _options = options.Value;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<EmailResult> SendTicketEmailAsync(string recipientEmail, IEnumerable<Ticket> tickets, Event eventDetails, string? recipientName = null)
    {
        _logger.LogInformation(
            "Sending ticket confirmation email to {Recipient} for event {EventId}",
            recipientEmail, eventDetails.Id);

        var ticketList = tickets.ToList();
        var attachments = new List<ResendAttachment>();
        var ticketQrCodes = new List<(Ticket Ticket, string QrImageSrc)>();

        for (int i = 0; i < ticketList.Count; i++)
        {
            var ticket = ticketList[i];
            var imageBase64 = _ticketService.GenerateQRCodeImage(ticket.QRCodeData);
            var contentId = $"qr-ticket-{ticket.Id}";

            attachments.Add(new ResendAttachment
            {
                Filename = $"qr-ticket-{i + 1}.png",
                Content = imageBase64,
                ContentType = "image/png",
                ContentId = contentId
            });

            ticketQrCodes.Add((ticket, GetQrImageSrc(ticket, imageBase64)));
        }

        var totalAmount = ticketList.Sum(t => t.TicketType?.Price ?? 0m);
        var html = TicketConfirmationTemplate.Render(
            eventDetails,
            ticketQrCodes,
            totalAmount,
            recipientEmail,
            recipientName);

        var request = new ResendEmailRequest
        {
            From = ResolvedFrom,
            To = recipientEmail,
            Subject = $"Tus entradas para {eventDetails.Name}",
            Html = html,
            Attachments = attachments
        };

        var result = await SendWithRetryAsync(request);

        if (result.Success)
        {
            _logger.LogInformation(
                "Ticket confirmation email sent to {Recipient} for event {EventId}",
                recipientEmail, eventDetails.Id);
        }
        else
        {
            _logger.LogError(
                "Failed to send ticket confirmation email to {Recipient} for event {EventId}: {Error}",
                recipientEmail, eventDetails.Id, result.Error);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<EmailResult> SendResendEmailAsync(string recipientEmail, IEnumerable<Ticket> tickets, Event eventDetails, string? recipientName = null)
    {
        _logger.LogInformation(
            "Sending ticket resend email to {Recipient} for event {EventId}",
            recipientEmail, eventDetails.Id);

        var ticketList = tickets.ToList();
        var attachments = new List<ResendAttachment>();
        var ticketQrCodes = new List<(Ticket Ticket, string QrImageSrc)>();

        for (int i = 0; i < ticketList.Count; i++)
        {
            var ticket = ticketList[i];
            var imageBase64 = _ticketService.GenerateQRCodeImage(ticket.QRCodeData);
            var contentId = $"qr-ticket-{ticket.Id}";

            attachments.Add(new ResendAttachment
            {
                Filename = $"qr-ticket-{i + 1}.png",
                Content = imageBase64,
                ContentType = "image/png",
                ContentId = contentId
            });

            ticketQrCodes.Add((ticket, GetQrImageSrc(ticket, imageBase64)));
        }

        var totalAmount = ticketList.Sum(t => t.TicketType?.Price ?? 0m);
        var html = TicketConfirmationTemplate.Render(
            eventDetails,
            ticketQrCodes,
            totalAmount,
            recipientEmail,
            recipientName);

        var request = new ResendEmailRequest
        {
            From = ResolvedFrom,
            To = recipientEmail,
            Subject = $"Te reenviamos tus entradas para {eventDetails.Name}",
            Html = html,
            Attachments = attachments
        };

        var result = await SendWithRetryAsync(request);

        if (result.Success)
        {
            _logger.LogInformation(
                "Ticket resend email sent to {Recipient} for event {EventId}",
                recipientEmail, eventDetails.Id);
        }
        else
        {
            _logger.LogError(
                "Failed to send ticket resend email to {Recipient} for event {EventId}: {Error}",
                recipientEmail, eventDetails.Id, result.Error);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<EmailResult> SendRefundNotificationAsync(string recipientEmail, decimal amount, string reason, string? recipientName = null)
    {
        _logger.LogInformation(
            "Sending refund notification email to {Recipient} for amount {Amount}",
            recipientEmail, amount);

        var html = RefundNotificationTemplate.Render(amount, reason, recipientName);

        var request = new ResendEmailRequest
        {
            From = ResolvedFrom,
            To = recipientEmail,
            Subject = "Te reembolsamos tu compra",
            Html = html
        };

        var result = await SendWithRetryAsync(request);

        if (result.Success)
        {
            _logger.LogInformation(
                "Refund notification email sent to {Recipient} for amount {Amount}",
                recipientEmail, amount);
        }
        else
        {
            _logger.LogError(
                "Failed to send refund notification email to {Recipient}: {Error}",
                recipientEmail, result.Error);
        }

        return result;
    }

    /// <summary>
    /// Sends an email through Resend with in-memory exponential backoff retry.
    /// Logs every attempt and the final outcome.
    /// </summary>
    private async Task<EmailResult> SendWithRetryAsync(ResendEmailRequest request)
    {
        var maxAttempts = Math.Max(1, _options.MaxRetryAttempts);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Sending email to {Recipient}, attempt {Attempt} of {MaxAttempts}",
                    request.To, attempt, maxAttempts);

                var response = await _resendClient.SendEmailAsync(request);

                _logger.LogInformation(
                    "Email sent successfully to {Recipient} on attempt {Attempt} with Resend id {EmailId}",
                    request.To, attempt, response.Id);

                return new EmailResult { Success = true };
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(
                    ex,
                    "Email delivery failed for {Recipient} on attempt {Attempt} of {MaxAttempts}",
                    request.To, attempt, maxAttempts);

                if (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(
                        _options.RetryDelayMilliseconds * Math.Pow(2, attempt - 1));
                    _logger.LogInformation(
                        "Waiting {DelayMs}ms before retrying email to {Recipient}",
                        delay.TotalMilliseconds, request.To);
                    await Task.Delay(delay);
                }
            }
        }

        return new EmailResult
        {
            Success = false,
            Error = lastException?.Message ?? "Email delivery failed after maximum retry attempts"
        };
    }

    /// <summary>
    /// Builds the QR image src for the email body: the public QR endpoint URL
    /// (the API renders the PNG on demand from the ticket's immutable payload),
    /// with a data URI fallback. The QR is intentionally NOT uploaded to R2:
    /// the AWS SDK cannot negotiate TLS with R2 from the Render Linux container
    /// ("sslv3 alert handshake failure"), while the endpoint sidesteps storage
    /// entirely and always renders in every email client.
    /// </summary>
    private string GetQrImageSrc(Ticket ticket, string imageBase64)
    {
        var publicBaseUrl = _configuration["MercadoPago:WebhookBaseUrl"];
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return $"{publicBaseUrl.TrimEnd('/')}/api/tickets/{ticket.Id}/qr.png";
        }

        _logger.LogWarning("MercadoPago:WebhookBaseUrl not configured; QR image will be embedded as data URI");
        return $"data:image/png;base64,{imageBase64}";
    }

    /// <inheritdoc />
    public async Task<EmailResult> SendEventDateChangeNotificationAsync(
        string recipientEmail, string eventName, DateTime oldDate, DateTime newDate, string? recipientName = null)
    {
        _logger.LogInformation(
            "Sending event date change notification to {Recipient} for event '{EventName}'",
            recipientEmail, eventName);

        var refundContactEmail = _options.FromEmail;
        var html = EventDateChangeTemplate.Render(eventName, oldDate, newDate, refundContactEmail, recipientName);

        var request = new ResendEmailRequest
        {
            From = ResolvedFrom,
            To = recipientEmail,
            Subject = $"Tu evento cambió de fecha: {eventName}",
            Html = html
        };

        var result = await SendWithRetryAsync(request);

        if (result.Success)
        {
            _logger.LogInformation(
                "Event date change notification sent to {Recipient} for event '{EventName}'",
                recipientEmail, eventName);
        }
        else
        {
            _logger.LogError(
                "Failed to send event date change notification to {Recipient} for event '{EventName}': {Error}",
                recipientEmail, eventName, result.Error);
        }

        return result;
    }
}