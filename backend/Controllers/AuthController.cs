using Microsoft.AspNetCore.Mvc;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

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

    [HttpPost("register")]
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

    [HttpPost("login")]
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
