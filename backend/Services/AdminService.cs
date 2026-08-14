using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for admin system-wide operations.
/// Provides read access to all users and events regardless of ownership.
/// </summary>
public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminService> _logger;

    public AdminService(ApplicationDbContext context, ILogger<AdminService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of all user accounts in the system, ordered by creation date descending.
    /// Password hashes are intentionally excluded from the returned summaries.
    /// </summary>
    public async Task<PagedResult<UserSummary>> GetAllUsersAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, Math.Min(pageSize, 200));

        _logger.LogInformation("Retrieving user accounts for admin view (page {Page}, pageSize {PageSize})", page, pageSize);

        var total = await _context.Users.AsNoTracking().CountAsync();
        var users = await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserSummary
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        _logger.LogInformation("Retrieved {UserCount} user accounts for admin view", users.Count);

        return new PagedResult<UserSummary>
        {
            Items = users,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Retrieves a paginated list of all events in the system regardless of organizer ownership,
    /// ordered by date ascending.
    /// </summary>
    public async Task<PagedResult<EventSummary>> GetAllEventsAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, Math.Min(pageSize, 200));

        _logger.LogInformation("Retrieving events for admin view (page {Page}, pageSize {PageSize})", page, pageSize);

        var total = await _context.Events.AsNoTracking().CountAsync();
        var events = await _context.Events
            .AsNoTracking()
            .OrderBy(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventSummary
            {
                Id = e.Id,
                Name = e.Name,
                Date = e.Date,
                Location = e.Location,
                OrganizerId = e.OrganizerId,
                CreatedAt = e.CreatedAt,
                Status = e.Status
            })
            .ToListAsync();

        _logger.LogInformation("Retrieved {EventCount} events for admin view", events.Count);

        return new PagedResult<EventSummary>
        {
            Items = events,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Approves an event (EA-003): any status may be approved (EA-005). The status
    /// flip is the whole operation — a Rejected event re-enters the public catalog.
    /// </summary>
    public async Task<EventSummary> ApproveEventAsync(Guid eventId)
    {
        var eventEntity = await _context.Events.FindAsync(eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        eventEntity.Status = EventStatus.Approved;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin approved event {EventId}", eventId);
        return ToSummary(eventEntity);
    }

    /// <summary>
    /// Rejects an event (EA-004): any status may be rejected (EA-005). The optional
    /// reason is audit-only — the controller stores it in the audit detail; it is
    /// never persisted on the event.
    /// </summary>
    public async Task<EventSummary> RejectEventAsync(Guid eventId, string? reason)
    {
        var eventEntity = await _context.Events.FindAsync(eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        eventEntity.Status = EventStatus.Rejected;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin rejected event {EventId}", eventId);
        return ToSummary(eventEntity);
    }

    /// <summary>
    /// Lists events awaiting approval (EA-003): Pending events only, oldest first,
    /// paginated with the same clamps as the other admin list endpoints.
    /// </summary>
    public async Task<PagedResult<EventSummary>> GetPendingEventsAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, Math.Min(pageSize, 200));

        _logger.LogInformation("Retrieving pending events for admin view (page {Page}, pageSize {PageSize})", page, pageSize);

        var total = await _context.Events.AsNoTracking().CountAsync(e => e.Status == EventStatus.Pending);
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventSummary
            {
                Id = e.Id,
                Name = e.Name,
                Date = e.Date,
                Location = e.Location,
                OrganizerId = e.OrganizerId,
                CreatedAt = e.CreatedAt,
                Status = e.Status
            })
            .ToListAsync();

        return new PagedResult<EventSummary>
        {
            Items = events,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static EventSummary ToSummary(Event eventEntity) => new()
    {
        Id = eventEntity.Id,
        Name = eventEntity.Name,
        Date = eventEntity.Date,
        Location = eventEntity.Location,
        OrganizerId = eventEntity.OrganizerId,
        CreatedAt = eventEntity.CreatedAt,
        Status = eventEntity.Status
    };

    /// <summary>
    /// Retrieves a paginated list of all audit log entries in the system,
    /// ordered by timestamp descending then by id descending.
    /// </summary>
    public async Task<PagedResult<AuditLogEntry>> GetAllLogsAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, Math.Min(pageSize, 200));

        _logger.LogInformation("Retrieving audit logs for admin view (page {Page}, pageSize {PageSize})", page, pageSize);

        var total = await _context.AuditLogs.AsNoTracking().CountAsync();
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogEntry
            {
                Id = l.Id,
                UserId = l.UserId,
                UserIdentifier = l.UserIdentifier,
                ActionType = l.ActionType,
                ResourceType = l.ResourceType,
                ResourceId = l.ResourceId,
                Details = l.Details,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                Timestamp = l.Timestamp
            })
            .ToListAsync();

        _logger.LogInformation("Retrieved {LogCount} audit log entries for admin view", logs.Count);

        return new PagedResult<AuditLogEntry>
        {
            Items = logs,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
