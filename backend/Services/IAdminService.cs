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
}

/// <summary>
/// Data transfer object representing a user account summary for admin views.
/// Excludes sensitive fields such as password hashes.
/// </summary>
public class UserSummary
{
    public Guid Id { get; set; }
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
}
