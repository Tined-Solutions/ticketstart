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
                CreatedAt = e.CreatedAt
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
}
