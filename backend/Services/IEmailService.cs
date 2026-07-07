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
    /// <returns>Email delivery result</returns>
    Task<EmailResult> SendTicketEmailAsync(string recipientEmail, IEnumerable<Ticket> tickets, Event eventDetails);

    /// <summary>
    /// Sends a refund notification email explaining the refund reason and amount.
    /// Validates: Requirement 12.4
    /// </summary>
    /// <param name="recipientEmail">Purchaser email address</param>
    /// <param name="amount">Refund amount</param>
    /// <param name="reason">Human-readable refund reason</param>
    /// <returns>Email delivery result</returns>
    Task<EmailResult> SendRefundNotificationAsync(string recipientEmail, decimal amount, string reason);
}

/// <summary>
/// Result of an email delivery attempt.
/// </summary>
public class EmailResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
