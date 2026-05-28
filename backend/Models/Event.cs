namespace TicketeraOnline.Api.Models;

public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public Guid OrganizerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User Organizer { get; set; } = null!;
    public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
