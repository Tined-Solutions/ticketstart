namespace TicketeraOnline.Api.Services;

/// <summary>
/// Configuration options for the Resend email delivery service.
/// </summary>
public class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>
    /// Resend API key used to authenticate email requests.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Default sender email address (must be verified in Resend).
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

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
