namespace TicketeraOnline.Api.Models;

/// <summary>
/// An organizer tried to delete their event after it was approved. Organizers may
/// delete their own events only while they are NOT approved (Pending/Rejected);
/// once approved, only an Admin can. Mapped to 409 (problem type "event-approved")
/// so the UI can explain it — typically an Admin approved it moments earlier.
/// </summary>
public class EventAlreadyApprovedException : Exception
{
    public EventAlreadyApprovedException()
        : base("The event has already been approved and can no longer be deleted by its organizer") { }
}
