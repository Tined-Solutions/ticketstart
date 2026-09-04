namespace TicketeraOnline.Api.Services;

/// <summary>
/// Configuration options for the Brevo transactional email service (API v3).
///
/// Staging uses Brevo instead of Resend because Brevo verifies the sender by
/// code (no DNS/domain required), while Resend's sandbox (@resend.dev) is
/// blocked by a production gate and cannot deliver to real recipients.
/// </summary>
public class BrevoOptions
{
    public const string SectionName = "Brevo";

    /// <summary>
    /// Brevo API key (Settings → SMTP and API → API Keys).
    /// Sent as the `api-key` header on every request.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Verified sender email address (must exist in Brevo's Senders list).
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the sender shown in recipients' inboxes.
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of delivery attempts for a single email.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retry attempts in milliseconds. The actual delay
    /// grows exponentially with the attempt number (1x, 2x, 4x, ...).
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;
}