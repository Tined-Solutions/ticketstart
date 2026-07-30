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
/// An attachment to include in a Resend email, either as a regular
/// attachment or as an inline image referenced by Content-ID (CID).
/// </summary>
public class ResendAttachment
{
    /// <summary>File name shown in the email client (e.g. "qr-ticket.png").</summary>
    [System.Text.Json.Serialization.JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    /// <summary>Base64-encoded file content (without data URI prefix).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type (e.g. "image/png"). Optional — Resend infers it
    /// from the filename extension when omitted.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("content_type")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; set; }

    /// <summary>
    /// Content-ID used to reference this attachment from the HTML body
    /// via &lt;img src="cid:{ContentId}" /&gt;. Resend maps this to the
    /// Content-ID MIME header so inline images render in all email clients.
    /// Must be &lt; 128 characters and unique within the email.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("content_id")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentId { get; set; }
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

    /// <summary>
    /// Optional attachments, including inline images referenced by CID.
    /// When null (refund emails, etc.), the field is omitted from the JSON
    /// payload entirely so the Resend API never sees a null attachments array.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<ResendAttachment>? Attachments { get; set; }
}

/// <summary>
/// Response returned by the Resend API after sending an email.
/// </summary>
public class ResendEmailResponse
{
    public string Id { get; set; } = string.Empty;
}
