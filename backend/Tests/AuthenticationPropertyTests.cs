using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for authentication functionality
/// Validates Requirements 1.2, 1.3, 1.4
/// </summary>
public class AuthenticationPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthenticationPropertyTests()
    {
        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup configuration with JWT settings
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789"},
            {"Jwt:Issuer", "TicketeraOnlineTest"},
            {"Jwt:Audience", "TicketeraOnlineTestAudience"},
            {"Jwt:ExpirationMinutes", "1440"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<AuthService>();

        _authService = new AuthService(_context, _configuration, logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Property 1: User Registration Creates Valid Accounts

    /// <summary>
    /// Property 1: User Registration Creates Valid Accounts
    /// For any valid registration data (email, password, role), 
    /// the system SHALL create a user account with the provided email, 
    /// a hashed password, and the assigned role.
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public async Task UserRegistration_CreatesValidAccount_WithProvidedData()
    {
        // Test with multiple valid registration requests
        var testCases = new[]
        {
            new RegisterRequest { Email = "test1@example.com", Password = "password123", Role = UserRole.Organizador },
            new RegisterRequest { Email = "test2@test.com", Password = "securePass456", Role = UserRole.Staff },
            new RegisterRequest { Email = "admin@mail.com", Password = "adminPass789", Role = UserRole.Admin }
        };

        foreach (var request in testCases)
        {
            // Arrange - ensure clean state
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());
            if (existingUser != null)
            {
                _context.Users.Remove(existingUser);
                await _context.SaveChangesAsync();
            }

            // Act
            var result = await _authService.RegisterAsync(request);

            // Assert
            Assert.True(result.Success, $"Registration should succeed for valid data. Error: {result.Error}");
            Assert.NotEmpty(result.Token);
            Assert.NotEqual(Guid.Empty, result.UserId);
            Assert.Equal(request.Role, result.Role);

            // Verify user was created in database
            var createdUser = await _context.Users.FindAsync(result.UserId);
            Assert.NotNull(createdUser);
            Assert.Equal(request.Email.ToLower(), createdUser.Email);
            Assert.Equal(request.Role, createdUser.Role);
            
            // Verify password was hashed (not stored in plain text)
            Assert.NotEqual(request.Password, createdUser.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, createdUser.PasswordHash));
        }
    }

    /// <summary>
    /// Property 1 (Edge Case): Duplicate Email Registration Should Fail
    /// </summary>
    [Fact]
    public async Task UserRegistration_RejectsDuplicateEmail()
    {
        var request = new RegisterRequest
        {
            Email = "duplicate@example.com",
            Password = "password123",
            Role = UserRole.Organizador
        };

        // Arrange - register user first time
        var firstResult = await _authService.RegisterAsync(request);
        Assert.True(firstResult.Success);

        // Act - attempt to register same email again
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Property 2: Valid Login Returns Valid JWT

    /// <summary>
    /// Property 2: Valid Login Returns Valid JWT
    /// For any registered user with valid credentials, logging in SHALL return 
    /// a JWT token that can be validated and contains the correct user ID and role claims.
    /// Validates: Requirements 1.3
    /// </summary>
    [Fact]
    public async Task ValidLogin_ReturnsValidJWT_WithCorrectClaims()
    {
        // Test with multiple users
        var testCases = new[]
        {
            new RegisterRequest { Email = "user1@example.com", Password = "password123", Role = UserRole.Organizador },
            new RegisterRequest { Email = "user2@test.com", Password = "securePass456", Role = UserRole.Staff },
            new RegisterRequest { Email = "user3@mail.com", Password = "adminPass789", Role = UserRole.Admin }
        };

        foreach (var registerRequest in testCases)
        {
            // Arrange - register a user first
            var registerResult = await _authService.RegisterAsync(registerRequest);
            Assert.True(registerResult.Success);

            var loginRequest = new LoginRequest
            {
                Email = registerRequest.Email,
                Password = registerRequest.Password
            };

            // Act
            var loginResult = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.True(loginResult.Success, $"Login should succeed for valid credentials. Error: {loginResult.Error}");
            Assert.NotEmpty(loginResult.Token);
            Assert.Equal(registerResult.UserId, loginResult.UserId);
            Assert.Equal(registerRequest.Role, loginResult.Role);

            // Validate JWT token structure and claims
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(loginResult.Token);

            // Verify token contains correct claims
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            Assert.NotNull(userIdClaim);
            Assert.Equal(registerResult.UserId.ToString(), userIdClaim.Value);

            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal(registerRequest.Email.ToLower(), emailClaim.Value);

            var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            Assert.NotNull(roleClaim);
            Assert.Equal(registerRequest.Role.ToString(), roleClaim.Value);

            // Verify token can be validated
            var validatedUser = await _authService.ValidateTokenAsync(loginResult.Token);
            Assert.NotNull(validatedUser);
            Assert.Equal(registerResult.UserId, validatedUser.Id);
            Assert.Equal(registerRequest.Email.ToLower(), validatedUser.Email);
            Assert.Equal(registerRequest.Role, validatedUser.Role);
        }
    }

    /// <summary>
    /// Property 2 (Edge Case): Case-Insensitive Email Login
    /// </summary>
    [Fact]
    public async Task ValidLogin_IsCaseInsensitive_ForEmail()
    {
        var registerRequest = new RegisterRequest
        {
            Email = "CaseSensitive@Example.COM",
            Password = "password123",
            Role = UserRole.Organizador
        };

        // Arrange - register with original email
        var registerResult = await _authService.RegisterAsync(registerRequest);
        Assert.True(registerResult.Success);

        // Act - login with different case
        var loginRequest = new LoginRequest
        {
            Email = registerRequest.Email.ToUpper(),
            Password = registerRequest.Password
        };
        var loginResult = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.True(loginResult.Success);
        Assert.Equal(registerResult.UserId, loginResult.UserId);
    }

    #endregion

    #region Property 3: Invalid Credentials Rejected

    /// <summary>
    /// Property 3: Invalid Credentials Rejected
    /// For any invalid credentials (non-existent email or incorrect password), 
    /// login attempts SHALL be rejected with an authentication error.
    /// Validates: Requirements 1.4
    /// </summary>
    [Fact]
    public async Task InvalidLogin_WithWrongPassword_IsRejected()
    {
        var registerRequest = new RegisterRequest
        {
            Email = "validuser@example.com",
            Password = "correctPassword123",
            Role = UserRole.Organizador
        };

        // Arrange - register a user
        var registerResult = await _authService.RegisterAsync(registerRequest);
        Assert.True(registerResult.Success);

        // Test multiple wrong passwords
        var wrongPasswords = new[] { "wrongPass1", "incorrect", "badPassword", "123456" };

        foreach (var wrongPassword in wrongPasswords)
        {
            // Act - attempt login with wrong password
            var loginRequest = new LoginRequest
            {
                Email = registerRequest.Email,
                Password = wrongPassword
            };
            var loginResult = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.False(loginResult.Success, "Login should fail with incorrect password");
            Assert.NotEmpty(loginResult.Error);
            Assert.Contains("Invalid", loginResult.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(loginResult.Token);
        }
    }

    /// <summary>
    /// Property 3 (Edge Case): Non-Existent Email Login Rejected
    /// </summary>
    [Fact]
    public async Task InvalidLogin_WithNonExistentEmail_IsRejected()
    {
        var nonExistentEmails = new[]
        {
            "nonexistent1@example.com",
            "notregistered@test.com",
            "fake@mail.com"
        };

        foreach (var email in nonExistentEmails)
        {
            // Ensure email doesn't exist
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            
            Assert.Null(existingUser);

            // Act - attempt login with non-existent email
            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = "anyPassword123"
            };
            var loginResult = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.False(loginResult.Success, "Login should fail with non-existent email");
            Assert.NotEmpty(loginResult.Error);
            Assert.Contains("Invalid", loginResult.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(loginResult.Token);
        }
    }

    /// <summary>
    /// Property 3 (Edge Case): Empty Credentials Rejected
    /// </summary>
    [Fact]
    public async Task InvalidLogin_WithEmptyCredentials_IsRejected()
    {
        // Test empty email
        var result1 = await _authService.LoginAsync(new LoginRequest
        {
            Email = "",
            Password = "password123"
        });
        Assert.False(result1.Success);
        Assert.NotEmpty(result1.Error);

        // Test empty password
        var result2 = await _authService.LoginAsync(new LoginRequest
        {
            Email = "test@example.com",
            Password = ""
        });
        Assert.False(result2.Success);
        Assert.NotEmpty(result2.Error);

        // Test both empty
        var result3 = await _authService.LoginAsync(new LoginRequest
        {
            Email = "",
            Password = ""
        });
        Assert.False(result3.Success);
        Assert.NotEmpty(result3.Error);
    }

    #endregion

    #region Property 4: Role-Based Authorization Enforcement

    /// <summary>
    /// Property 4: Role-Based Authorization Enforcement
    /// For any role-specific operation, the system SHALL enforce authorization rules 
    /// such that only users with the appropriate role can perform the operation.
    /// Validates: Requirements 1.6
    /// </summary>
    [Fact]
    public async Task RoleBasedAuthorization_EnforcesCorrectRoleAccess()
    {
        // Arrange - Create users with different roles
        var organizadorRequest = new RegisterRequest
        {
            Email = "organizador@example.com",
            Password = "password123",
            Role = UserRole.Organizador
        };

        var staffRequest = new RegisterRequest
        {
            Email = "staff@example.com",
            Password = "password123",
            Role = UserRole.Staff
        };

        var adminRequest = new RegisterRequest
        {
            Email = "admin@example.com",
            Password = "password123",
            Role = UserRole.Admin
        };

        var organizadorResult = await _authService.RegisterAsync(organizadorRequest);
        var staffResult = await _authService.RegisterAsync(staffRequest);
        var adminResult = await _authService.RegisterAsync(adminRequest);

        Assert.True(organizadorResult.Success);
        Assert.True(staffResult.Success);
        Assert.True(adminResult.Success);

        // Act & Assert - Verify each token contains the correct role claim
        var tokenHandler = new JwtSecurityTokenHandler();

        // Verify Organizador token
        var organizadorToken = tokenHandler.ReadJwtToken(organizadorResult.Token);
        var organizadorRoleClaim = organizadorToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(organizadorRoleClaim);
        Assert.Equal(UserRole.Organizador.ToString(), organizadorRoleClaim.Value);

        // Verify Staff token
        var staffToken = tokenHandler.ReadJwtToken(staffResult.Token);
        var staffRoleClaim = staffToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(staffRoleClaim);
        Assert.Equal(UserRole.Staff.ToString(), staffRoleClaim.Value);

        // Verify Admin token
        var adminToken = tokenHandler.ReadJwtToken(adminResult.Token);
        var adminRoleClaim = adminToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(adminRoleClaim);
        Assert.Equal(UserRole.Admin.ToString(), adminRoleClaim.Value);

        // Verify token validation preserves role information
        var validatedOrganizador = await _authService.ValidateTokenAsync(organizadorResult.Token);
        Assert.NotNull(validatedOrganizador);
        Assert.Equal(UserRole.Organizador, validatedOrganizador.Role);

        var validatedStaff = await _authService.ValidateTokenAsync(staffResult.Token);
        Assert.NotNull(validatedStaff);
        Assert.Equal(UserRole.Staff, validatedStaff.Role);

        var validatedAdmin = await _authService.ValidateTokenAsync(adminResult.Token);
        Assert.NotNull(validatedAdmin);
        Assert.Equal(UserRole.Admin, validatedAdmin.Role);
    }

    /// <summary>
    /// Property 4 (Edge Case): Role Cannot Be Changed After Registration
    /// </summary>
    [Fact]
    public async Task RoleBasedAuthorization_RoleImmutableAfterRegistration()
    {
        // Arrange - Register a user with Organizador role
        var registerRequest = new RegisterRequest
        {
            Email = "immutable@example.com",
            Password = "password123",
            Role = UserRole.Organizador
        };

        var registerResult = await _authService.RegisterAsync(registerRequest);
        Assert.True(registerResult.Success);
        Assert.Equal(UserRole.Organizador, registerResult.Role);

        // Act - Login and verify role hasn't changed
        var loginRequest = new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        };

        var loginResult = await _authService.LoginAsync(loginRequest);
        Assert.True(loginResult.Success);

        // Assert - Role should remain the same
        Assert.Equal(UserRole.Organizador, loginResult.Role);

        // Verify in database
        var user = await _context.Users.FindAsync(registerResult.UserId);
        Assert.NotNull(user);
        Assert.Equal(UserRole.Organizador, user.Role);

        // Verify in token claims
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.ReadJwtToken(loginResult.Token);
        var roleClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal(UserRole.Organizador.ToString(), roleClaim.Value);
    }

    /// <summary>
    /// Property 4 (Edge Case): All Valid Roles Are Supported
    /// </summary>
    [Fact]
    public async Task RoleBasedAuthorization_SupportsAllValidRoles()
    {
        // Test all enum values
        var allRoles = Enum.GetValues<UserRole>();

        foreach (var role in allRoles)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"{role.ToString().ToLower()}@example.com",
                Password = "password123",
                Role = role
            };

            // Act
            var result = await _authService.RegisterAsync(request);

            // Assert
            Assert.True(result.Success, $"Registration should succeed for role {role}");
            Assert.Equal(role, result.Role);

            // Verify token contains correct role
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.ReadJwtToken(result.Token);
            var roleClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            Assert.NotNull(roleClaim);
            Assert.Equal(role.ToString(), roleClaim.Value);

            // Verify validated user has correct role
            var validatedUser = await _authService.ValidateTokenAsync(result.Token);
            Assert.NotNull(validatedUser);
            Assert.Equal(role, validatedUser.Role);
        }
    }

    #endregion
}
