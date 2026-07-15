using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Authorization;

/// <summary>
/// Authorization handler that verifies event ownership or admin role
/// </summary>
public class EventOwnershipHandler : AuthorizationHandler<EventOwnershipRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public EventOwnershipHandler(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EventOwnershipRequirement requirement)
    {
        // Get user ID from claims
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return; // Fail - no valid user ID
        }

        // Get user role from claims
        var roleClaim = context.User.FindFirst(ClaimTypes.Role);
        if (roleClaim == null)
        {
            return; // Fail - no role claim
        }

        // Admins have access to all events
        if (Enum.TryParse<UserRole>(roleClaim.Value, out var role) && role == UserRole.Admin)
        {
            context.Succeed(requirement);
            return;
        }

        // Get event ID from route
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return; // Fail - no HTTP context
        }

        var eventIdString = httpContext.Request.RouteValues[requirement.RouteParameterName]?.ToString();
        if (string.IsNullOrEmpty(eventIdString) || !Guid.TryParse(eventIdString, out var eventId))
        {
            return; // Fail - no valid event ID in route
        }

        // Check event ownership using scoped DbContext
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var eventExists = await dbContext.Events
            .AnyAsync(e => e.Id == eventId && e.OrganizerId == userId);

        if (eventExists)
        {
            context.Succeed(requirement);
        }
    }
}
