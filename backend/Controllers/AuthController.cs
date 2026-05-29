using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Authentication controller for user registration and login.
/// All endpoints are public (no authorization required).
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
    /// Register a new user account.
    /// Public endpoint - no authorization required.
    /// </summary>
    /// <param name="request">Registration details including email, password, and role</param>
    /// <returns>JWT token and user information</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
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

        var result = await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            _logger.LogWarning("Registration failed for email {Email}: {Error}", request.Email, result.Error);
            
            if (result.Error.Contains("already exists"))
            {
                return Conflict(new { error = result.Error });
            }
            
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("User registered successfully: {Email}", request.Email);

        return Ok(new
        {
            token = result.Token,
            userId = result.UserId,
            role = result.Role.ToString()
        });
    }

    /// <summary>
    /// Authenticate a user and return a JWT token.
    /// Public endpoint - no authorization required.
    /// </summary>
    /// <param name="request">Login credentials including email and password</param>
    /// <returns>JWT token and user information</returns>
    [HttpPost("login")]
    [AllowAnonymous]
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

        return Ok(new
        {
            token = result.Token,
            userId = result.UserId,
            role = result.Role.ToString()
        });
    }
}
