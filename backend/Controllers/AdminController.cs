using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Controller for system-wide admin operations.
/// All endpoints require the Admin role.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminController : TicketeraControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService adminService, IAuditLogService auditLogService, ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all user accounts in the system.
    /// Requires Admin role.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var users = await _adminService.GetAllUsersAsync(page, pageSize);
            await TryLogAuditAsync(userId, new AuditLogContext(userId, AuditActionType.ViewUsers, AuditResourceType.User, null, "Admin viewed all users"));

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users for admin {AdminId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving users" });
        }
    }

    /// <summary>
    /// Gets all events in the system regardless of ownership.
    /// Requires Admin role.
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetAllEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var events = await _adminService.GetAllEventsAsync(page, pageSize);
            await TryLogAuditAsync(userId, new AuditLogContext(userId, AuditActionType.ViewEvents, AuditResourceType.Event, null, "Admin viewed all events"));

            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all events for admin {AdminId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving events" });
        }
    }

    /// <summary>
    /// Gets audit logs for the system. Optionally filters by admin user ID.
    /// Requires Admin role.
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] Guid? userId = null)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized();
        }

        try
        {
            var logs = userId.HasValue
                ? await _auditLogService.GetLogsForUserAsync(userId.Value)
                : await _auditLogService.GetAllLogsAsync();

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for admin {AdminId}", adminId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving audit logs" });
        }
    }

    private async Task TryLogAuditAsync(Guid adminId, AuditLogContext context)
    {
        try
        {
            await _auditLogService.LogActionAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Audit logging failed for admin {AdminId} action {ActionType} resource {ResourceType} id {ResourceId}; continuing with response",
                adminId, context.Action, context.Resource, context.ResourceId);
        }
    }
}
