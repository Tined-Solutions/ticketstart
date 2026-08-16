using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for transactional email delivery.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a ticket confirmation email with embedded QR codes, event details,
    /// and purchase confirmation information.
    /// Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6
    /// </summary>
    /// <param name="recipientEmail">Purchaser email address</param>
    /// <param name="tickets">Confirmed tickets to include in the email</param>
    /// <param name="eventDetails">Event associated with the tickets</param>
    /// <param name="recipientName">Optional recipient name used to personalize the greeting</param>
    /// <returns>Email delivery result</returns>
    Task<EmailResult> SendTicketEmailAsync(string recipientEmail, IEnumerable<Ticket> tickets, Event eventDetails, string? recipientName = null);

    /// <summary>
    /// Sends a ticket resend email with embedded QR codes. Identical to
    /// SendTicketEmailAsync except the subject uses Spanish copy.
    /// </summary>
    /// <param name="recipientEmail">Purchaser email address</param>
    /// <param name="tickets">Tickets to include in the email</param>
    /// <param name="eventDetails">Event associated with the tickets</param>
    /// <param name="recipientName">Optional recipient name used to personalize the greeting</param>
    /// <returns>Email delivery result</returns>
    Task<EmailResult> SendResendEmailAsync(string recipientEmail, IEnumerable<Ticket> tickets, Event eventDetails, string? recipientName = null);

    /// <summary>
    /// Sends a refund notification email explaining the refund reason and amount.
    /// Validates: Requirement 12.4
    /// </summary>
    /// <param name="recipientEmail">Purchaser email address</param>
    /// <param name="amount">Refund amount</param>
    /// <param name="reason">Human-readable refund reason</param>
    /// <param name="recipientName">Optional recipient name used to personalize the greeting</param>
    /// <returns>Email delivery result</returns>
    Task<EmailResult> SendRefundNotificationAsync(string recipientEmail, decimal amount, string reason, string? recipientName = null);

    /// <summary>
    /// Sends an event date change notification email to a ticket buyer.
    /// Includes the event name, old date, new date, and refund-request contact.
    /// Validates: Requirement EDC-003
    /// </summary>
    /// <param name="recipientEmail">Buyer email address</param>
    /// <param name="eventName">Name of the event whose date changed</param>
    /// <param name="oldDate">Previous event date</param>
    /// <param name="newDate">Updated event date</param>
    /// <param name="recipientName">Optional recipient name used to personalize the greeting</param>
    /// <returns>Email delivery result</returns>
    Task<EmailResult> SendEventDateChangeNotificationAsync(string recipientEmail, string eventName, DateTime oldDate, DateTime newDate, string? recipientName = null);
}

/// <summary>
/// Result of an email delivery attempt.
/// </summary>
public class EmailResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}