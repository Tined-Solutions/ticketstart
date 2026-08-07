namespace TicketeraOnline.Api.Models;

public class TicketType
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Event Event { get; set; } = null!;
}
