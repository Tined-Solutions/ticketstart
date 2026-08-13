using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

[ApiController]
[Route("api/reservations")]
public class ReservationController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly ILogger<ReservationController> _logger;

    public ReservationController(IReservationService reservationService, ILogger<ReservationController> logger)
    {
        _reservationService = reservationService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new reservation with 10-minute expiration.
    /// Validates: Requirements 4.1, 4.3, 16.2, 16.3
    /// </summary>
    /// <param name="request">Reservation creation request</param>
    /// <returns>Created reservation details</returns>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("Reservations")]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        // Validate email confirmation match (Batch 4 B4.3)
        if (!string.IsNullOrEmpty(request.PurchaserEmail) && 
            !string.Equals(request.PurchaserEmail, request.ConfirmEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "PurchaserEmail and ConfirmEmail do not match" });
        }

        // Get userId from claims if authenticated, otherwise null (guest purchase)
        var userId = GetUserId();

        try
        {
            // Create reservation using service
            var reservation = await _reservationService.CreateReservationAsync(
                userId,
                request.EventId,
                request.TicketTypeId,
                request.Quantity,
                request.PurchaserDNI,
                request.PurchaserEmail
            );

            // Map to response DTO
            var response = new ReservationResponse
            {
                Id = reservation.Id,
                EventId = reservation.EventId,
                TicketTypeId = reservation.TicketTypeId,
                Quantity = reservation.Quantity,
                PurchaserEmail = reservation.PurchaserEmail,
                ExpiresAt = reservation.ExpiresAt,
                Status = reservation.Status.ToString(),
                Token = _reservationService.GenerateReservationToken(reservation.Id)
            };

            _logger.LogInformation("Reservation {ReservationId} created successfully for user {UserId}",
                reservation.Id, userId);

            // Return 201 Created
            return Created($"/api/reservations/{reservation.Id}", response);
        }
        catch (ArgumentException ex)
        {
            // Validation errors: invalid quantity or insufficient tickets
            _logger.LogWarning(ex, "Validation error creating reservation for user {UserId}", userId);
            
            // Check if it's an insufficient tickets error
            if (ex.Message.Contains("Insufficient tickets"))
            {
                return Conflict(new { error = ex.Message });
            }
            
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            // Event or ticket type not found
            _logger.LogWarning(ex, "Resource not found while creating reservation for user {UserId}", userId);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Concurrency conflicts
            _logger.LogWarning(ex, "Concurrency conflict creating reservation for user {UserId}", userId);
            return Conflict(new { error = ex.Message });
        }
        catch (EventExpiredException ex)
        {
            // EHE-004/ADR-5: expired event → 409 RFC 7807 ProblemDetails.
            // MUST stay ABOVE the generic catch below, otherwise it would be swallowed as a 500.
            _logger.LogWarning(ex, "Event has already started for user {UserId}", userId);
            return Problem(
                detail: "This event has already started and is no longer purchasable.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Event has already started",
                type: "event-expired");
        }
        catch (Exception ex)
        {
            // Unexpected errors
            _logger.LogError(ex, "Unexpected error creating reservation for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while creating the reservation" });
        }
    }

    /// <summary>
    /// Updates purchaser data (DNI, email) on an existing active reservation.
    /// Does NOT affect ticket stock — the reservation already holds the tickets.
    /// Requires a valid reservation token for authorization.
    /// </summary>
    /// <param name="id">Reservation identifier</param>
    /// <param name="request">Updated purchaser data with reservation token</param>
    /// <returns>Updated reservation details</returns>
    [HttpPatch("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateReservation(Guid id, [FromBody] UpdateReservationRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        try
        {
            var reservation = await _reservationService.UpdateReservationAsync(id, request);

            var response = new ReservationResponse
            {
                Id = reservation.Id,
                EventId = reservation.EventId,
                TicketTypeId = reservation.TicketTypeId,
                Quantity = reservation.Quantity,
                PurchaserEmail = reservation.PurchaserEmail,
                ExpiresAt = reservation.ExpiresAt,
                Status = reservation.Status.ToString(),
                Token = request.Token // Keep the same token — it's still valid
            };

            _logger.LogInformation("Reservation {ReservationId} updated successfully", id);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Reservation {ReservationId} not found for update", id);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Reservation {ReservationId} cannot be updated", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Invalid token for reservation {ReservationId}", id);
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error updating reservation {ReservationId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating reservation {ReservationId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while updating the reservation" });
        }
    }

    /// <summary>
    /// Extracts user identifier from JWT claims if authenticated.
    /// Returns null for guest users (unauthenticated).
    /// </summary>
    /// <returns>User identifier or null</returns>
    private Guid? GetUserId()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (Guid.TryParse(userIdValue, out var userId))
        {
            return userId;
        }
        
        return null;
    }
}
