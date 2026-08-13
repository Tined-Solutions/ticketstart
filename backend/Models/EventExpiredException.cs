namespace TicketeraOnline.Api.Models;

/// <summary>
/// Thrown by the purchase guards (EHE-004/EHE-005) when a buyer attempts to
/// create a reservation or a payment preference for an event whose start
/// instant has already passed. Mapped to 409 Conflict with RFC 7807
/// <c>type: "event-expired"</c> and title "Event has already started" (ADR-5).
/// </summary>
public class EventExpiredException : Exception
{
    public EventExpiredException() : base("Event has already started") { }
}
