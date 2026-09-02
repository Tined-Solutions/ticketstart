using System.ComponentModel.DataAnnotations.Schema;

namespace TicketeraOnline.Api.Models;

/// <summary>
/// Immutable audit ledger row recording ONE refund operation (APR-012).
/// Exactly one row per refund op: the reservation, the K refunded TicketIds,
/// Quantity (K), Amount (unit price × K — D7), the admin who refunded, and
/// CreatedAt. No UpdatedAt — the operation record never changes.
/// AdminId is NULL only for legacy rows backfilled from pre-existing Refunded
/// transactions (APR-014).
/// </summary>
public class Refund
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }

    /// <summary>
    /// Snapshot of the K ticket ids marked refunded by this operation (D5).
    /// PG uuid[] column; InMemory stores the CLR array natively.
    /// </summary>
    [Column(TypeName = "uuid[]")]
    public Guid[] TicketIds { get; set; } = Array.Empty<Guid>();

    /// <summary>Number of tickets refunded by this operation (K).</summary>
    public int Quantity { get; set; }

    /// <summary>Amount refunded by this operation — the admin-defined amount stored
    /// verbatim (0 &lt; A ≤ unit price × K; NOT necessarily unit price × K — D7 now
    /// describes the CAP only).</summary>
    public decimal Amount { get; set; }

    /// <summary>Admin who performed the refund; NULL only for backfilled legacy rows.</summary>
    public Guid? AdminId { get; set; }

    /// <summary>Timestamp of the refund operation.</summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Reservation Reservation { get; set; } = null!;
}
