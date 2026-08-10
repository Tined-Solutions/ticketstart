using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Admin-only surface for listing an event's confirmed purchases and refunding an
/// unused full purchase. The refund is an in-process EF Core transaction that marks
/// tickets refunded and FLIPS the Approved Transaction to Refunded — it never moves
/// money via Mercado Pago, never sends email and never records a motivo (APR-008).
/// </summary>
public interface IAdminPurchaseService
{
    /// <summary>
    /// Lists an event's confirmed purchases with raw buyer data, ticket type,
    /// quantity, amount, purchase date, refunded flag and the per-event totalRefunded.
    /// Buyer email/DNI are exposed RAW: this surface is Admin-only by policy
    /// (RequireAdminRole) so the admin can identify the buyer when refunding.
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>Listing response (empty list when no confirmed purchases)</returns>
    /// <exception cref="KeyNotFoundException">Event does not exist</exception>
    Task<AdminPurchasesResponse> GetPurchasesAsync(Guid eventId);

    /// <summary>
    /// Refunds an unused full purchase atomically: marks all tickets of the
    /// reservation refunded and flips the Approved Transaction to Refunded.
    /// </summary>
    /// <param name="reservationId">Confirmed reservation to refund</param>
    /// <param name="adminId">Admin performing the refund (used for audit context)</param>
    /// <exception cref="KeyNotFoundException">Reservation does not exist</exception>
    /// <exception cref="InvalidOperationException">No Approved transaction, already
    /// refunded, or any ticket IsUsed (APR-003/APR-004)</exception>
    Task RefundPurchaseAsync(Guid reservationId, Guid adminId);
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
/// the buyer when refunding. <see cref="LinkUnverified"/> is true when no ticket
/// of the purchase could be linked to its reservation (legacy backfill leftovers,
/// APR-009).
/// </summary>
public record AdminPurchaseRow(
    Guid ReservationId,
    string PurchaserEmail,
    string PurchaserDni,
    string TicketType,
    int Quantity,
    decimal Amount,
    DateTime PurchasedAt,
    bool Refunded,
    bool LinkUnverified);
