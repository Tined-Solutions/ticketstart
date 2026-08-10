using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Implementation of <see cref="IAdminPurchaseService"/>.
///
/// Refund (APR-003/004): one EF Core transaction with an execution-strategy wrapper
/// (mirroring <c>ReservationService.CreateReservationTransactionalAsync</c>). The
/// reservation's tickets are row-locked (Npgsql SELECT ... FOR UPDATE / SQLite no-op
/// UPDATE / InMemory plain), IsUsed is re-checked under the lock (scan-vs-refund race,
/// APR-004), and the existing Approved Transaction row is FLIPPED to Refunded — never
/// a second row, preserving the unique IX_Transactions_MercadoPagoId index.
/// </summary>
public class AdminPurchaseService : IAdminPurchaseService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminPurchaseService> _logger;

    public AdminPurchaseService(ApplicationDbContext context, ILogger<AdminPurchaseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminPurchasesResponse> GetPurchasesAsync(Guid eventId)
    {
        _logger.LogInformation("Admin listing purchases for event {EventId}", eventId);

        var eventEntity = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found for purchase listing", eventId);
            throw new KeyNotFoundException($"Event {eventId} not found");
        }

        var reservations = await _context.Reservations
            .AsNoTracking()
            .Include(r => r.TicketType)
            .Where(r => r.EventId == eventId && r.Status == ReservationStatus.Confirmed)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        var reservationIds = reservations.Select(r => r.Id).ToList();

        var transactions = reservationIds.Count == 0
            ? new List<Transaction>()
            : await _context.Transactions
                .AsNoTracking()
                .Where(t => reservationIds.Contains(t.ReservationId) &&
                            (t.Status == TransactionStatus.Approved || t.Status == TransactionStatus.Refunded))
                .ToListAsync();

        // Number of tickets actually linked to each reservation (APR-009). Zero linked
        // tickets for a confirmed purchase means the legacy backfill could not prove
        // the link → flagged "link unverified" in the listing.
        var linkedTicketCounts = reservationIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _context.Tickets
                .AsNoTracking()
                .Where(t => t.ReservationId != null && reservationIds.Contains(t.ReservationId.Value))
                .GroupBy(t => t.ReservationId!.Value)
                .Select(g => new { ReservationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReservationId, x => x.Count);

        var rows = reservations.Select(r =>
        {
            var tx = transactions.FirstOrDefault(t => t.ReservationId == r.Id);
            linkedTicketCounts.TryGetValue(r.Id, out var linkedTickets);

            return new AdminPurchaseRow(
                r.Id,
                r.PurchaserEmail ?? string.Empty,
                r.PurchaserDNI,
                r.TicketType.Name,
                r.Quantity,
                tx?.Amount ?? 0m,
                tx?.CreatedAt ?? r.CreatedAt,
                tx?.Status == TransactionStatus.Refunded,
                linkedTickets == 0);
        }).ToList();

        var totalRefunded = transactions
            .Where(t => t.Status == TransactionStatus.Refunded)
            .Sum(t => t.Amount);

        return new AdminPurchasesResponse(eventId, eventEntity.Name, rows, totalRefunded);
    }

    /// <inheritdoc />
    public async Task RefundPurchaseAsync(Guid reservationId, Guid adminId)
    {
        _logger.LogInformation("Admin {AdminId} refunding reservation {ReservationId}", adminId, reservationId);

        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == reservationId);

                if (reservation == null)
                {
                    _logger.LogWarning("Reservation {ReservationId} not found for refund", reservationId);
                    throw new KeyNotFoundException($"Reservation {reservationId} not found");
                }

                var now = DateTime.UtcNow;
                var provider = _context.Database.ProviderName;

                // Row-lock the reservation's tickets (APR-004): the same three-provider
                // lock trio used by ReservationService/EventService.
                List<Ticket> tickets;
                if (provider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                {
                    tickets = await _context.Tickets
                        .FromSqlInterpolated($"SELECT * FROM \"Tickets\" WHERE \"ReservationId\" = {reservationId} FOR UPDATE")
                        .ToListAsync();
                }
                else if (provider == "Microsoft.EntityFrameworkCore.Sqlite")
                {
                    // SQLite has no FOR UPDATE: a no-op UPDATE on each row acquires the
                    // database write lock so the check-then-write serializes.
                    tickets = await _context.Tickets.Where(t => t.ReservationId == reservationId).ToListAsync();
                    foreach (var ticket in tickets)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"Tickets\" SET \"CreatedAt\" = \"CreatedAt\" WHERE \"Id\" = {ticket.Id}");
                    }
                }
                else
                {
                    // InMemory provider (tests): no native locking support.
                    tickets = await _context.Tickets.Where(t => t.ReservationId == reservationId).ToListAsync();
                }

                // Re-check under the lock (APR-004): if a concurrent staff scan marked a
                // ticket used after the refund read its state, the refund must abort.
                if (tickets.Any(t => t.IsUsed))
                {
                    _logger.LogWarning("Refund of reservation {ReservationId} blocked: a ticket is already used", reservationId);
                    throw new InvalidOperationException("Cannot refund a purchase with used tickets");
                }

                if (tickets.Any(t => t.IsRefunded))
                {
                    _logger.LogWarning("Refund of reservation {ReservationId} blocked: tickets already refunded", reservationId);
                    throw new InvalidOperationException("Purchase already refunded");
                }

                var existingRefundedTx = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.ReservationId == reservationId && t.Status == TransactionStatus.Refunded);
                if (existingRefundedTx != null)
                {
                    _logger.LogWarning("Refund of reservation {ReservationId} blocked: already refunded", reservationId);
                    throw new InvalidOperationException("Purchase already refunded");
                }

                // APR-003: the Approved transaction is FLIPPED, never duplicated.
                var approvedTx = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.ReservationId == reservationId && t.Status == TransactionStatus.Approved);
                if (approvedTx == null)
                {
                    _logger.LogWarning("Reservation {ReservationId} has no Approved transaction to refund", reservationId);
                    throw new InvalidOperationException("No approved transaction found for this purchase");
                }

                approvedTx.Status = TransactionStatus.Refunded;
                approvedTx.UpdatedAt = now;

                foreach (var ticket in tickets)
                {
                    ticket.IsRefunded = true;
                    ticket.RefundedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Purchase {ReservationId} refunded by admin {AdminId}: {TicketCount} tickets marked refunded, transaction flipped to Refunded",
                    reservationId, adminId, tickets.Count);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}
