using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Event-agnostic image upload (EIM-002). The route accepts NO event identifier
/// and performs no event lookup or ownership check — it only validates the file
/// and stores it under a fresh <c>events/{{guid}}.{{ext}}</c> key (EIM-007: an
/// organizer cannot use this endpoint to target a specific event's image).
/// Attaching the returned <c>imageUrl</c> to an event still flows through
/// POST /events (creator becomes owner) or PUT /events/{id} (EventOwnership +
/// EnsureMutable, both untouched).
/// </summary>
[ApiController]
[Route("api/uploads")]
public class UploadsController : TicketeraControllerBase
{
    private readonly IEventService _eventService;
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(IEventService eventService, ILogger<UploadsController> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads an event image without touching any event row. Multipart field:
    /// <c>image</c>. Returns 200 <c>{ "imageUrl": "…" }</c> on success.
    /// </summary>
    [HttpPost("event-image")]
    [Authorize(Policy = "RequireOrganizadorRole")] // Organizador + Admin (EIM-002)
    [EnableRateLimiting("EventImageUpload")]       // 10/min per client (JD-C2)
    public async Task<IActionResult> UploadEventImage(IFormFile image)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { error = "Image file is required" });
        }

        try
        {
            using var stream = image.OpenReadStream();
            var imageUrl = await _eventService.UploadEventImageAsync(stream, image.FileName, image.ContentType);
            return Ok(new { imageUrl });
        }
        catch (ArgumentException ex)
        {
            // MIME ∉ {jpeg,png,webp} or size > 5 MB — validation failure, no R2 object created
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // R2 configuration missing or transport failure — 500, no retry semantics
            _logger.LogError(ex, "Error uploading image via /api/uploads/event-image");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while uploading the image" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading image via /api/uploads/event-image");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while uploading the image" });
        }
    }
}