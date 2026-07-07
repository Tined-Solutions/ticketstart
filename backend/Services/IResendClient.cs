namespace TicketeraOnline.Api.Services;

/// <summary>
/// Client abstraction for the Resend transactional email API.
/// Allows tests to mock email delivery without making real HTTP calls.
/// </summary>
public interface IResendClient
{
    /// <summary>
    /// Sends an email through the Resend API.
    /// </summary>
    /// <param name="request">Email payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resend response with the sent email identifier</returns>
    Task<ResendEmailResponse> SendEmailAsync(ResendEmailRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Payload for a Resend email send request.
/// </summary>
public class ResendEmailRequest
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
}

/// <summary>
/// Response returned by the Resend API after sending an email.
/// </summary>
public class ResendEmailResponse
{
    public string Id { get; set; } = string.Empty;
}
