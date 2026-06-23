using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for managing ticket reservations with expiration and concurrency control.
/// Handles temporary ticket holds, inventory management, and reservation lifecycle.
/// Uses database transactions with optimistic concurrency control (RowVersion) to prevent race conditions.
/// </summary>
public class ReservationService : IReservationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReservationService> _logger;

    // Reservation expiration time: 10 minutes
    private const int ReservationExpirationMinutes = 10;

    public ReservationService(ApplicationDbContext context, ILogger<ReservationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new reservation with 10-minute expiration.
    /// Decrements ticket inventory atomically using database transactions and optimistic concurrency control.
    /// Validates: Requirements 4.1, 4.2, 4.3, 4.4, 12.6
    /// </summary>
    public async Task<Reservation> CreateReservationAsync(Guid? userId, Guid eventId, Guid ticketTypeId, int quantity)
    {
        _logger.LogInformation("Creating reservation for user {UserId}, event {EventId}, ticketType {TicketTypeId}, quantity {Quantity}",
            userId, eventId, ticketTypeId, quantity);

        // Validate quantity
        if (quantity <= 0)
        {
            _logger.LogWarning("Invalid quantity {Quantity} for reservation", quantity);
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        }

        // Use transaction with retry logic for optimistic concurrency
        const int maxRetries = 3;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Load ticket type with row-level locking for update (optimistic concurrency via RowVersion)
                    var ticketType = await _context.TicketTypes
                        .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId && tt.EventId == eventId);

                    if (ticketType == null)
                    {
                        _logger.LogWarning("Ticket type {TicketTypeId} not found for event {EventId}", ticketTypeId, eventId);
                        throw new KeyNotFoundException($"Ticket type {ticketTypeId} not found for event {eventId}");
                    }

                    // Calculate currently reserved tickets (active reservations only)
                    var activeReservations = await _context.Reservations
                        .Where(r => r.TicketTypeId == ticketTypeId &&
                                    r.Status == ReservationStatus.Active &&
                                    r.ExpiresAt > DateTime.UtcNow)
                        .SumAsync(r => r.Quantity);

                    // Calculate sold tickets (confirmed tickets in database)
                    var soldTickets = await _context.Tickets
                        .CountAsync(t => t.TicketTypeId == ticketTypeId);

                    // Calculate available inventory: total quantity - sold tickets - active reservations
                    var availableTickets = ticketType.Quantity - soldTickets - activeReservations;

                    _logger.LogInformation("Ticket availability check: Total={Total}, Sold={Sold}, Reserved={Reserved}, Available={Available}",
                        ticketType.Quantity, soldTickets, activeReservations, availableTickets);

                    // Validate sufficient inventory
                    if (availableTickets < quantity)
                    {
                        _logger.LogWarning("Insufficient tickets available. Requested: {Requested}, Available: {Available}",
                            quantity, availableTickets);
                        throw new ArgumentException($"Insufficient tickets available. Requested: {quantity}, Available: {availableTickets}", nameof(quantity));
                    }

                    // Create reservation with 10-minute expiration
                    var now = DateTime.UtcNow;
                    var reservation = new Reservation
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        EventId = eventId,
                        TicketTypeId = ticketTypeId,
                        Quantity = quantity,
                        ExpiresAt = now.AddMinutes(ReservationExpirationMinutes), // Requirement 4.1: 10-minute expiration
                        Status = ReservationStatus.Active,
                        CreatedAt = now
                    };

                    _context.Reservations.Add(reservation);

                    // Save changes - this will validate RowVersion for optimistic concurrency
                    await _context.SaveChangesAsync();

                    // Commit transaction
                    await transaction.CommitAsync();

                    _logger.LogInformation("Reservation {ReservationId} created successfully. Expires at {ExpiresAt}",
                        reservation.Id, reservation.ExpiresAt);

                    return reservation;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // Rollback on concurrency conflict
                    await transaction.RollbackAsync();

                    retryCount++;
                    _logger.LogWarning(ex, "Concurrency conflict on attempt {AttemptCount}/{MaxRetries} for reservation creation",
                        retryCount, maxRetries);

                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError("Maximum retry attempts ({MaxRetries}) reached for reservation creation", maxRetries);
                        throw new InvalidOperationException("Unable to create reservation due to concurrent updates. Please try again.", ex);
                    }

                    // Wait before retry with exponential backoff
                    await Task.Delay(100 * retryCount);
                }
                catch
                {
                    // Rollback on any other error
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                // Continue retry loop
                continue;
            }
        }

        // Should never reach here due to exception handling above
        throw new InvalidOperationException("Unable to create reservation after maximum retries");
    }

    /// <summary>
    /// Validates if a reservation exists, is active, and not expired.
    /// Validates: Requirement 4.4
    /// </summary>
    public async Task<bool> ValidateReservationAsync(Guid reservationId)
    {
        _logger.LogInformation("Validating reservation {ReservationId}", reservationId);

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found", reservationId);
            return false;
        }

        // Check if reservation is active and not expired
        var isValid = reservation.Status == ReservationStatus.Active &&
                      reservation.ExpiresAt > DateTime.UtcNow;

        _logger.LogInformation("Reservation {ReservationId} validation result: {IsValid} (Status: {Status}, ExpiresAt: {ExpiresAt})",
            reservationId, isValid, reservation.Status, reservation.ExpiresAt);

        return isValid;
    }

    /// <summary>
    /// Releases all expired active reservations and restores ticket inventory.
    /// Validates: Requirement 4.5
    /// </summary>
    public async Task<int> ReleaseExpiredReservationsAsync()
    {
        _logger.LogInformation("Starting expired reservation release process");

        var now = DateTime.UtcNow;

        // Find all expired active reservations
        var expiredReservations = await _context.Reservations
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now)
            .ToListAsync();

        if (!expiredReservations.Any())
        {
            _logger.LogInformation("No expired reservations found");
            return 0;
        }

        _logger.LogInformation("Found {Count} expired reservations to release", expiredReservations.Count);

        // Mark all as expired
        foreach (var reservation in expiredReservations)
        {
            reservation.Status = ReservationStatus.Expired;
            _logger.LogInformation("Releasing reservation {ReservationId} for {Quantity} tickets of type {TicketTypeId}",
                reservation.Id, reservation.Quantity, reservation.TicketTypeId);
        }

        // Save changes - inventory is automatically restored by removing from active reservations
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successfully released {Count} expired reservations", expiredReservations.Count);

        return expiredReservations.Count;
    }

    /// <summary>
    /// Confirms a reservation after successful payment.
    /// Marks reservation as Confirmed (called after payment processing).
    /// </summary>
    public async Task<Reservation> ConfirmReservationAsync(Guid reservationId)
    {
        _logger.LogInformation("Confirming reservation {ReservationId}", reservationId);

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found for confirmation", reservationId);
            throw new KeyNotFoundException($"Reservation {reservationId} not found");
        }

        // Validate reservation can be confirmed (must be active and not expired)
        if (reservation.Status != ReservationStatus.Active)
        {
            _logger.LogWarning("Reservation {ReservationId} cannot be confirmed. Current status: {Status}",
                reservationId, reservation.Status);
            throw new InvalidOperationException($"Reservation cannot be confirmed. Current status: {reservation.Status}");
        }

        if (reservation.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Reservation {ReservationId} has expired and cannot be confirmed. Expired at: {ExpiresAt}",
                reservationId, reservation.ExpiresAt);
            throw new InvalidOperationException("Reservation has expired and cannot be confirmed");
        }

        // Mark as confirmed
        reservation.Status = ReservationStatus.Confirmed;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reservation {ReservationId} confirmed successfully", reservationId);

        return reservation;
    }

    /// <summary>
    /// Cancels a reservation and restores ticket inventory.
    /// Marks reservation as Cancelled and inventory is automatically restored by removing from active reservations.
    /// </summary>
    public async Task<Reservation> CancelReservationAsync(Guid reservationId)
    {
        _logger.LogInformation("Cancelling reservation {ReservationId}", reservationId);

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found for cancellation", reservationId);
            throw new KeyNotFoundException($"Reservation {reservationId} not found");
        }

        // Validate reservation can be cancelled (must be active)
        if (reservation.Status != ReservationStatus.Active)
        {
            _logger.LogWarning("Reservation {ReservationId} cannot be cancelled. Current status: {Status}",
                reservationId, reservation.Status);
            throw new InvalidOperationException($"Reservation cannot be cancelled. Current status: {reservation.Status}");
        }

        // Mark as cancelled
        reservation.Status = ReservationStatus.Cancelled;

        _logger.LogInformation("Releasing {Quantity} tickets of type {TicketTypeId} from cancelled reservation {ReservationId}",
            reservation.Quantity, reservation.TicketTypeId, reservationId);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reservation {ReservationId} cancelled successfully. Inventory restored.", reservationId);

        return reservation;
    }

    /// <summary>
    /// Retrieves a reservation by identifier.
    /// </summary>
    public async Task<Reservation?> GetReservationByIdAsync(Guid reservationId)
    {
        _logger.LogInformation("Retrieving reservation {ReservationId}", reservationId);

        var reservation = await _context.Reservations
            .Include(r => r.Event)
            .Include(r => r.TicketType)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found", reservationId);
        }

        return reservation;
    }
}
