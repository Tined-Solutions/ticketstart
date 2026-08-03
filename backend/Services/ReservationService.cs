using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
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
    private readonly ReservationTokenOptions _tokenOptions;

    // Reservation expiration time: 10 minutes
    private const int ReservationExpirationMinutes = 10;

    public ReservationService(
        ApplicationDbContext context,
        ILogger<ReservationService> logger,
        IOptions<ReservationTokenOptions> tokenOptions)
    {
        _context = context;
        _logger = logger;
        _tokenOptions = tokenOptions.Value;
    }

    /// <summary>
    /// Creates a new reservation with 10-minute expiration.
    /// Uses atomic ExecuteUpdateAsync on TicketType.CurrentlyReserved for relational providers,
    /// and falls back to explicit transactions for InMemory provider.
    /// Validates: Requirements 4.1, 4.2, 4.3, 4.4, 12.6, Batch 3 REQ-7, REQ-8.
    /// </summary>
    public async Task<Reservation> CreateReservationAsync(Guid? userId, Guid eventId, Guid ticketTypeId, int quantity, string purchaserDNI, string? purchaserEmail = null)
    {
        _logger.LogInformation("Creating reservation for user {UserId}, event {EventId}, ticketType {TicketTypeId}, quantity {Quantity}",
            userId, eventId, ticketTypeId, quantity);

        // Validate quantity
        if (quantity <= 0)
        {
            _logger.LogWarning("Invalid quantity {Quantity} for reservation", quantity);
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        }

        // Validate purchaser DNI
        if (string.IsNullOrWhiteSpace(purchaserDNI))
        {
            _logger.LogWarning("Purchaser DNI is required for reservation");
            throw new ArgumentException("Purchaser DNI is required", nameof(purchaserDNI));
        }

        if (purchaserDNI.Length > 50)
        {
            _logger.LogWarning("Purchaser DNI exceeds maximum length of 50 characters");
            throw new ArgumentException("Purchaser DNI must not exceed 50 characters", nameof(purchaserDNI));
        }

        // Verify the ticket type exists for this event
        var ticketTypeExists = await _context.TicketTypes
            .AnyAsync(tt => tt.Id == ticketTypeId && tt.EventId == eventId);

        if (!ticketTypeExists)
        {
            _logger.LogWarning("Ticket type {TicketTypeId} not found for event {EventId}", ticketTypeId, eventId);
            throw new KeyNotFoundException($"Ticket type {ticketTypeId} not found for event {eventId}");
        }

        // Route to atomic (relational) or transaction-based (InMemory) path.
        // InMemory does not support ExecuteUpdateAsync.
        if (_context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            return await CreateReservationAtomicAsync(userId, eventId, ticketTypeId, quantity, purchaserDNI, purchaserEmail);
        }

        return await CreateReservationTransactionalAsync(userId, eventId, ticketTypeId, quantity, purchaserDNI, purchaserEmail);
    }

    /// <summary>
    /// Atomic stock check using ExecuteUpdateAsync on TicketType.CurrentlyReserved.
    /// The WHERE clause ensures stock sufficiency at evaluation time;
    /// the SET clause atomically increments CurrentlyReserved.
    /// Used by PostgreSQL, SQLite, and other relational providers.
    /// </summary>
    private async Task<Reservation> CreateReservationAtomicAsync(Guid? userId, Guid eventId, Guid ticketTypeId, int quantity, string purchaserDNI, string? purchaserEmail)
    {
        // Single atomic round-trip: check stock AND reserve in one SQL statement
        var rowsAffected = await _context.TicketTypes
            .Where(tt => tt.Id == ticketTypeId &&
                         tt.Quantity - tt.CurrentlyReserved >= quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(tt => tt.CurrentlyReserved, tt => tt.CurrentlyReserved + quantity));

        if (rowsAffected == 0)
        {
            _logger.LogWarning("Insufficient tickets available. Requested: {Requested}, TicketType: {TicketTypeId}",
                quantity, ticketTypeId);
            throw new ArgumentException($"Insufficient tickets available. Requested: {quantity}", nameof(quantity));
        }

        _logger.LogInformation("Atomic stock reservation successful. Reserved {Quantity} tickets for type {TicketTypeId}",
            quantity, ticketTypeId);

        // Insert the Reservation entity with retry for concurrency conflicts.
        // On failure, rollback the atomic stock increment.
        const int maxRetries = 3;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                var now = DateTime.UtcNow;
                var reservation = new Reservation
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EventId = eventId,
                    TicketTypeId = ticketTypeId,
                    Quantity = quantity,
                    PurchaserDNI = purchaserDNI,
                    PurchaserEmail = purchaserEmail,
                    ExpiresAt = now.AddMinutes(ReservationExpirationMinutes),
                    Status = ReservationStatus.Active,
                    CreatedAt = now
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Reservation {ReservationId} created successfully. Expires at {ExpiresAt}",
                    reservation.Id, reservation.ExpiresAt);

                return reservation;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "Concurrency conflict on attempt {AttemptCount}/{MaxRetries}", retryCount, maxRetries);

                if (retryCount >= maxRetries)
                {
                    // Rollback the atomic stock reservation
                    await _context.TicketTypes
                        .Where(tt => tt.Id == ticketTypeId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(tt => tt.CurrentlyReserved, tt => Math.Max(0, tt.CurrentlyReserved - quantity)));

                    _logger.LogError("Max retries reached for reservation creation");
                    throw new InvalidOperationException("Unable to create reservation due to concurrent updates.", ex);
                }

                await Task.Delay(100 * retryCount);
            }
            catch (Exception ex) when (ex is not DbUpdateConcurrencyException)
            {
                // Rollback stock on any non-concurrency error
                await _context.TicketTypes
                    .Where(tt => tt.Id == ticketTypeId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(tt => tt.CurrentlyReserved, tt => Math.Max(0, tt.CurrentlyReserved - quantity)));
                throw;
            }
        }

        throw new InvalidOperationException("Unable to create reservation after maximum retries");
    }

    /// <summary>
    /// Transaction-based reservation for InMemory provider (does not support ExecuteUpdateAsync).
    /// </summary>
    private async Task<Reservation> CreateReservationTransactionalAsync(Guid? userId, Guid eventId, Guid ticketTypeId, int quantity, string purchaserDNI, string? purchaserEmail)
    {
        const int maxRetries = 3;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var ticketType = await _context.TicketTypes
                        .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId && tt.EventId == eventId);

                    if (ticketType == null)
                        throw new KeyNotFoundException($"Ticket type {ticketTypeId} not found for event {eventId}");

                    var activeReservations = await _context.Reservations
                        .Where(r => r.TicketTypeId == ticketTypeId &&
                                    r.Status == ReservationStatus.Active &&
                                    r.ExpiresAt > DateTime.UtcNow)
                        .SumAsync(r => r.Quantity);

                    var soldTickets = await _context.Tickets
                        .CountAsync(t => t.TicketTypeId == ticketTypeId);

                    var availableTickets = ticketType.Quantity - soldTickets - activeReservations;

                    if (availableTickets < quantity)
                        throw new ArgumentException($"Insufficient tickets available. Requested: {quantity}, Available: {availableTickets}", nameof(quantity));

                    var now = DateTime.UtcNow;
                    var reservation = new Reservation
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        EventId = eventId,
                        TicketTypeId = ticketTypeId,
                        Quantity = quantity,
                        PurchaserDNI = purchaserDNI,
                        PurchaserEmail = purchaserEmail,
                        ExpiresAt = now.AddMinutes(ReservationExpirationMinutes),
                        Status = ReservationStatus.Active,
                        CreatedAt = now
                    };

                    _context.Reservations.Add(reservation);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Reservation {ReservationId} created successfully. Expires at {ExpiresAt}",
                        reservation.Id, reservation.ExpiresAt);

                    return reservation;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    retryCount++;
                    _logger.LogWarning(ex, "Concurrency conflict on attempt {AttemptCount}/{MaxRetries}", retryCount, maxRetries);
                    if (retryCount >= maxRetries)
                        throw new InvalidOperationException("Unable to create reservation due to concurrent updates.", ex);
                    await Task.Delay(100 * retryCount);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                continue;
            }
        }

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

    /// <summary>
    /// Updates the purchaser data (DNI, email) on an existing active reservation.
    /// Does NOT affect ticket stock — the reservation already holds the tickets.
    /// Requires a valid reservation token for authorization.
    /// </summary>
    public async Task<Reservation> UpdateReservationAsync(Guid reservationId, UpdateReservationRequest request)
    {
        _logger.LogInformation("Updating reservation {ReservationId}", reservationId);

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found for update", reservationId);
            throw new KeyNotFoundException($"Reservation {reservationId} not found");
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            _logger.LogWarning("Reservation {ReservationId} cannot be updated. Current status: {Status}",
                reservationId, reservation.Status);
            throw new InvalidOperationException($"Reservation cannot be updated. Current status: {reservation.Status}");
        }

        if (reservation.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Reservation {ReservationId} has expired and cannot be updated", reservationId);
            throw new InvalidOperationException("Reservation has expired and cannot be updated");
        }

        // Validate reservation token (proves the caller owns this reservation)
        if (!ValidateReservationToken(request.Token, out _))
        {
            _logger.LogWarning("Invalid reservation token for reservation {ReservationId}", reservationId);
            throw new UnauthorizedAccessException("Invalid reservation token");
        }

        // Validate purchaser DNI
        if (string.IsNullOrWhiteSpace(request.PurchaserDNI))
        {
            throw new ArgumentException("Purchaser DNI is required", nameof(request.PurchaserDNI));
        }

        if (request.PurchaserDNI.Length > 50)
        {
            throw new ArgumentException("Purchaser DNI must not exceed 50 characters", nameof(request.PurchaserDNI));
        }

        // Update only the editable fields — stock and ticket selection remain untouched
        reservation.PurchaserDNI = request.PurchaserDNI.Trim();
        reservation.PurchaserEmail = request.PurchaserEmail?.Trim();

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reservation {ReservationId} updated successfully", reservationId);

        return reservation;
    }

    /// <summary>
    /// Generates an HMAC-SHA256 token for a reservation.
    /// Token format: nonce:timestamp:signature
    /// The token proves the caller created the reservation without requiring authentication.
    /// </summary>
    public string GenerateReservationToken(Guid reservationId)
    {
        if (string.IsNullOrEmpty(_tokenOptions.TokenSecretKey))
        {
            throw new InvalidOperationException("Reservation:TokenSecretKey is not configured");
        }

        var nonce = Guid.NewGuid().ToString("N")[..16];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataToSign = $"{nonce}:{timestamp}";
        var signature = HmacHelper.ComputeHmacSha256(dataToSign, _tokenOptions.TokenSecretKey);
        var token = $"{nonce}:{timestamp}:{signature}";

        _logger.LogDebug("Generated reservation token for reservation {ReservationId}", reservationId);

        return token;
    }

    /// <summary>
    /// Validates a reservation token.
    /// Checks signature integrity and token expiry (default 10 minutes).
    /// Returns the reservation ID if valid.
    /// </summary>
    public bool ValidateReservationToken(string token, out Guid reservationId, int expiryMinutes = 10)
    {
        reservationId = Guid.Empty;

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(_tokenOptions.TokenSecretKey))
        {
            return false;
        }

        var parts = token.Split(':');
        if (parts.Length != 3)
        {
            _logger.LogWarning("Invalid reservation token format: expected 3 parts, got {Count}", parts.Length);
            return false;
        }

        var nonce = parts[0];
        var timestampStr = parts[1];
        var providedSignature = parts[2];

        if (!long.TryParse(timestampStr, out var timestamp))
        {
            _logger.LogWarning("Invalid timestamp in reservation token");
            return false;
        }

        // Check expiry
        var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
        if ((DateTime.UtcNow - tokenTime).TotalMinutes > expiryMinutes)
        {
            _logger.LogWarning("Reservation token expired. Token time: {TokenTime}, Now: {Now}", tokenTime, DateTime.UtcNow);
            return false;
        }

        // Verify signature
        var dataToVerify = $"{nonce}:{timestamp}";
        if (!HmacHelper.ValidateHmacSha256(dataToVerify, _tokenOptions.TokenSecretKey, providedSignature))
        {
            _logger.LogWarning("Reservation token signature verification failed");
            return false;
        }

        return true;
    }
}
