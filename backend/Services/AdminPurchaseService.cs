using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Implementation of <see cref="IAdminPurchaseService"/>.
///
/// Refund (APR-003/004/012/013): one EF Core transaction with an execution-strategy
/// wrapper (mirroring <c>ReservationService.CreateReservationTransactionalAsync</c>).
/// The reservation's tickets are row-locked (Npgsql SELECT ... FOR UPDATE / SQLite
/// no-op UPDATE / InMemory plain), IsUsed is re-checked under the lock (scan-vs-refund
/// race, APR-004), and the K oldest non-refunded/non-used tickets are marked refunded
/// (APR-013). Exactly one immutable <c>Refunds</c> ledger row is inserted per operation
/// (APR-012, Amount = unit price × K — D7). The existing Approved Transaction row is
/// FLIPPED to Refunded ONLY when zero active tickets remain (D2) — never a second row,
/// preserving the unique IX_Transactions_MercadoPagoId index.
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

        // Refunded tickets per reservation (APR-012 RefundedQuantity). Group query,
        // no N+1 — parallel structure to linkedTicketCounts.
        var refundedTicketCounts = reservationIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _context.Tickets
                .AsNoTracking()
                .Where(t => t.ReservationId != null && reservationIds.Contains(t.ReservationId.Value) && t.IsRefunded)
                .GroupBy(t => t.ReservationId!.Value)
                .Select(g => new { ReservationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReservationId, x => x.Count);

        // Σ Refunds.Amount per reservation (APR-012 RefundedAmount / TotalRefunded).
        var refundsByRes = reservationIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await _context.Refunds
                .AsNoTracking()
                .Where(r => reservationIds.Contains(r.ReservationId))
                .GroupBy(r => r.ReservationId)
                .Select(g => new { ReservationId = g.Key, Sum = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.ReservationId, x => x.Sum);

        var rows = reservations.Select(r =>
        {
            var tx = transactions.FirstOrDefault(t => t.ReservationId == r.Id);
            linkedTicketCounts.TryGetValue(r.Id, out var linkedTickets);
            refundedTicketCounts.TryGetValue(r.Id, out var refundedQuantity);

            return new AdminPurchaseRow(
                r.Id,
                r.PurchaserEmail ?? string.Empty,
                r.PurchaserDNI,
                r.TicketType.Name,
                r.Quantity,
                tx?.Amount ?? 0m,
                tx?.CreatedAt ?? r.CreatedAt,
                refundedQuantity,
                refundsByRes.GetValueOrDefault(r.Id),
                refundedQuantity >= r.Quantity,   // derived fully-refunded flag (APR-012)
                linkedTickets == 0);
        }).ToList();

        // APR-012: TotalRefunded = Σ Refunds.Amount across the event's reservations.
        var totalRefunded = refundsByRes.Values.Sum();

        return new AdminPurchasesResponse(eventId, eventEntity.Name, rows, totalRefunded);
    }

    /// <inheritdoc />
    public async Task RefundPurchaseAsync(Guid reservationId, int quantity, Guid adminId)
    {
        _logger.LogInformation("Admin {AdminId} refunding {Quantity} tickets of reservation {ReservationId}", adminId, quantity, reservationId);

        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // D7: unit price comes from the reservation's TicketType (canonical,
                // stable decimal(18,2); Amount = Price × K is exact).
                var reservation = await _context.Reservations
                    .Include(r => r.TicketType)
                    .FirstOrDefaultAsync(r => r.Id == reservationId);

                if (reservation == null)
                {
                    _logger.LogWarning("Reservation {ReservationId} not found for refund", reservationId);
                    throw new KeyNotFoundException($"Reservation {reservationId} not found");
                }

                var now = DateTime.UtcNow;

                // Row-lock the reservation's tickets (APR-004): the same three-provider
                // lock trio used by ReservationService/EventService.
                var tickets = await AcquireTicketLocksAsync(reservationId);

                // Re-check under the lock (APR-004): if a concurrent staff scan marked a
                // ticket used after the refund read its state, the refund must abort.
                if (tickets.Any(t => t.IsUsed))
                {
                    _logger.LogWarning("Refund of reservation {ReservationId} blocked: a ticket is already used", reservationId);
                    throw new InvalidOperationException("Cannot refund a purchase with used tickets");
                }

                // APR-003 quantity guard: K must be > 0 and ≤ active (non-refunded,
                // non-used) tickets — observed ON THE LOCKED LIST so concurrent refunds
                // serialize and no ticket is refunded twice.
                var active = tickets.Count(t => !t.IsRefunded && !t.IsUsed);
                if (quantity <= 0 || quantity > active)
                {
                    _logger.LogWarning("Refund of reservation {ReservationId} blocked: quantity {Quantity} out of range ({Active} active remaining)", reservationId, quantity, active);
                    throw new InvalidOperationException($"Cannot refund {quantity} tickets; {active} active remaining");
                }

                // APR-003: the Approved transaction is FLIPPED (only at zero active),
                // never duplicated.
                var approvedTx = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.ReservationId == reservationId && t.Status == TransactionStatus.Approved);
                if (approvedTx == null)
                {
                    _logger.LogWarning("Reservation {ReservationId} has no Approved transaction to refund", reservationId);
                    throw new InvalidOperationException("No approved transaction found for this purchase");
                }

                // APR-013 deterministic selection: the K OLDEST non-refunded, non-used
                // tickets (stable, replayable, auditable under concurrent ops).
                var selected = tickets
                    .Where(t => !t.IsRefunded && !t.IsUsed)
                    .OrderBy(t => t.CreatedAt)
                    .Take(quantity)
                    .ToList();

                foreach (var ticket in selected)
                {
                    ticket.IsRefunded = true;
                    ticket.RefundedAt = now;
                }

                // APR-012: exactly one immutable Refunds ledger row per operation.
                var unitPrice = reservation.TicketType.Price;   // D7
                _context.Refunds.Add(new Refund
                {
                    ReservationId = reservationId,
                    TicketIds = selected.Select(t => t.Id).ToArray(),
                    Quantity = quantity,
                    Amount = unitPrice * quantity,
                    AdminId = adminId,
                    CreatedAt = now
                });

                // D2 flip invariant: flip Approved → Refunded ONLY when this operation
                // leaves zero active tickets; partial ops keep the tx Approved.
                if (active == quantity)
                {
                    approvedTx.Status = TransactionStatus.Refunded;
                    approvedTx.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Refunded {Quantity}/{Active} tickets of reservation {ReservationId}; tx {Flip} by admin {AdminId}",
                    quantity, active, reservationId, active == quantity ? "flipped" : "kept-Approved", adminId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// Row-locks the reservation's tickets using the three-provider lock trio
    /// (Npgsql SELECT ... FOR UPDATE / SQLite no-op UPDATE / InMemory plain) so the
    /// check-then-write refund serializes across concurrent operations (APR-004/013).
    /// </summary>
    private async Task<List<Ticket>> AcquireTicketLocksAsync(Guid reservationId)
    {
        var provider = _context.Database.ProviderName;
        if (provider == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await _context.Tickets
                .FromSqlInterpolated($"SELECT * FROM \"Tickets\" WHERE \"ReservationId\" = {reservationId} FOR UPDATE")
                .ToListAsync();
        }
        if (provider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite has no FOR UPDATE: a no-op UPDATE on each row acquires the
            // database write lock so the check-then-write serializes.
            var sqliteTickets = await _context.Tickets.Where(t => t.ReservationId == reservationId).ToListAsync();
            foreach (var ticket in sqliteTickets)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"Tickets\" SET \"CreatedAt\" = \"CreatedAt\" WHERE \"Id\" = {ticket.Id}");
            }
            return sqliteTickets;
        }

        // InMemory provider (tests): no native locking support.
        return await _context.Tickets.Where(t => t.ReservationId == reservationId).ToListAsync();
    }
}
