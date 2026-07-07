using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for managing ticket reservations with expiration and concurrency control.
/// Handles temporary ticket holds, inventory management, and reservation lifecycle.
/// </summary>
public interface IReservationService
{
    /// <summary>
    /// Creates a new reservation with 10-minute expiration.
    /// Decrements ticket inventory atomically using database transactions and optimistic concurrency control.
    /// Validates: Requirements 4.1, 4.2, 4.3, 4.4, 12.6
    /// </summary>
    /// <param name="userId">Optional user identifier (nullable for guest purchases)</param>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ticketTypeId">Ticket type identifier</param>
    /// <param name="quantity">Number of tickets to reserve</param>
    /// <param name="purchaserDNI">Purchaser DNI (required, max 50 characters)</param>
    /// <returns>Created reservation with identifier</returns>
    /// <exception cref="ArgumentException">Thrown when quantity is invalid, DNI is invalid, or insufficient tickets available</exception>
    /// <exception cref="KeyNotFoundException">Thrown when event or ticket type not found</exception>
    /// <exception cref="InvalidOperationException">Thrown on concurrency conflicts</exception>
    Task<Reservation> CreateReservationAsync(Guid? userId, Guid eventId, Guid ticketTypeId, int quantity, string purchaserDNI);

    /// <summary>
    /// Validates if a reservation exists, is active, and not expired.
    /// Validates: Requirement 4.4
    /// </summary>
    /// <param name="reservationId">Reservation identifier</param>
    /// <returns>True if reservation is valid and active, false otherwise</returns>
    Task<bool> ValidateReservationAsync(Guid reservationId);

    /// <summary>
    /// Releases all expired active reservations and restores ticket inventory.
    /// Validates: Requirement 4.5
    /// </summary>
    /// <returns>Number of reservations released</returns>
    Task<int> ReleaseExpiredReservationsAsync();

    /// <summary>
    /// Confirms a reservation after successful payment.
    /// Marks reservation as Confirmed (called after payment processing).
    /// </summary>
    /// <param name="reservationId">Reservation identifier</param>
    /// <returns>Updated reservation</returns>
    /// <exception cref="KeyNotFoundException">Thrown when reservation not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when reservation is not active or expired</exception>
    Task<Reservation> ConfirmReservationAsync(Guid reservationId);

    /// <summary>
    /// Cancels a reservation and restores ticket inventory.
    /// Marks reservation as Cancelled and increments ticket quantity.
    /// </summary>
    /// <param name="reservationId">Reservation identifier</param>
    /// <returns>Updated reservation</returns>
    /// <exception cref="KeyNotFoundException">Thrown when reservation not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when reservation cannot be cancelled</exception>
    Task<Reservation> CancelReservationAsync(Guid reservationId);

    /// <summary>
    /// Retrieves a reservation by identifier.
    /// </summary>
    /// <param name="reservationId">Reservation identifier</param>
    /// <returns>Reservation if found, null otherwise</returns>
    Task<Reservation?> GetReservationByIdAsync(Guid reservationId);
}

/// <summary>
/// Request model for creating a new reservation.
/// </summary>
public class CreateReservationRequest
{
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public int Quantity { get; set; }
    public string PurchaserDNI { get; set; } = string.Empty;
}

/// <summary>
/// Response model for reservation details.
/// </summary>
public class ReservationResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public int Quantity { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public EventResponse? Event { get; set; }
    public TicketTypeResponse? TicketType { get; set; }
}

/// <summary>
/// Response model for event details in reservation.
/// </summary>
public class EventResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

/// <summary>
/// Response model for ticket type details in reservation.
/// </summary>
public class TicketTypeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
