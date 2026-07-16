using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for writing and querying admin audit logs.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Logs an admin action for audit purposes.
    /// </summary>
    Task LogActionAsync(AuditLogContext context);

    /// <summary>
    /// Retrieves all audit log entries ordered by timestamp descending, then by id descending.
    /// </summary>
    Task<IEnumerable<AuditLogEntry>> GetAllLogsAsync();

    /// <summary>
    /// Retrieves audit log entries for a specific admin user ordered by timestamp descending, then by id descending.
    /// </summary>
    /// <param name="userId">ID of the admin user</param>
    Task<IEnumerable<AuditLogEntry>> GetLogsForUserAsync(Guid userId);
}

/// <summary>
/// Parameter object describing an admin action to be recorded in the audit log.
/// </summary>
public record AuditLogContext(
    Guid? UserId,
    AuditActionType Action,
    AuditResourceType Resource,
    Guid? ResourceId = null,
    string? Details = null,
    string? UserIdentifier = null,
    string? IpAddress = null,
    string? UserAgent = null);

/// <summary>
/// Data transfer object representing an audit log entry.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserIdentifier { get; set; }
    public AuditActionType ActionType { get; set; }
    public AuditResourceType ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}
