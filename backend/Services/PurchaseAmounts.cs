using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// The amounts a buyer ACTUALLY paid, taken from the persisted approved
/// <see cref="Transaction"/> rows — never recomputed from the live ticket price
/// or the current service-fee percentage. Ticket emails (first send, retry and
/// resend) render these values so the buyer always sees what they paid, even if
/// the ticket price or the fee percentage changed afterwards.
/// </summary>
/// <param name="Subtotal">Ticket amount paid (sum of approved <c>Transaction.Amount</c>)</param>
/// <param name="ServiceFee">Service fee paid (sum of approved <c>Transaction.ServiceFee</c>)</param>
/// <param name="UnitPriceByReservation">Paid price per ticket, keyed by reservation id</param>
public sealed record PurchaseAmounts(
    decimal Subtotal,
    decimal ServiceFee,
    IReadOnlyDictionary<Guid, decimal> UnitPriceByReservation)
{
    public decimal Total => Subtotal + ServiceFee;

    /// <summary>Amounts of a single purchase, as charged at approval time.</summary>
    public static PurchaseAmounts ForReservation(Guid reservationId, int quantity, decimal amount, decimal serviceFee) =>
        new(amount, serviceFee, new Dictionary<Guid, decimal> { [reservationId] = UnitPrice(amount, quantity) });

    /// <summary>
    /// Loads the paid amounts of the given reservations from their approved
    /// transactions. Returns <c>null</c> when any reservation has no approved
    /// transaction (e.g. legacy or manually created tickets): callers then fall
    /// back to the ticket-only rendering instead of inventing amounts.
    /// </summary>
    public static async Task<PurchaseAmounts?> LoadAsync(ApplicationDbContext context, IEnumerable<Guid> reservationIds)
    {
        var ids = reservationIds.Distinct().ToList();
        if (ids.Count == 0) return null;

        var rows = await context.Transactions
            .Where(t => ids.Contains(t.ReservationId) && t.Status == TransactionStatus.Approved)
            .Select(t => new { t.ReservationId, t.Amount, t.ServiceFee, t.Reservation.Quantity })
            .ToListAsync();

        if (rows.Select(r => r.ReservationId).Distinct().Count() != ids.Count) return null;

        return new PurchaseAmounts(
            rows.Sum(r => r.Amount),
            rows.Sum(r => r.ServiceFee),
            rows.GroupBy(r => r.ReservationId)
                .ToDictionary(g => g.Key, g => UnitPrice(g.First().Amount, g.First().Quantity)));
    }

    private static decimal UnitPrice(decimal amount, int quantity) =>
        quantity > 0 ? Math.Round(amount / quantity, 2, MidpointRounding.AwayFromZero) : amount;
}
