using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for managing ticket reservations with expiration.
/// Handles temporary ticket holds and reservation lifecycle.
/// Stock is never stored as a counter: availability is computed mathematically
/// (Quantity - sold tickets - active unexpired reservations) inside a single
/// transaction protected by a native PostgreSQL row lock (SELECT ... FOR UPDATE).
/// </summary>
public class ReservationService : IReservationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReservationService> _logger;
    private readonly ReservationTokenOptions _tokenOptions;
    private readonly TimeProvider _clock;

    // Reservation expiration time: 10 minutes
    private const int ReservationExpirationMinutes = 10;

    public ReservationService(
        ApplicationDbContext context,
        ILogger<ReservationService> logger,
        IOptions<ReservationTokenOptions> tokenOptions,
        TimeProvider timeProvider)
    {
        _context = context;
        _logger = logger;
        _tokenOptions = tokenOptions.Value;
        _clock = timeProvider;
    }

    /// <summary>
    /// Creates a new reservation with 10-minute expiration.
    /// Runs the whole operation inside ONE transaction:
    /// row lock on the ticket type (FOR UPDATE) + availability check + insert + commit.
    /// Availability is mathematical over sold tickets and active reservations — there is
    /// no stock counter to increment or roll back.
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

        // Route to transaction-based creation. Both relational (PostgreSQL/SQLite) and
        // InMemory providers use ONE transaction that contains the row lock, the
        // availability check and the reservation insert. Only the row-lock mechanism
        // differs by provider.
        return await CreateReservationTransactionalAsync(userId, eventId, ticketTypeId, quantity, purchaserDNI, purchaserEmail);
    }

    /// <summary>
    /// Creates a reservation inside a single transaction using the native execution
    /// strategy (retry-safe for Npgsql), mirroring ProcessApprovedPaymentAsync.
    /// PostgreSQL acquires the row lock with SELECT ... FOR UPDATE, which serializes
    /// concurrent reservations on the same ticket type: under 1000 concurrent users only
    /// the first sees the available stock and the rest observe the fresh committed state.
    /// Any failure (missing row, insufficient stock, insert error) rolls back everything.
    /// </summary>
    private async Task<Reservation> CreateReservationTransactionalAsync(Guid? userId, Guid eventId, Guid ticketTypeId, int quantity, string purchaserDNI, string? purchaserEmail)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = _clock.GetUtcNow().UtcDateTime;
                var provider = _context.Database.ProviderName;

                TicketType? ticketType;
                if (provider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                {
                    // Native PostgreSQL row lock: blocks concurrent reservations on the same row.
                    ticketType = await _context.TicketTypes
                        .FromSqlInterpolated($"SELECT * FROM \"TicketTypes\" WHERE \"Id\" = {ticketTypeId} AND \"EventId\" = {eventId} FOR UPDATE")
                        .FirstOrDefaultAsync();
                }
                else if (provider == "Microsoft.EntityFrameworkCore.Sqlite")
                {
                    // SQLite has no FOR UPDATE support. A no-op UPDATE on the row acquires the
                    // database write lock so the check-then-insert serializes against concurrent writers.
                    ticketType = await _context.TicketTypes
                        .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId && tt.EventId == eventId);
                    if (ticketType != null)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"TicketTypes\" SET \"CreatedAt\" = \"CreatedAt\" WHERE \"Id\" = {ticketTypeId}");
                    }
                }
                else
                {
                    // InMemory provider (tests): no native locking support.
                    ticketType = await _context.TicketTypes
                        .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId && tt.EventId == eventId);
                }

                if (ticketType == null)
                {
                    _logger.LogWarning("Ticket type {TicketTypeId} not found for event {EventId}", ticketTypeId, eventId);
                    throw new KeyNotFoundException($"Ticket type {ticketTypeId} not found for event {eventId}");
                }

                // Mathematical availability: no stock counter involved.
                // Refunded tickets do not count as sold (APR-005).
                var soldTickets = await _context.Tickets
                    .CountAsync(t => t.TicketTypeId == ticketTypeId && !t.IsRefunded);

                var activeReservations = await _context.Reservations
                    .Where(r => r.TicketTypeId == ticketTypeId &&
                                r.Status == ReservationStatus.Active &&
                                r.ExpiresAt > now)
                    .SumAsync(r => (int?)r.Quantity) ?? 0;

                var availableTickets = ticketType.Quantity - soldTickets - activeReservations;

                if (availableTickets < quantity)
                {
                    _logger.LogWarning("Insufficient tickets available. Requested: {Requested}, Available: {Available}, TicketType: {TicketTypeId}",
                        quantity, availableTickets, ticketTypeId);
                    throw new ArgumentException($"Insufficient tickets available. Requested: {quantity}, Available: {availableTickets}", nameof(quantity));
                }

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
            catch
            {
                // Any failure rolls back the entire operation (including nothing stock-related
                // to restore — availability is computed on the fly).
                await transaction.RollbackAsync();
                throw;
            }
        });
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
                      reservation.ExpiresAt > _clock.GetUtcNow().UtcDateTime;

        _logger.LogInformation("Reservation {ReservationId} validation result: {IsValid} (Status: {Status}, ExpiresAt: {ExpiresAt})",
            reservationId, isValid, reservation.Status, reservation.ExpiresAt);

        return isValid;
    }

    /// <summary>
    /// Marks all expired active reservations as Expired (state cleanup only).
    /// Does NOT touch any stock counter — availability is computed mathematically from
    /// active, unexpired reservations, so expired ones automatically stop being counted.
    /// Validates: Requirement 4.5
    /// </summary>
    public async Task<int> ReleaseExpiredReservationsAsync()
    {
        _logger.LogInformation("Starting expired reservation release process");

        var now = _clock.GetUtcNow().UtcDateTime;

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
            _logger.LogInformation("Marking reservation {ReservationId} for {Quantity} tickets of type {TicketTypeId} as expired",
                reservation.Id, reservation.Quantity, reservation.TicketTypeId);
        }

        // Save changes — no stock counter to decrement; expired reservations stop counting
        // toward availability automatically.
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

        if (reservation.ExpiresAt <= _clock.GetUtcNow().UtcDateTime)
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
    /// Cancels a reservation (state change only).
    /// Marks reservation as Cancelled. No stock counter is touched — a cancelled reservation
    /// simply stops counting toward the mathematical availability calculation.
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

        _logger.LogInformation("Cancelling reservation {ReservationId} for {Quantity} tickets of type {TicketTypeId}",
            reservationId, reservation.Quantity, reservation.TicketTypeId);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reservation {ReservationId} cancelled successfully", reservationId);

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

        if (reservation.ExpiresAt <= _clock.GetUtcNow().UtcDateTime)
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
        var timestamp = _clock.GetUtcNow().ToUnixTimeSeconds();
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
        if ((_clock.GetUtcNow().UtcDateTime - tokenTime).TotalMinutes > expiryMinutes)
        {
            _logger.LogWarning("Reservation token expired. Token time: {TokenTime}, Now: {Now}", tokenTime, _clock.GetUtcNow().UtcDateTime);
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
