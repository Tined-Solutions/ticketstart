using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Authentication controller for user login, session info, and logout.
/// Public registration has been removed; only administrators can create users.
/// Uses httpOnly cookies for JWT storage instead of exposing tokens in the response body.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate a user and set a JWT httpOnly cookie.
    /// The token is NOT returned in the response body for security.
    /// Public endpoint - no authorization required.
    /// </summary>
    /// <param name="request">Login credentials including email and password</param>
    /// <returns>User information (without token)</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Email is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Password is required" });
        }

        var result = await _authService.LoginAsync(request);

        if (!result.Success)
        {
            _logger.LogWarning("Login failed for email {Email}: {Error}", request.Email, result.Error);
            return Unauthorized(new { error = result.Error });
        }

        _logger.LogInformation("User logged in successfully: {Email}", request.Email);

        // Set httpOnly cookie with the JWT token
        Response.Cookies.Append("token", result.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(7)
        });

        // Return user info in body WITHOUT the token
        return Ok(new
        {
            userId = result.UserId,
            role = result.Role.ToString(),
            name = result.Name
        });
    }

    /// <summary>
    /// Returns the current authenticated user's information from the JWT claims.
    /// Requires authentication via the httpOnly cookie.
    /// </summary>
    /// <returns>User id, email, name, and role</returns>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
        var nameClaim = User.FindFirst(ClaimTypes.Name)?.Value;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<UserRole>(roleClaim, out var role))
        {
            return Unauthorized();
        }

        return Ok(new
        {
            id = userId,
            email = emailClaim ?? string.Empty,
            name = nameClaim ?? string.Empty,
            role = role
        });
    }

    /// <summary>
    /// Clears the authentication cookie, effectively logging out the user.
    /// </summary>
    /// <returns>200 OK with cleared cookie</returns>
    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        Response.Cookies.Append("token", string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(-1)
        });

        return Ok();
    }
}
