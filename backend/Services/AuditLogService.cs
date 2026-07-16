using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for writing and querying admin audit logs.
/// Persists entries to the database via Entity Framework Core.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(ApplicationDbContext context, ILogger<AuditLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Logs an admin action by adding an AuditLog entity to the database.
    /// Audit writes are best-effort: failures are logged and do not propagate to callers.
    /// </summary>
    public async Task LogActionAsync(AuditLogContext context)
    {
        var logEntry = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = context.UserId,
            UserIdentifier = context.UserIdentifier,
            ActionType = context.Action,
            ResourceType = context.Resource,
            ResourceId = context.ResourceId,
            Details = context.Details,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            _context.AuditLogs.Add(logEntry);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to write audit log for user {UserIdentifier} action {ActionType} resource {ResourceType} id {ResourceId}",
                context.UserIdentifier ?? context.UserId?.ToString(), context.Action, context.Resource, context.ResourceId);
        }
    }

    /// <summary>
    /// Retrieves all audit log entries ordered by timestamp descending, then by id descending.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> GetAllLogsAsync()
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.Id)
            .Select(l => MapToEntry(l))
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves audit log entries for a specific admin user ordered by timestamp descending, then by id descending.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> GetLogsForUserAsync(Guid userId)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.Id)
            .Select(l => MapToEntry(l))
            .ToListAsync();
    }

    private static AuditLogEntry MapToEntry(AuditLog log)
    {
        return new AuditLogEntry
        {
            Id = log.Id,
            UserId = log.UserId,
            UserIdentifier = log.UserIdentifier,
            ActionType = log.ActionType,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            Details = log.Details,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            Timestamp = log.Timestamp
        };
    }
}
