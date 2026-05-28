namespace TicketeraOnline.Api.Models;

public class Reservation
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public int Quantity { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public Event Event { get; set; } = null!;
    public TicketType TicketType { get; set; } = null!;
}
