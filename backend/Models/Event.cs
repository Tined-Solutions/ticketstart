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

    /// <summary>
    /// EA-001: approval status of the event. New events start <see cref="EventStatus.Pending"/>;
    /// only <see cref="EventStatus.Approved"/> events surface in the buyer-facing catalog.
    /// DB default is Pending (0); the migration backfills pre-existing rows to Approved.
    /// </summary>
    public EventStatus Status { get; set; } = EventStatus.Pending;

    /// <summary>
    /// EHE-001: an event is expired when its start instant is strictly BEFORE
    /// <paramref name="asOf"/>. At the exact start instant (Date == asOf) the event
    /// is NOT expired — the predicate uses strict &lt;, not &lt;=. Pure function,
    /// no side effects; unit-testable in isolation.
    /// </summary>
    public bool IsExpired(DateTime asOf) => Date < asOf;

    // Navigation properties
    public User Organizer { get; set; } = null!;
    public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
