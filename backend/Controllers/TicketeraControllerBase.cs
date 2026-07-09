using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Base controller for Ticketera Online API controllers.
/// Provides common helpers such as user ID extraction from JWT claims.
/// </summary>
[ApiController]
public abstract class TicketeraControllerBase : ControllerBase
{
    /// <summary>
    /// Tries to extract the user ID from the authenticated user's claims.
    /// </summary>
    protected bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }
}
