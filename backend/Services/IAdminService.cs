using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for admin system-wide operations.
/// Provides access to all users and events regardless of ownership.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Retrieves a paginated list of all user accounts in the system.
    /// </summary>
    Task<PagedResult<UserSummary>> GetAllUsersAsync(int page, int pageSize);

    /// <summary>
    /// Retrieves a paginated list of all events in the system regardless of organizer ownership.
    /// </summary>
    Task<PagedResult<EventSummary>> GetAllEventsAsync(int page, int pageSize);

    /// <summary>
    /// Retrieves a paginated list of all audit log entries in the system,
    /// ordered by timestamp and id descending.
    /// </summary>
    Task<PagedResult<AuditLogEntry>> GetAllLogsAsync(int page, int pageSize);

    /// <summary>
    /// Approves an event (EA-003): flips its status to <see cref="EventStatus.Approved"/>,
    /// making it buyer-visible. Any status may be approved (EA-005 — no state machine).
    /// </summary>
    /// <param name="eventId">ID of the event to approve</param>
    /// <returns>The updated event summary</returns>
    /// <exception cref="KeyNotFoundException">Event does not exist.</exception>
    Task<EventSummary> ApproveEventAsync(Guid eventId);

    /// <summary>
    /// Rejects an event (EA-004): flips its status to <see cref="EventStatus.Rejected"/>,
    /// hiding it from the public catalog. Any status may be rejected (EA-005). The
    /// optional reason is audit-only — it is never stored on the event.
    /// </summary>
    /// <param name="eventId">ID of the event to reject</param>
    /// <param name="reason">Optional rejection reason (may be null), stored in the audit detail</param>
    /// <returns>The updated event summary</returns>
    /// <exception cref="KeyNotFoundException">Event does not exist.</exception>
    Task<EventSummary> RejectEventAsync(Guid eventId, string? reason);

    /// <summary>
    /// Retrieves a paginated list of events still awaiting approval
    /// (<see cref="EventStatus.Pending"/>), oldest first.
    /// </summary>
    Task<PagedResult<EventSummary>> GetPendingEventsAsync(int page, int pageSize);

    /// <summary>
    /// Updates a user's role (AUM-001). The account row is never deleted —
    /// role editing is the only revoke mechanism. The change applies on the
    /// target's next login (the JWT role claim is frozen in the cookie).
    /// </summary>
    /// <param name="targetUserId">ID of the user whose role is changed</param>
    /// <param name="newRole">The new role</param>
    /// <returns>The updated user summary</returns>
    /// <exception cref="KeyNotFoundException">User does not exist.</exception>
    Task<UserSummary> UpdateUserRoleAsync(Guid targetUserId, UserRole newRole);
}

/// <summary>
/// Data transfer object representing a user account summary for admin views.
/// Excludes sensitive fields such as password hashes.
/// </summary>
public class UserSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Data transfer object representing an event summary for admin views.
/// </summary>
public class EventSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public Guid OrganizerId { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// EA-007: approval status, serialized as "Pending"/"Approved"/"Rejected"
    /// (per-enum <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>)
    /// so the admin panel renders the moderation badge.
    /// </summary>
    public EventStatus Status { get; set; }
}
