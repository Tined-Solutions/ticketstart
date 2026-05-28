namespace TicketeraOnline.Api.Models;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string MercadoPagoId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Reservation Reservation { get; set; } = null!;
}
