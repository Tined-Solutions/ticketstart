namespace TicketeraOnline.Api.Models;

/// <summary>
/// Thrown when an authenticated user attempts an action they are not authorized to perform.
/// Maps to HTTP 403 Forbidden in the global exception handler.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// ATE-002: thrown when a full ticket-type replacement is attempted for an event
/// that is not eligible for it (<see cref="EventStatus.Approved"/> events keep the
/// add-only flow). Mapped to HTTP 409 with RFC 7807
/// <c>type: "ticket-types-not-editable"</c>.
/// </summary>
public class TicketTypesNotEditableException : Exception
{
    public TicketTypesNotEditableException()
        : base("Ticket types can only be fully edited for pending or rejected events")
    {
    }
}

/// <summary>
/// ATE-003: thrown when a full ticket-type replacement is attempted for a
/// Pending/Rejected event that already owns commercial history (ANY
/// <c>Ticket</c> or <c>Reservation</c> row, in any state). Deleting a referenced
/// type would violate the Restrict FK, so the replacement is rejected instead.
/// Mapped to HTTP 409 with RFC 7807 <c>type: "ticket-types-referenced"</c>.
/// </summary>
public class TicketTypesReferencedException : Exception
{
    public TicketTypesReferencedException()
        : base("This event already has tickets or reservations, so its ticket types cannot be fully edited. Add-only operations remain available.")
    {
    }
}
