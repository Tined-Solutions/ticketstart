namespace TicketeraOnline.Api.Models;

/// <summary>
/// Represents an email notification queued for delivery after a change to an
/// event (date move, location change, etc.). Dispatched asynchronously by the
/// EventNotificationDispatchService background worker.
///
/// Implements IRetryableEmailRow so the shared RetryableEmailSender state machine
/// can manage delivery attempts without per-entity duplication.
/// </summary>
public class EventNotification : IRetryableEmailRow
{
    public Guid Id { get; set; }

    /// <summary>
    /// The event that changed.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Denormalized event name captured at enqueue time so the dispatch service
    /// does not need to join back to the Events table.
    /// </summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// Discriminator for future event-change types (DateChange, LocationChange, etc.).
    /// </summary>
    public string NotificationType { get; set; } = "DateChange";

    /// <summary>
    /// Old event date captured before the update. Null when notification type
    /// is not DateChange.
    /// </summary>
    public DateTime? OldDate { get; set; }

    /// <summary>
    /// New event date from the update request.
    /// </summary>
    public DateTime? NewDate { get; set; }

    /// <summary>
    /// Purchaser email address from the ticket.
    /// </summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// Recipient display name captured at enqueue time for personalized greetings.
    /// Nullable: falls back to a generic greeting when no name is available.
    /// </summary>
    public string? RecipientName { get; set; }

    // ---- IRetryableEmailRow ----

    /// <inheritdoc />
    public string Status { get; set; } = "pending";

    /// <inheritdoc />
    public int Attempts { get; set; } = 0;

    /// <inheritdoc />
    public int MaxAttempts { get; set; } = 5;

    /// <inheritdoc />
    public string? LastError { get; set; }

    /// <inheritdoc />
    public DateTime? LastAttemptAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public Event? Event { get; set; }
}
