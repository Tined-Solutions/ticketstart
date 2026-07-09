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
    /// </summary>
    public Guid UserId { get; set; }

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
    DeleteEvent
}

/// <summary>
/// Types of resources that can be affected by audited actions.
/// </summary>
public enum AuditResourceType
{
    User,
    Event
}
