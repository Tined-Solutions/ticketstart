using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly ILogger<EventController> _logger;

    public EventController(IEventService eventService, ILogger<EventController> logger)
    {
        _eventService = eventService;
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
            var eventDetails = await _eventService.GetEventByIdAsync(updatedEvent.Id);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting event {EventId} for user {UserId}", id, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while deleting the event" });
        }
    }

    [HttpPost("{id:guid}/image")]
    [Authorize(Policy = "EventOwnership")]
    public async Task<IActionResult> UploadEventImage(Guid id, IFormFile image)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { error = "Image file is required" });
        }

        if (!TryGetUserId(out var userId) || !TryGetUserRole(out var userRole))
        {
            return Unauthorized();
        }

        var eventDetails = await _eventService.GetEventByIdAsync(id);
        if (eventDetails == null)
        {
            return NotFound(new { error = "Event not found" });
        }

        try
        {
            using var stream = image.OpenReadStream();
            var imageUrl = await _eventService.UploadEventImageAsync(stream, image.FileName, image.ContentType);

            var updateRequest = new UpdateEventRequest
            {
                Name = eventDetails.Name,
                Description = eventDetails.Description,
                Date = eventDetails.Date,
                Location = eventDetails.Location,
                ImageUrl = imageUrl
            };

            await _eventService.UpdateEventAsync(id, updateRequest, userId, userRole);

            return Ok(new { imageUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Event not found" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image for event {EventId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while uploading the image" });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }

    private bool TryGetUserRole(out UserRole userRole)
    {
        userRole = UserRole.Organizador;
        var roleValue = User.FindFirst(ClaimTypes.Role)?.Value;
        return Enum.TryParse(roleValue, true, out userRole);
    }
}
