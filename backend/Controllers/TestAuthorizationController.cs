using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Test controller to verify authorization attributes are working correctly.
/// This controller demonstrates the different authorization patterns used in the system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestAuthorizationController : ControllerBase
{
    private readonly ILogger<TestAuthorizationController> _logger;

    public TestAuthorizationController(ILogger<TestAuthorizationController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Public endpoint - no authorization required
    /// </summary>
    [HttpGet("public")]
    public IActionResult PublicEndpoint()
    {
        return Ok(new { message = "This is a public endpoint - no authentication required" });
    }

    /// <summary>
    /// Protected endpoint - requires any authenticated user
    /// </summary>
    [HttpGet("protected")]
    [Authorize]
    public IActionResult ProtectedEndpoint()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        return Ok(new 
        { 
            message = "This is a protected endpoint - authentication required",
            userId = userId,
            role = role
        });
    }

    /// <summary>
    /// Admin-only endpoint - requires Admin role
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminOnlyEndpoint()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        return Ok(new 
        { 
            message = "This is an admin-only endpoint",
            userId = userId
        });
    }

    /// <summary>
    /// Organizador or Admin endpoint - requires Organizador or Admin role
    /// </summary>
    [HttpGet("organizador")]
    [Authorize(Policy = "RequireOrganizadorRole")]
    public IActionResult OrganizadorEndpoint()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        return Ok(new 
        { 
            message = "This endpoint requires Organizador or Admin role",
            userId = userId,
            role = role
        });
    }

    /// <summary>
    /// Staff or Admin endpoint - requires Staff or Admin role
    /// </summary>
    [HttpGet("staff")]
    [Authorize(Policy = "RequireStaffRole")]
    public IActionResult StaffEndpoint()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        return Ok(new 
        { 
            message = "This endpoint requires Staff or Admin role",
            userId = userId,
            role = role
        });
    }

    /// <summary>
    /// Event ownership endpoint - requires event ownership or Admin role
    /// This demonstrates the custom EventOwnership authorization policy
    /// </summary>
    [HttpGet("event/{id}")]
    [Authorize(Policy = "EventOwnership")]
    public IActionResult EventOwnershipEndpoint(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        return Ok(new 
        { 
            message = "This endpoint requires event ownership or Admin role",
            eventId = id,
            userId = userId,
            role = role
        });
    }
}
