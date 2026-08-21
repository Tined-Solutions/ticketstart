namespace TicketeraOnline.Api.Models;

/// <summary>
/// Thrown by <c>EventFinalizedGuard.EnsureMutable</c> (PEM-001) when an Admin or
/// Organizer attempts to mutate an event whose start instant has already passed
/// (<c>Date &lt; now</c>). Mapped to 409 Conflict with RFC 7807
/// <c>type: "event-finalized"</c> and title "Event has already finished" (D-1).
/// Distinct from <see cref="EventExpiredException"/> ("event-expired" — purchase
/// guard): the two rules share the IsExpired predicate but encode different
/// business meanings, and clients MAY handle them differently.
/// </summary>
public class EventFinalizedException : Exception
{
    public EventFinalizedException() : base("Event has already finished") { }
}