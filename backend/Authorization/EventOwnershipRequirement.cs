using Microsoft.AspNetCore.Authorization;

namespace TicketeraOnline.Api.Authorization;

/// <summary>
/// Authorization requirement that verifies the user owns the event or is an Admin.
/// </summary>
public class EventOwnershipRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The route parameter name to read the event ID from.
    /// Defaults to "id".
    /// </summary>
    public string RouteParameterName { get; }

    /// <summary>
    /// Creates a requirement with the default route parameter name "id".
    /// </summary>
    public EventOwnershipRequirement()
    {
        RouteParameterName = "id";
    }

    /// <summary>
    /// Creates a requirement with a custom route parameter name.
    /// </summary>
    /// <param name="routeParameterName">The route parameter name (e.g., "id", "eventId").</param>
    public EventOwnershipRequirement(string routeParameterName)
    {
        RouteParameterName = string.IsNullOrWhiteSpace(routeParameterName)
            ? "id"
            : routeParameterName;
    }
}
