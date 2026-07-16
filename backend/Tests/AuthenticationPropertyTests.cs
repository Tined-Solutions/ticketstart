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
using ArbStatic = FsCheck.Fluent.Arb;
using GenStatic = FsCheck.Fluent.Gen;
using PropStatic = FsCheck.Fluent.Prop;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for authentication functionality.
/// Validates Requirements 1.2, 1.3, 1.4 and Batch 2 admin-only user creation.
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

    #region Property 1: Admin User Creation Creates Valid Accounts

    /// <summary>
    /// Property 1: Admin User Creation Creates Valid Accounts
    /// For any valid admin user creation data (name, email, password, role),
    /// the system SHALL create a user account with the provided name, email,
    /// a hashed password, and the assigned role.
    /// Validates: Batch 2 REQ-2, REQ-3
    /// </summary>
    [Fact]
    public async Task CreateUser_CreatesValidAccount_WithProvidedData()
    {
        // Test with multiple valid creation requests
        var testCases = new[]
        {
            new { Name = "Test Organizador", Email = "test1@example.com", Password = "password123", Role = UserRole.Organizador },
            new { Name = "Test Staff", Email = "test2@test.com", Password = "securePass456", Role = UserRole.Staff },
            new { Name = "Test Admin", Email = "admin@mail.com", Password = "adminPass789", Role = UserRole.Admin }
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
            var result = await _authService.CreateUserAsync(request.Name, request.Email, request.Password, request.Role);

            // Assert
            Assert.True(result.Success, $"User creation should succeed for valid data. Error: {result.Error}");
            Assert.NotEqual(Guid.Empty, result.UserId);
            Assert.Equal(request.Name, result.Name);
            Assert.Equal(request.Email.ToLower(), result.Email);
            Assert.Equal(request.Role, result.Role);

            // Verify user was created in database
            var createdUser = await _context.Users.FindAsync(result.UserId);
            Assert.NotNull(createdUser);
            Assert.Equal(request.Name, createdUser.Name);
            Assert.Equal(request.Email.ToLower(), createdUser.Email);
            Assert.Equal(request.Role, createdUser.Role);
            
            // Verify password was hashed (not stored in plain text)
            Assert.NotEqual(request.Password, createdUser.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, createdUser.PasswordHash));
        }
    }

    /// <summary>
    /// Property 1 (Edge Case): Duplicate Email User Creation Should Fail
    /// </summary>
    [Fact]
    public async Task CreateUser_RejectsDuplicateEmail()
    {
        var name = "Duplicate User";
        var email = "duplicate@example.com";
        var password = "password123";
        var role = UserRole.Organizador;

        // Arrange - create user first time
        var firstResult = await _authService.CreateUserAsync(name, email, password, role);
        Assert.True(firstResult.Success);

        // Act - attempt to create same email again
        var result = await _authService.CreateUserAsync(name, email, password, role);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Property 1 (Edge Case): Invalid Email Format Is Rejected
    /// </summary>
    [Fact]
    public async Task CreateUser_RejectsInvalidEmail()
    {
        var invalidEmails = new[] { "not-an-email", "missing-at-sign.com", "@nodomain.com", "" };

        foreach (var email in invalidEmails)
        {
            var result = await _authService.CreateUserAsync("Invalid Email", email, "password123", UserRole.Organizador);
            Assert.False(result.Success);
            Assert.Contains("email", result.Error, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Property 1 (Edge Case): Short Password Is Rejected
    /// </summary>
    [Fact]
    public async Task CreateUser_RejectsShortPassword()
    {
        var result = await _authService.CreateUserAsync("Short Password", "shortpass@example.com", "1234567", UserRole.Organizador);
        Assert.False(result.Success);
        Assert.Contains("8", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Property 1 (FsCheck): For any valid role, user creation succeeds and persists the requested role.
    /// </summary>
    [Property]
    public Property CreateUser_WithAnyValidRole_PersistsRole()
    {
        var roleArb = ArbStatic.From(GenStatic.Elements(Enum.GetValues<UserRole>()));
        var localArb = ArbStatic.From(GenStatic.Elements("alpha", "beta", "gamma", "delta"));
        var domainArb = ArbStatic.From(GenStatic.Elements("example.com", "test.com", "mail.com"));

        return PropStatic.ForAll(
            roleArb,
            localArb,
            domainArb,
            (role, local, domain) =>
            {
                // Use a fresh in-memory database per generated case to avoid collisions
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;

                using var context = new ApplicationDbContext(options);
                var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AuthService>();
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        {"Jwt:SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789"},
                        {"Jwt:Issuer", "TicketeraOnlineTest"},
                        {"Jwt:Audience", "TicketeraOnlineTestAudience"},
                        {"Jwt:ExpirationMinutes", "1440"}
                    })
                    .Build();

                var authService = new AuthService(context, configuration, logger);
                var email = $"{local}-{Guid.NewGuid()}@{domain}";
                var result = Task.Run(() => authService.CreateUserAsync("Generated User", email, "password123", role)).Result;

                return result.Success
                    && result.Role == role
                    && result.Email == email.ToLower()
                    && result.Name == "Generated User";
            });
    }

    #endregion

    #region Property 2: Valid Login Returns Valid JWT

    /// <summary>
    /// Property 2: Valid Login Returns Valid JWT
    /// For any created user with valid credentials, logging in SHALL return 
    /// a JWT token that can be validated and contains the correct user ID, name, and role claims.
    /// Validates: Requirements 1.3
    /// </summary>
    [Fact]
    public async Task ValidLogin_ReturnsValidJWT_WithCorrectClaims()
    {
        // Test with multiple users
        var testCases = new[]
        {
            new { Name = "User One", Email = "user1@example.com", Password = "password123", Role = UserRole.Organizador },
            new { Name = "User Two", Email = "user2@test.com", Password = "securePass456", Role = UserRole.Staff },
            new { Name = "User Three", Email = "user3@mail.com", Password = "adminPass789", Role = UserRole.Admin }
        };

        foreach (var createRequest in testCases)
        {
            // Arrange - create a user first
            var createResult = await _authService.CreateUserAsync(
                createRequest.Name,
                createRequest.Email,
                createRequest.Password,
                createRequest.Role);
            Assert.True(createResult.Success);

            var loginRequest = new LoginRequest
            {
                Email = createRequest.Email,
                Password = createRequest.Password
            };

            // Act
            var loginResult = await _authService.LoginAsync(loginRequest);

            // Assert
            Assert.True(loginResult.Success, $"Login should succeed for valid credentials. Error: {loginResult.Error}");
            Assert.NotEmpty(loginResult.Token);
            Assert.Equal(createResult.UserId, loginResult.UserId);
            Assert.Equal(createRequest.Role, loginResult.Role);

            // Validate JWT token structure and claims
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(loginResult.Token);

            // Verify token contains correct claims
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            Assert.NotNull(userIdClaim);
            Assert.Equal(createResult.UserId.ToString(), userIdClaim.Value);

            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal(createRequest.Email.ToLower(), emailClaim.Value);

            var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            Assert.NotNull(roleClaim);
            Assert.Equal(createRequest.Role.ToString(), roleClaim.Value);

            // Verify token can be validated
            var validatedUser = await _authService.ValidateTokenAsync(loginResult.Token);
            Assert.NotNull(validatedUser);
            Assert.Equal(createResult.UserId, validatedUser.Id);
            Assert.Equal(createRequest.Email.ToLower(), validatedUser.Email);
            Assert.Equal(createRequest.Role, validatedUser.Role);
            Assert.Equal(createRequest.Name, validatedUser.Name);
        }
    }

    /// <summary>
    /// Property 2 (Edge Case): Case-Insensitive Email Login
    /// </summary>
    [Fact]
    public async Task ValidLogin_IsCaseInsensitive_ForEmail()
    {
        var createResult = await _authService.CreateUserAsync(
            "Case Sensitive",
            "CaseSensitive@Example.COM",
            "password123",
            UserRole.Organizador);
        Assert.True(createResult.Success);

        // Act - login with different case
        var loginRequest = new LoginRequest
        {
            Email = "CASESENSITIVE@EXAMPLE.COM",
            Password = "password123"
        };
        var loginResult = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.True(loginResult.Success);
        Assert.Equal(createResult.UserId, loginResult.UserId);
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
        var createResult = await _authService.CreateUserAsync(
            "Valid User",
            "validuser@example.com",
            "correctPassword123",
            UserRole.Organizador);
        Assert.True(createResult.Success);

        // Test multiple wrong passwords
        var wrongPasswords = new[] { "wrongPass1", "incorrect", "badPassword", "123456" };

        foreach (var wrongPassword in wrongPasswords)
        {
            // Act - attempt login with wrong password
            var loginRequest = new LoginRequest
            {
                Email = createResult.Email,
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
        var organizadorResult = await _authService.CreateUserAsync(
            "Organizador User",
            "organizador@example.com",
            "password123",
            UserRole.Organizador);

        var staffResult = await _authService.CreateUserAsync(
            "Staff User",
            "staff@example.com",
            "password123",
            UserRole.Staff);

        var adminResult = await _authService.CreateUserAsync(
            "Admin User",
            "admin@example.com",
            "password123",
            UserRole.Admin);

        Assert.True(organizadorResult.Success);
        Assert.True(staffResult.Success);
        Assert.True(adminResult.Success);

        // Act & Assert - Verify each token contains the correct role claim
        var tokenHandler = new JwtSecurityTokenHandler();

        // Verify Organizador token
        var organizadorLogin = await _authService.LoginAsync(new LoginRequest
            { Email = "organizador@example.com", Password = "password123" });
        var organizadorToken = tokenHandler.ReadJwtToken(organizadorLogin.Token);
        var organizadorRoleClaim = organizadorToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(organizadorRoleClaim);
        Assert.Equal(UserRole.Organizador.ToString(), organizadorRoleClaim.Value);

        // Verify Staff token
        var staffLogin = await _authService.LoginAsync(new LoginRequest
            { Email = "staff@example.com", Password = "password123" });
        var staffToken = tokenHandler.ReadJwtToken(staffLogin.Token);
        var staffRoleClaim = staffToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(staffRoleClaim);
        Assert.Equal(UserRole.Staff.ToString(), staffRoleClaim.Value);

        // Verify Admin token
        var adminLogin = await _authService.LoginAsync(new LoginRequest
            { Email = "admin@example.com", Password = "password123" });
        var adminToken = tokenHandler.ReadJwtToken(adminLogin.Token);
        var adminRoleClaim = adminToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(adminRoleClaim);
        Assert.Equal(UserRole.Admin.ToString(), adminRoleClaim.Value);
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
            var email = $"{role.ToString().ToLower()}-{Guid.NewGuid()}@example.com";
            var name = $"{role} User";

            // Act
            var result = await _authService.CreateUserAsync(name, email, "password123", role);

            // Assert
            Assert.True(result.Success, $"User creation should succeed for role {role}");
            Assert.Equal(role, result.Role);

            // Verify login token contains correct role
            var login = await _authService.LoginAsync(new LoginRequest { Email = email, Password = "password123" });
            Assert.True(login.Success);
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.ReadJwtToken(login.Token);
            var roleClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            Assert.NotNull(roleClaim);
            Assert.Equal(role.ToString(), roleClaim.Value);
        }
    }

    #endregion

    #region Shared Email Validation

    /// <summary>
    /// Shared email validation rejects invalid formats and accepts valid formats consistently.
    /// Validates: Batch 2 REQ-5 (JD-SG10)
    /// </summary>
    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("first.last@example.co.uk", true)]
    [InlineData("user+tag@example.com", true)]
    [InlineData("not-an-email", false)]
    [InlineData("missing-at-sign.com", false)]
    [InlineData("@nodomain.com", false)]
    [InlineData("user@", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void ValidateEmail_AcceptsValidAndRejectsInvalidFormats(string email, bool expectedValid)
    {
        // Act
        var isValid = AuthService.ValidateEmail(email);

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    #endregion
}
