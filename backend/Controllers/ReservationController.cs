using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

            // Return 201 Created with location header
            return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, response);
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
        catch (Exception ex)
        {
            // Unexpected errors
            _logger.LogError(ex, "Unexpected error creating reservation for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while creating the reservation" });
        }
    }

    /// <summary>
    /// Retrieves reservation details by identifier.
    /// Validates: Requirements 4.3, 16.2, 16.3
    /// </summary>
    /// <param name="id">Reservation identifier</param>
    /// <returns>Reservation details</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReservation(Guid id)
    {
        try
        {
            // Retrieve reservation with related entities
            var reservation = await _reservationService.GetReservationByIdAsync(id);

            if (reservation == null)
            {
                _logger.LogWarning("Reservation {ReservationId} not found", id);
                return NotFound(new { error = "Reservation not found" });
            }

            // Map to response DTO with related entities
            var response = new ReservationResponse
            {
                Id = reservation.Id,
                EventId = reservation.EventId,
                TicketTypeId = reservation.TicketTypeId,
                Quantity = reservation.Quantity,
                PurchaserEmail = reservation.PurchaserEmail,
                ExpiresAt = reservation.ExpiresAt,
                Status = reservation.Status.ToString(),
                Event = reservation.Event != null ? new EventResponse
                {
                    Id = reservation.Event.Id,
                    Name = reservation.Event.Name,
                    Description = reservation.Event.Description,
                    Date = reservation.Event.Date,
                    Location = reservation.Event.Location,
                    ImageUrl = reservation.Event.ImageUrl
                } : null,
                TicketType = reservation.TicketType != null ? new TicketTypeResponse
                {
                    Id = reservation.TicketType.Id,
                    Name = reservation.TicketType.Name,
                    Price = reservation.TicketType.Price,
                    Quantity = reservation.TicketType.Quantity
                } : null
            };

            _logger.LogInformation("Reservation {ReservationId} retrieved successfully", id);

            return Ok(response);
        }
        catch (Exception ex)
        {
            // Unexpected errors
            _logger.LogError(ex, "Unexpected error retrieving reservation {ReservationId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while retrieving the reservation" });
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
