namespace TicketeraOnline.Api.Models;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public Guid? ReservationId { get; set; }
    public string PurchaserEmail { get; set; } = string.Empty;
    public string PurchaserDNI { get; set; } = string.Empty;
    public string QRCodeData { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public bool IsRefunded { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Event Event { get; set; } = null!;
    public TicketType TicketType { get; set; } = null!;
    public Reservation? Reservation { get; set; }
}
