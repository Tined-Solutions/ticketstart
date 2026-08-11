using System.ComponentModel.DataAnnotations.Schema;

namespace TicketeraOnline.Api.Models;

/// <summary>
/// Tracks a failed email send that should be retried.
/// Created by the webhook handler when SendTicketEmailAsync fails
/// after a successful payment commit. Retried via the admin endpoint.
/// </summary>
public class PendingEmailSend : IRetryableEmailRow
{
    public Guid Id { get; set; }

    /// <summary>
    /// The reservation associated with the tickets.
    /// </summary>
    public Guid ReservationId { get; set; }

    /// <summary>
    /// Mercado Pago payment ID from the webhook.
    /// </summary>
    public string PaymentId { get; set; } = string.Empty;

    /// <summary>
    /// Purchaser email address.
    /// </summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// Ticket IDs to include in the retry email. Npgsql uuid[] column.
    /// </summary>
    [Column(TypeName = "uuid[]")]
    public List<Guid> TicketIds { get; set; } = new();

    /// <summary>
    /// Error message from the most recent failed attempt.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Number of send attempts so far.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Maximum retry attempts before marking as exhausted.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Current status: pending, sent, exhausted.
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Timestamp of the most recent retry attempt.
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public Reservation Reservation { get; set; } = null!;
}
