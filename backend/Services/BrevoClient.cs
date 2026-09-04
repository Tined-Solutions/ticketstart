using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Brevo transactional email client (API v3: POST /v3/smtp/email).
///
/// Implements the same <see cref="IResendClient"/> contract as the (now
/// inactive) Resend client so <see cref="EmailService"/> stays
/// provider-agnostic. EmailService renders templates and retries; this class
/// only maps the transport request to Brevo's payload shape and sends it.
///
/// Known limitation: Brevo does not render inline images referenced by
/// Content-ID reliably, so QR images arrive as downloadable attachments
/// instead of being embedded in the email body.
/// </summary>
public class BrevoClient : IResendClient
{
    private static readonly Uri BrevoApiBase = new("https://api.brevo.com/v3/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<BrevoClient> _logger;

    public BrevoClient(HttpClient httpClient, IOptions<BrevoOptions> options, ILogger<BrevoClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = BrevoApiBase;
        _httpClient.DefaultRequestHeaders.Add("api-key", options.Value.ApiKey);
    }

    /// <inheritdoc />
    public async Task<ResendEmailResponse> SendEmailAsync(ResendEmailRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending email to {Recipient} via Brevo", request.To);

        var (senderName, senderEmail) = ParseFrom(request.From);
        var payload = new BrevoPayload
        {
            Sender = new BrevoSender { Name = senderName, Email = senderEmail },
            To = [new BrevoRecipient { Email = request.To }],
            Subject = request.Subject,
            HtmlContent = request.Html,
            Attachments = request.Attachments?.Select(a => new BrevoAttachment
            {
                Name = a.Filename,
                Content = a.Content,
                // ContentId intentionally omitted: Brevo does not render inline
                // CID images, so QR PNGs travel as plain downloadable attachments
                // (the HTML body embeds the QR via data URI instead).
            }).ToList(),
        };

        var response = await _httpClient.PostAsJsonAsync("smtp/email", payload, JsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Brevo API returned {StatusCode} for {Recipient}. Body: {ErrorBody}",
                (int)response.StatusCode, request.To, errorBody);
            throw new HttpRequestException(
                $"Brevo API error {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<BrevoSendResponse>(JsonOptions, cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Brevo returned an empty response body");
        }

        _logger.LogDebug("Brevo accepted email with id {MessageId}", result.MessageId);
        return new ResendEmailResponse { Id = result.MessageId };
    }

    /// <summary>
    /// Parses the RFC-5322 "Name &lt;email&gt;" form produced by EmailService
    /// into Brevo's separate sender name/email fields. Falls back to treating
    /// the whole string as an email when no angle-bracket address is present.
    /// </summary>
    private static (string Name, string Email) ParseFrom(string from)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            from, @"^(?:(.+?)\s*)?<([^>]+)>$");
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim().Trim('"'), match.Groups[2].Value.Trim());
        }
        return (string.Empty, from.Trim());
    }

    private sealed class BrevoPayload
    {
        public BrevoSender Sender { get; set; } = new();
        public List<BrevoRecipient> To { get; set; } = new();
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<BrevoAttachment>? Attachments { get; set; }
    }

    private sealed class BrevoSender
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    private sealed class BrevoRecipient
    {
        public string Email { get; set; } = string.Empty;
    }

    private sealed class BrevoAttachment
    {
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ContentId { get; set; }
    }

    private sealed class BrevoSendResponse
    {
        public string MessageId { get; set; } = string.Empty;
    }
}