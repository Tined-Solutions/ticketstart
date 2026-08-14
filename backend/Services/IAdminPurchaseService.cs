using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Admin-only surface for listing an event's confirmed purchases and refunding an
/// unused purchase by quantity. The refund is an in-process EF Core transaction that
/// marks the K oldest non-refunded tickets refunded (APR-013), inserts one immutable
/// Refunds ledger row (APR-012) and FLIPS the Approved Transaction to Refunded ONLY
/// when zero active tickets remain (D2) — it never moves money via Mercado Pago,
/// never sends email and never records a motivo (APR-008).
/// </summary>
public interface IAdminPurchaseService
{
    /// <summary>
    /// Lists an event's confirmed purchases with raw buyer data, ticket type,
    /// quantity, amount, purchase date, refunded quantity/amount, derived refunded
    /// flag and the per-event totalRefunded (= Σ Refunds.Amount, APR-012).
    /// Buyer email/DNI are exposed RAW: this surface is Admin-only by policy
    /// (RequireAdminRole) so the admin can identify the buyer when refunding.
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>Listing response (empty list when no confirmed purchases)</returns>
    /// <exception cref="KeyNotFoundException">Event does not exist</exception>
    Task<AdminPurchasesResponse> GetPurchasesAsync(Guid eventId);

    /// <summary>
    /// Refunds K tickets of a purchase atomically: marks the K oldest non-refunded,
    /// non-used tickets refunded (APR-013), inserts one Refunds ledger row with
    /// Amount = unit price × K (APR-012/D7) and flips the Approved Transaction to
    /// Refunded ONLY when the operation leaves zero active tickets (D2).
    /// </summary>
    /// <param name="reservationId">Confirmed reservation to refund</param>
    /// <param name="quantity">Number of tickets to refund (K, must be &gt; 0 and ≤ active)</param>
    /// <param name="adminId">Admin performing the refund (recorded in the Refunds row)</param>
    /// <exception cref="KeyNotFoundException">Reservation does not exist</exception>
    /// <exception cref="InvalidOperationException">No Approved transaction, K ≤ 0,
    /// K &gt; active remaining, or any ticket IsUsed (APR-003/APR-004)</exception>
    Task RefundPurchaseAsync(Guid reservationId, int quantity, Guid adminId);
}

/// <summary>
/// Response for the admin purchases listing (APR-002).
/// </summary>
public record AdminPurchasesResponse(
    Guid EventId,
    string EventName,
    IReadOnlyList<AdminPurchaseRow> Purchases,
    decimal TotalRefunded);

/// <summary>
/// A single purchase row in the admin listing. Buyer email/DNI are exposed raw
/// (NOT masked): the surface is Admin-only by policy, so the admin can identify
/// the buyer when refunding. <see cref="RefundedQuantity"/> is the count of
/// IsRefunded tickets (APR-012), <see cref="RefundedAmount"/> is Σ Refunds.Amount
/// for the reservation, and <see cref="Refunded"/> is derived as fully refunded
/// (RefundedQuantity &gt;= Quantity). <see cref="LinkUnverified"/> is true when no
/// ticket of the purchase could be linked to its reservation (legacy backfill
/// leftovers, APR-009).
/// </summary>
public record AdminPurchaseRow(
    Guid ReservationId,
    string PurchaserEmail,
    string PurchaserDni,
    string TicketType,
    int Quantity,
    decimal Amount,
    DateTime PurchasedAt,
    int RefundedQuantity,
    decimal RefundedAmount,
    bool Refunded,
    bool LinkUnverified);
