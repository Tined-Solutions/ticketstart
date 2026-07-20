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
    private readonly ResendOptions _options;

    public EmailService(
        IResendClient resendClient,
        ITicketService ticketService,
        ILogger<EmailService> logger,
        IOptions<ResendOptions> options)
    {
        _resendClient = resendClient;
        _ticketService = ticketService;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<EmailResult> SendTicketEmailAsync(string recipientEmail, IEnumerable<Ticket> tickets, Event eventDetails)
    {
        _logger.LogInformation(
            "Sending ticket confirmation email to {Recipient} for event {EventId}",
            recipientEmail, eventDetails.Id);

        var ticketList = tickets.ToList();
        var ticketImages = new List<(Ticket Ticket, string ImageBase64)>();

        foreach (var ticket in ticketList)
        {
            var imageBase64 = _ticketService.GenerateQRCodeImage(ticket.QRCodeData);
            ticketImages.Add((ticket, imageBase64));
        }

        var totalAmount = ticketList.Sum(t => t.TicketType?.Price ?? 0m);
        var html = TicketConfirmationTemplate.Render(
            eventDetails,
            ticketImages,
            totalAmount,
            recipientEmail);

        var request = new ResendEmailRequest
        {
            From = _options.FromEmail,
            To = recipientEmail,
            Subject = $"Your tickets for {eventDetails.Name}",
            Html = html
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
    public async Task<EmailResult> SendResendEmailAsync(string recipientEmail, IEnumerable<Ticket> tickets, Event eventDetails)
    {
        _logger.LogInformation(
            "Sending ticket resend email to {Recipient} for event {EventId}",
            recipientEmail, eventDetails.Id);

        var ticketList = tickets.ToList();
        var ticketImages = new List<(Ticket Ticket, string ImageBase64)>();

        foreach (var ticket in ticketList)
        {
            var imageBase64 = _ticketService.GenerateQRCodeImage(ticket.QRCodeData);
            ticketImages.Add((ticket, imageBase64));
        }

        var totalAmount = ticketList.Sum(t => t.TicketType?.Price ?? 0m);
        var html = TicketConfirmationTemplate.Render(
            eventDetails,
            ticketImages,
            totalAmount,
            recipientEmail);

        var request = new ResendEmailRequest
        {
            From = _options.FromEmail,
            To = recipientEmail,
            Subject = $"Reenvío de tus entradas para {eventDetails.Name}",
            Html = html
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
    public async Task<EmailResult> SendRefundNotificationAsync(string recipientEmail, decimal amount, string reason)
    {
        _logger.LogInformation(
            "Sending refund notification email to {Recipient} for amount {Amount}",
            recipientEmail, amount);

        var html = RefundNotificationTemplate.Render(amount, reason);

        var request = new ResendEmailRequest
        {
            From = _options.FromEmail,
            To = recipientEmail,
            Subject = "Refund notification",
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
}
