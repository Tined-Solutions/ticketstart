namespace TicketeraOnline.Api.Models;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string MercadoPagoId { get; set; } = string.Empty;
    /// <summary>Ticket subtotal only (organizer revenue). Excludes the service fee.</summary>
    public decimal Amount { get; set; }
    /// <summary>Service fee charged to the buyer on top of <see cref="Amount"/>.</summary>
    public decimal ServiceFee { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Reservation Reservation { get; set; } = null!;
}
