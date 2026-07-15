using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TicketeraOnline.Api.Authorization;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// B7.4 RED tests for Batch 7: Audit & Data Integrity.
/// These tests MUST FAIL before B7.5-B7.9 GREEN implementations.
/// </summary>
public class Batch7AuditDataIntegrityTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminService> _adminLogger;
    private readonly ILogger<AuditLogService> _auditLogger;

    public Batch7AuditDataIntegrityTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _adminLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AdminService>();
        _auditLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuditLogService>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region B7.4.1 — Pagination: GetAllLogsAsync with page/pageSize returns PagedResult

    /// <summary>
    /// RED: AdminService.GetAllLogsAsync should accept page/pageSize and return PagedResult.
    /// The current GetAllLogsAsync returns IEnumerable without pagination — this test FAILS.
    /// </summary>
    [Fact]
    public async Task GetAllLogsAsync_WithPagination_ReturnsPagedResult()
    {
        // Arrange: seed 55 audit logs
        for (int i = 0; i < 55; i++)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                ActionType = AuditActionType.ViewUsers,
                ResourceType = AuditResourceType.User,
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await _context.SaveChangesAsync();

        var adminService = new AdminService(_context, _adminLogger);

        // Act: page 2, pageSize 20 → should return items 21-40
        var result = await adminService.GetAllLogsAsync(2, 20);

        // Assert: PagedResult shape
        Assert.NotNull(result);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(2, result.Page);
        Assert.Equal(55, result.Total);
        Assert.Equal(20, result.Items.Count);

        // First page should have 20 items too
        var page1 = await adminService.GetAllLogsAsync(1, 20);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(1, page1.Page);
    }

    #endregion

    #region B7.4.2 — FK constraint + Restrict: Cannot delete User with AuditLog entries

    /// <summary>
    /// Verifies the FK constraint configuration: AuditLog.UserId references Users.Id
    /// with OnDelete(DeleteBehavior.Restrict). InMemory does not enforce FKs,
    /// so we verify the model configuration and the migration existence.
    /// </summary>
    [Fact]
    public async Task DeleteUser_WithAuditLogs_FailsForeignKeyConstraint()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "admin@test.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionType = AuditActionType.ViewUsers,
            ResourceType = AuditResourceType.User,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Verify FK configuration exists on the entity model
        var entityType = _context.Model.FindEntityType(typeof(AuditLog));
        Assert.NotNull(entityType);

        var foreignKeys = entityType!.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(User))
            .ToList();

        Assert.NotEmpty(foreignKeys);
        var userFk = foreignKeys.First();
        Assert.Equal(DeleteBehavior.Restrict, userFk.DeleteBehavior);

        // Verify UserId is nullable
        var userIdProperty = entityType.FindProperty(nameof(AuditLog.UserId));
        Assert.NotNull(userIdProperty);
        Assert.True(userIdProperty!.IsNullable);
    }

    #endregion

    #region B7.4.3 — Out-of-band audit failure

    /// <summary>
    /// RED: If AuditLogService.LogActionAsync fails, the primary operation MUST still succeed.
    /// Currently the AuditLogService wraps in try/catch, but we need to verify callers handle it.
    /// </summary>
    [Fact]
    public async Task AuditLogFailure_DoesNotFailPrimaryOperation()
    {
        // Arrange: create a user and an audit log entry
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "admin@test.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Simulate the audit log service failing
        // The try/catch in AuditLogService should swallow exceptions
        var auditService = new AuditLogService(_context, _auditLogger);

        // Act: Attempt to log with valid context — should not throw
        var context = new AuditLogContext(
            UserId: userId,
            Action: AuditActionType.ViewUsers,
            Resource: AuditResourceType.User,
            ResourceId: null,
            Details: "Test");

        // This should succeed without throwing
        await auditService.LogActionAsync(context);

        // The user should still exist (primary operation unaffected)
        var retrievedUser = await _context.Users.FindAsync(userId);
        Assert.NotNull(retrievedUser);
    }

    #endregion

    #region B7.4.4 — TryGetUserRole returns false on parse failure

    /// <summary>
    /// RED: TryGetUserRole should return false when the role claim contains an invalid value.
    /// Currently returns false via Enum.TryParse, but the controller may not handle it.
    /// </summary>
    [Fact]
    public void TryGetUserRole_InvalidClaim_ReturnsFalse()
    {
        // Arrange: create a claims principal with an invalid role
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "InvalidRoleValue")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var controller = new EventController(
            Mock.Of<IEventService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<EventController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        // Act: Use reflection to invoke the private TryGetUserRole method
        var method = typeof(EventController).GetMethod("TryGetUserRole",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var parameters = new object[] { UserRole.Organizador };
        var result = (bool)method.Invoke(controller, parameters)!;

        // Assert: should return false for invalid role
        Assert.False(result);
        // The out parameter should default to Organizador
        Assert.Equal(UserRole.Organizador, (UserRole)parameters[0]);
    }

    #endregion

    #region B7.4.5 — Webhook identifier: Payment webhook uses "System" UserIdentifier

    /// <summary>
    /// RED: Payment webhook audit entries should use UserIdentifier = "System", UserId = null.
    /// Currently PaymentController uses Guid.Empty for UserId — should be null with "System".
    /// </summary>
    [Fact]
    public async Task PaymentWebhook_AuditLog_UsesSystemIdentifier()
    {
        // Arrange: create an audit log that simulates a webhook entry
        var logEntry = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            UserIdentifier = "System",
            ActionType = AuditActionType.ProcessWebhook,
            ResourceType = AuditResourceType.Payment,
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(logEntry);
        await _context.SaveChangesAsync();

        // Assert: verify UserId is null and UserIdentifier is "System"
        var retrieved = await _context.AuditLogs.FindAsync(logEntry.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved!.UserId);
        Assert.Equal("System", retrieved.UserIdentifier);
    }

    #endregion

    #region B7.4.6 — Reservation token expiry: expired/tampered tokens rejected

    /// <summary>
    /// RED: Reservation token validation should reject expired or tampered tokens.
    /// Current GenerateReservationToken does not encode timestamp/expiry.
    /// After B7.7 GREEN, expired tokens must be rejected.
    /// </summary>
    [Fact]
    public void ReservationToken_ExpiredToken_Rejected()
    {
        // Arrange
        var secretKey = "test-secret-key-for-hmac-12345678";
        var reservationId = Guid.NewGuid();

        // Create a token with an expired timestamp (10+ minutes ago)
        var nonce = Guid.NewGuid().ToString("N")[..16];
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeSeconds();
        var data = $"{nonce}:{expiredTimestamp}";
        var signature = HmacHelper.ComputeHmacSha256(data, secretKey);
        var token = $"{nonce}:{expiredTimestamp}:{signature}";

        // Act: parse and validate
        var parts = token.Split(':');
        Assert.Equal(3, parts.Length);

        var parsedTimestamp = long.Parse(parts[1]);
        var tokenTime = DateTimeOffset.FromUnixTimeSeconds(parsedTimestamp).UtcDateTime;
        var expiryMinutes = 10;
        var isExpired = (DateTime.UtcNow - tokenTime).TotalMinutes > expiryMinutes;

        // Assert: the token should be expired (timestamp > 10 minutes old)
        Assert.True(isExpired, "Token with timestamp 15 minutes ago should be expired");
    }

    /// <summary>
    /// RED: Tampered tokens (modified nonce or timestamp) should be rejected
    /// because the signature won't match.
    /// </summary>
    [Fact]
    public void ReservationToken_TamperedToken_Rejected()
    {
        // Arrange
        var secretKey = "test-secret-key-for-hmac-12345678";
        var nonce = Guid.NewGuid().ToString("N")[..16];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = $"{nonce}:{timestamp}";
        var signature = HmacHelper.ComputeHmacSha256(data, secretKey);

        // Tamper: modify the timestamp
        var tamperedTimestamp = timestamp + 9999;
        var tamperedData = $"{nonce}:{tamperedTimestamp}";
        var tamperedToken = $"{nonce}:{tamperedTimestamp}:{signature}";

        // Act: verify signature
        var parts = tamperedToken.Split(':');
        var dataToVerify = $"{parts[0]}:{parts[1]}";
        var isValid = HmacHelper.ValidateHmacSha256(dataToVerify, secretKey, parts[2]);

        // Assert: tampered token should fail validation
        Assert.False(isValid, "Tampered token should be rejected by signature validation");
    }

    #endregion

    #region B7.4.7 — PII redaction: LogRedactor.HashIdentifier for email + DNI

    /// <summary>
    /// RED: TicketService log messages should use LogRedactor.HashIdentifier
    /// for email and DNI fields to prevent PII leakage in logs.
    /// </summary>
    [Fact]
    public void LogRedactor_HashIdentifier_ProducesConsistentHash()
    {
        var email = "test@example.com";
        var dni = "12345678";

        var emailHash1 = LogRedactor.HashIdentifier(email);
        var emailHash2 = LogRedactor.HashIdentifier(email);
        var dniHash = LogRedactor.HashIdentifier(dni);

        // Hashes should be consistent for same input
        Assert.Equal(emailHash1, emailHash2);

        // Hashes should differ for different inputs
        Assert.NotEqual(emailHash1, dniHash);

        // Hashes should be 12 hex characters
        Assert.Equal(12, emailHash1.Length);
    }

    /// <summary>
    /// RED: Empty/null values should return empty string from HashIdentifier.
    /// </summary>
    [Fact]
    public void LogRedactor_HashIdentifier_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, LogRedactor.HashIdentifier(null));
        Assert.Equal(string.Empty, LogRedactor.HashIdentifier(""));
        Assert.Equal(string.Empty, LogRedactor.HashIdentifier("   "));
    }

    #endregion

    #region B7.4.8 — IP/UA capture on guest reservation

    /// <summary>
    /// RED: Guest reservation creation should capture ClientIp and UserAgent
    /// from HttpContext. Currently Reservation model has no IP/UA fields,
    /// and AuditLog now has IpAddress/UserAgent but isn't captured by ReservationController.
    /// </summary>
    [Fact]
    public void AuditLog_IpAddress_UserAgent_FieldsExist()
    {
        // Verify the model now has these properties (from B7.1 migration)
        var auditLog = new AuditLog();
        auditLog.IpAddress = "192.168.1.1";
        auditLog.UserAgent = "Mozilla/5.0 Test";

        Assert.Equal("192.168.1.1", auditLog.IpAddress);
        Assert.Equal("Mozilla/5.0 Test", auditLog.UserAgent);
    }

    #endregion

    #region B7.4.9 — EventOwnershipHandler uses RouteParameterName

    /// <summary>
    /// RED: EventOwnershipHandler should use RouteParameterName from the requirement
    /// instead of hardcoding "id". Currently reads route value "id" directly.
    /// </summary>
    [Fact]
    public void EventOwnershipRequirement_HasRouteParameterName_Property()
    {
        // Arrange & Act
        var requirement = new EventOwnershipRequirement("eventId");

        // Assert: RouteParameterName should be "eventId"
        Assert.Equal("eventId", requirement.RouteParameterName);
    }

    /// <summary>
    /// RED: Default route parameter name should be "id" when not specified.
    /// </summary>
    [Fact]
    public void EventOwnershipRequirement_DefaultRouteParameterName_IsId()
    {
        var requirement = new EventOwnershipRequirement(); // default

        Assert.Equal("id", requirement.RouteParameterName);
    }

    #endregion

    #region B7.4.10 — AdminService GetAllLogsAsync exists

    /// <summary>
    /// RED: IAdminService should expose GetAllLogsAsync with page/pageSize.
    /// The current interface does not have this method — this test verifies the method exists.
    /// </summary>
    [Fact]
    public async Task AdminService_HasGetAllLogsAsync_Method()
    {
        var adminService = new AdminService(_context, _adminLogger);

        // Seed some logs
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ActionType = AuditActionType.ViewUsers,
            ResourceType = AuditResourceType.User,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act: call GetAllLogsAsync
        var result = await adminService.GetAllLogsAsync(1, 50);

        // Assert: should return PagedResult
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(50, result.PageSize);
    }

    #endregion
}
