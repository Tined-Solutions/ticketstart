using Microsoft.AspNetCore.Authorization;

namespace TicketeraOnline.Api.Authorization;

/// <summary>
/// Authorization requirement that verifies the user owns the event or is an Admin
/// </summary>
public class EventOwnershipRequirement : IAuthorizationRequirement
{
    // No additional properties needed - the requirement itself is the marker
}
