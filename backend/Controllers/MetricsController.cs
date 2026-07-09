using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Controller for organizer dashboard metrics.
/// Provides endpoints to retrieve event-level and organizer-level metrics.
/// </summary>
[ApiController]
[Route("api/metrics")]
public class MetricsController : TicketeraControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(IMetricsService metricsService, ILogger<MetricsController> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets metrics for a single event.
    /// Requires the user to be the event owner or an Admin.
    /// </summary>
    [HttpGet("events/{id:guid}")]
    [Authorize(Policy = "EventOwnership")]
    public async Task<IActionResult> GetEventMetrics(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var metrics = await _metricsService.GetEventMetricsAsync(id);

            if (metrics == null)
            {
                return NotFound(new { error = "Event not found" });
            }

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics for event {EventId} by user {UserId}", id, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving event metrics" });
        }
    }

    /// <summary>
    /// Gets metrics for all events owned by the authenticated organizer.
    /// Requires Organizador or Admin role.
    /// </summary>
    [HttpGet("organizer")]
    [Authorize(Policy = "RequireOrganizadorRole")]
    public async Task<IActionResult> GetOrganizerMetrics()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var metrics = await _metricsService.GetOrganizerMetricsAsync(userId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organizer metrics for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving organizer metrics" });
        }
    }
}
