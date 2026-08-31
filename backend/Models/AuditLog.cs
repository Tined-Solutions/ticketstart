namespace TicketeraOnline.Api.Models;

/// <summary>
/// Represents an audit log entry for admin actions.
/// Captures who performed the action, what action was performed, and on which resource.
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Unique identifier for the audit log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the admin user who performed the action.
    /// Null for system-initiated actions (e.g., webhooks).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Human-readable identifier used when UserId is null (e.g., "System" for webhooks).
    /// Max length: 200 characters.
    /// </summary>
    public string? UserIdentifier { get; set; }

    /// <summary>
    /// Client IP address captured when the action was performed.
    /// Max length: 45 characters (supports IPv6).
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Client User-Agent header captured when the action was performed.
    /// Max length: 500 characters.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Navigation property to the User entity.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Type of action performed.
    /// </summary>
    public AuditActionType ActionType { get; set; }

    /// <summary>
    /// Type of resource affected.
    /// </summary>
    public AuditResourceType ResourceType { get; set; }

    /// <summary>
    /// Optional ID of the specific resource affected.
    /// Null for collection-level actions such as viewing all users or events.
    /// </summary>
    public Guid? ResourceId { get; set; }

    /// <summary>
    /// Optional additional details about the action.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// UTC timestamp when the action was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Types of actions that can be recorded in the audit log.
/// </summary>
public enum AuditActionType
{
    ViewUsers,
    ViewEvents,
    UpdateEvent,
    DeleteEvent,
    CreateUser,
    ProcessWebhook,
    ValidateQr,
    AddTicketStock,   // NEW (ATS-005) — ActionType varchar(100), no migration
    AddTicketType,    // NEW (ATS-005)
    RefundPurchase,   // NEW (APR-007) — varchar-stored, no migration
    ApproveEvent,     // NEW (EA-003) — varchar-stored, no migration
    RejectEvent,      // NEW (EA-004) — varchar-stored, no migration
    UpdateUserRole,   // NEW (AUM-001) — varchar-stored, no migration
    ResetPassword     // NEW (AUM-003) — varchar-stored, no migration
}

/// <summary>
/// Types of resources that can be affected by audited actions.
/// </summary>
public enum AuditResourceType
{
    User,
    Event,
    Payment,
    Ticket
}
