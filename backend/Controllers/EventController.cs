using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventController : TicketeraControllerBase
{
    private readonly IEventService _eventService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<EventController> _logger;

    public EventController(IEventService eventService, IAuditLogService auditLogService, ILogger<EventController> logger)
    {
        _eventService = eventService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllEvents()
    {
        var events = await _eventService.GetAllPublishedEventsAsync();
        return Ok(events);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEvent(Guid id)
    {
        var eventDetails = await _eventService.GetEventByIdAsync(id);

        if (eventDetails == null)
        {
            return NotFound(new { error = "Event not found" });
        }

        // EHE-003/EA-001: the public detail only surfaces Approved events. The
        // status filter lives HERE (post-read), not inside GetEventByIdAsync —
        // POST-201 create and the /manage variant depend on it unfiltered (D-2).
        if (eventDetails.Status != EventStatus.Approved)
        {
            return NotFound(new { error = "Event not found" });
        }

        return Ok(eventDetails);
    }

    /// <summary>
    /// Scannable event list for the staff scan chooser (EHE-007): future events
    /// plus events ended within the 24h QR validation window. Role-gated:
    /// Staff/Organizador/Admin (organizers scan as staff).
    /// </summary>
    [HttpGet("manage")]
    [Authorize(Policy = "RequireScanAccessRole")]
    public async Task<IActionResult> GetAllEventsForManagement()
    {
        var events = await _eventService.GetScannableEventsAsync();
        return Ok(events);
    }

    /// <summary>
    /// Unfiltered event detail for the organizer edit page (EHE-006). Returns the
    /// event regardless of expiry. Role-gated: event owner or Admin only.
    /// </summary>
    [HttpGet("{id:guid}/manage")]
    [Authorize(Policy = "EventOwnership")]
    public async Task<IActionResult> GetEventForManagement(Guid id)
    {
        var eventDetails = await _eventService.GetEventByIdAsync(id, includeExpired: true);

        if (eventDetails == null)
        {
            return NotFound(new { error = "Event not found" });
        }

        return Ok(eventDetails);
    }

    [HttpPost]
    [Authorize(Policy = "RequireOrganizadorRole")]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var createdEvent = await _eventService.CreateEventAsync(request, userId);
            var eventDetails = await _eventService.GetEventByIdAsync(createdEvent.Id);

            return CreatedAtAction(nameof(GetEvent), new { id = createdEvent.Id }, eventDetails);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating event for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while creating the event" });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "EventOwnership")]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        if (!TryGetUserId(out var userId) || !TryGetUserRole(out var userRole))
        {
            return Unauthorized();
        }

        try
        {
            var updatedEvent = await _eventService.UpdateEventAsync(id, request, userId, userRole);
            // EHE-006: includeExpired — an organizer editing a PAST event must get
            // the result back, not a null → 500. EventOwnership already gated access.
            var eventDetails = await _eventService.GetEventByIdAsync(updatedEvent.Id, includeExpired: true);

            if (userRole == UserRole.Admin)
            {
                await TryLogAuditAsync(userId, new AuditLogContext(userId, AuditActionType.UpdateEvent, AuditResourceType.Event, id, "Admin updated event"));
            }

            return Ok(eventDetails);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Event not found" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        // PEM-002/ADR-5: a finalized event is immutable — 409 RFC 7807
        // (type "event-finalized") BEFORE any save/audit/notification.
        catch (EventFinalizedException)
        {
            return Problem(
                detail: "This event has already finished and can no longer be modified.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Event has already finished",
                type: "event-finalized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating event {EventId} for user {UserId}", id, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while updating the event" });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "EventOwnership")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        if (!TryGetUserId(out var userId) || !TryGetUserRole(out var userRole))
        {
            return Unauthorized();
        }

        try
        {
            await _eventService.DeleteEventAsync(id, userId, userRole);

            // ED-001: success now implies an Admin requester (the service guard
            // rejects everyone else), so this audit branch is the only reachable
            // delete path. Kept conditional defensively in case owner-delete is
            // ever restored — organizer deletes were already unaudited before ED-001.
            if (userRole == UserRole.Admin)
            {
                await TryLogAuditAsync(userId, new AuditLogContext(userId, AuditActionType.DeleteEvent, AuditResourceType.Event, id, "Admin deleted event"));
            }

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Event not found" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        // PEM-002/ADR-5: a finalized event is immutable — 409 RFC 7807.
        catch (EventFinalizedException)
        {
            return Problem(
                detail: "This event has already finished and can no longer be modified.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Event has already finished",
                type: "event-finalized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting event {EventId} for user {UserId}", id, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while deleting the event" });
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

    private bool TryGetUserRole(out UserRole userRole)
    {
        userRole = UserRole.Organizador;
        var roleValue = User.FindFirst(ClaimTypes.Role)?.Value;
        return Enum.TryParse(roleValue, true, out userRole);
    }
}
