using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

public interface IAuthService
{
    Task<CreateUserResult> CreateUserAsync(string name, string email, string password, UserRole role);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<User?> ValidateTokenAsync(string token);
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResult
{
    public bool Success { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
}

public class CreateUserResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
