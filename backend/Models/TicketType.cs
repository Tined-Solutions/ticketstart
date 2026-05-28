using System.ComponentModel.DataAnnotations;

namespace TicketeraOnline.Api.Models;

public class TicketType
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation properties
    public Event Event { get; set; } = null!;
}
