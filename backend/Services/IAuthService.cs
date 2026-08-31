using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

public interface IAuthService
{
    Task<CreateUserResult> CreateUserAsync(string name, string email, string password, UserRole role);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<User?> ValidateTokenAsync(string token);

    /// <summary>
    /// Generates a cryptographically secure temporary password, persists ONLY
    /// its BCrypt hash, and returns the cleartext credential exactly once
    /// (AUM-003, D8). The credential is never logged or audited.
    /// </summary>
    /// <param name="targetUserId">ID of the user whose password is reset</param>
    /// <returns>Result with the one-time credential, or a failure whose Error
    /// is the pinned string "User not found" when the user does not exist (D6)</returns>
    Task<ResetPasswordResult> ResetPasswordAsync(Guid targetUserId);
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
    public string Name { get; set; } = string.Empty;
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

/// <summary>
/// Result of an admin-triggered password reset (AUM-003, D8). TemporaryPassword
/// is the cleartext one-time credential — it exists ONLY in this result and is
/// returned once in the reset response; it must never be logged or audited.
/// </summary>
public class ResetPasswordResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}
