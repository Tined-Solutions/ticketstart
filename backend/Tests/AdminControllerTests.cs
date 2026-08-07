using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using System.Text.Json;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for AdminController.
/// Validates Requirements 14.4, 14.5, 14.6
/// </summary>
public class AdminControllerTests
{
    private readonly Mock<IAdminService> _mockAdminService;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<ILogger<AdminController>> _mockLogger;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _mockAdminService = new Mock<IAdminService>();
        _mockAuthService = new Mock<IAuthService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockEventService = new Mock<IEventService>();
        _mockLogger = new Mock<ILogger<AdminController>>();
        _controller = new AdminController(
            _mockAdminService.Object,
            _mockAuthService.Object,
            _mockAuditLogService.Object,
            _mockLogger.Object,
            _mockEventService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GET /api/admin/users

    [Fact]
    public async Task GetAllUsers_AdminRole_ReturnsOkWithPagedUsers()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var users = new List<UserSummary>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Email = "user1@example.com",
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Email = "user2@example.com",
                Role = UserRole.Staff,
                CreatedAt = DateTime.UtcNow
            }
        };
        var pagedUsers = new PagedResult<UserSummary>
        {
            Items = users,
            Total = 2,
            Page = 1,
            PageSize = 50
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.GetAllUsersAsync(1, 50)).ReturnsAsync(pagedUsers);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<PagedResult<UserSummary>>(okResult.Value);
        Assert.Equal(2, value.Total);
        Assert.Equal(2, value.Items.Count);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.ViewUsers &&
            c.Resource == AuditResourceType.User &&
            c.ResourceId == null &&
            c.Details == "Admin viewed all users")), Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_RespectsPaginationParameters()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var users = new List<UserSummary>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Email = "paged@example.com",
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            }
        };
        var pagedUsers = new PagedResult<UserSummary>
        {
            Items = users,
            Total = 5,
            Page = 2,
            PageSize = 10
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.GetAllUsersAsync(2, 10)).ReturnsAsync(pagedUsers);

        // Act
        var result = await _controller.GetAllUsers(page: 2, pageSize: 10);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<PagedResult<UserSummary>>(okResult.Value);
        Assert.Equal(2, value.Page);
        Assert.Equal(10, value.PageSize);
        Assert.Equal(5, value.Total);
    }

    [Fact]
    public async Task GetAllUsers_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.GetAllUsersAsync(1, 50)).ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_AuditLogFails_StillReturnsOkWithData()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pagedUsers = new PagedResult<UserSummary>
        {
            Items = new List<UserSummary>(),
            Total = 0,
            Page = 1,
            PageSize = 50
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.GetAllUsersAsync(1, 50)).ReturnsAsync(pagedUsers);
        _mockAuditLogService.Setup(s => s.LogActionAsync(It.IsAny<AuditLogContext>())).ThrowsAsync(new InvalidOperationException("Audit failure"));

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<PagedResult<UserSummary>>(okResult.Value);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Audit logging failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_UserSummary_DoesNotExposePasswordHashInJson()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pagedUsers = new PagedResult<UserSummary>
        {
            Items = new List<UserSummary>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Email = "user@example.com",
                    Role = UserRole.Organizador,
                    CreatedAt = DateTime.UtcNow
                }
            },
            Total = 1,
            Page = 1,
            PageSize = 50
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.GetAllUsersAsync(1, 50)).ReturnsAsync(pagedUsers);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region POST /api/admin/users

    [Fact]
    public async Task CreateUser_AdminRole_ReturnsCreatedWithUserData()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var createdUserId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        _mockAuthService.Setup(s => s.CreateUserAsync(
                "Juan Perez",
                "juan@example.com",
                "password123",
                UserRole.Organizador))
            .ReturnsAsync(new CreateUserResult
            {
                Success = true,
                UserId = createdUserId,
                Name = "Juan Perez",
                Email = "juan@example.com",
                Role = UserRole.Organizador
            });

        // Act
        var result = await _controller.CreateUser(new AdminCreateUserRequest(
            "Juan Perez",
            "juan@example.com",
            "password123",
            UserRole.Organizador));

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        var value = Assert.IsType<TicketeraOnline.Api.Controllers.AdminUserResponse>(createdResult.Value);
        Assert.Equal(createdUserId, value.Id);
        Assert.Equal("Juan Perez", value.Name);
        Assert.Equal("juan@example.com", value.Email);
        Assert.Equal(UserRole.Organizador, value.Role);

        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.CreateUser &&
            c.Resource == AuditResourceType.User &&
            c.ResourceId == createdUserId &&
            c.Details == "Admin created user juan@example.com with role Organizador")), Times.Once);
    }

    [Fact]
    public async Task CreateUser_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        _mockAuthService.Setup(s => s.CreateUserAsync(
                "Invalid Email",
                "not-an-email",
                "password123",
                UserRole.Organizador))
            .ReturnsAsync(new CreateUserResult
            {
                Success = false,
                Error = "Invalid email format"
            });

        // Act
        var result = await _controller.CreateUser(new AdminCreateUserRequest(
            "Invalid Email",
            "not-an-email",
            "password123",
            UserRole.Organizador));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task CreateUser_ShortPassword_ReturnsBadRequest()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        _mockAuthService.Setup(s => s.CreateUserAsync(
                "Short Password",
                "short@example.com",
                "1234567",
                UserRole.Organizador))
            .ReturnsAsync(new CreateUserResult
            {
                Success = false,
                Error = "Password must be at least 8 characters long"
            });

        // Act
        var result = await _controller.CreateUser(new AdminCreateUserRequest(
            "Short Password",
            "short@example.com",
            "1234567",
            UserRole.Organizador));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        _mockAuthService.Setup(s => s.CreateUserAsync(
                "Duplicate User",
                "duplicate@example.com",
                "password123",
                UserRole.Organizador))
            .ReturnsAsync(new CreateUserResult
            {
                Success = false,
                Error = "User with this email already exists"
            });

        // Act
        var result = await _controller.CreateUser(new AdminCreateUserRequest(
            "Duplicate User",
            "duplicate@example.com",
            "password123",
            UserRole.Organizador));

        // Assert
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task CreateUser_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        _mockAuthService.Setup(s => s.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<UserRole>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.CreateUser(new AdminCreateUserRequest(
            "User",
            "user@example.com",
            "password123",
            UserRole.Organizador));

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_AuditLogFails_StillReturnsCreated()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var createdUserId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        _mockAuthService.Setup(s => s.CreateUserAsync(
                "Juan Perez",
                "juan@example.com",
                "password123",
                UserRole.Organizador))
            .ReturnsAsync(new CreateUserResult
            {
                Success = true,
                UserId = createdUserId,
                Name = "Juan Perez",
                Email = "juan@example.com",
                Role = UserRole.Organizador
            });

        _mockAuditLogService.Setup(s => s.LogActionAsync(It.IsAny<AuditLogContext>())).ThrowsAsync(new InvalidOperationException("Audit failure"));

        // Act
        var result = await _controller.CreateUser(new AdminCreateUserRequest(
            "Juan Perez",
            "juan@example.com",
            "password123",
            UserRole.Organizador));

        // Assert
        Assert.IsType<CreatedResult>(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Audit logging failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GET /api/admin/events

    [Fact]
    public async Task GetAllEvents_AdminRole_ReturnsOkWithPagedEvents()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var events = new List<EventSummary>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Event 1",
                Date = DateTime.UtcNow.AddDays(10),
                Location = "Location 1",
                OrganizerId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Event 2",
                Date = DateTime.UtcNow.AddDays(20),
                Location = "Location 2",
                OrganizerId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            }
        };
        var pagedEvents = new PagedResult<EventSummary>
        {
            Items = events,
            Total = 2,
            Page = 1,
            PageSize = 50
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.GetAllEventsAsync(1, 50)).ReturnsAsync(pagedEvents);

        // Act
        var result = await _controller.GetAllEvents();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<PagedResult<EventSummary>>(okResult.Value);
        Assert.Equal(2, value.Total);
        Assert.Equal(2, value.Items.Count);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.ViewEvents &&
            c.Resource == AuditResourceType.Event &&
            c.ResourceId == null &&
            c.Details == "Admin viewed all events")), Times.Once);
    }

    [Fact]
    public async Task GetAllEvents_NoUserId_ReturnsUnauthorized()
    {
        // Arrange
        // No authenticated user

        // Act
        var result = await _controller.GetAllEvents();

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task GetAllEvents_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.GetAllEventsAsync(1, 50)).ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.GetAllEvents();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    #endregion

    #region GET /api/admin/audit-logs

    [Fact]
    public async Task GetAuditLogs_NoFilter_ReturnsAllLogs()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var logs = new List<AuditLogEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = adminId,
                ActionType = AuditActionType.ViewUsers,
                ResourceType = AuditResourceType.User,
                Timestamp = DateTime.UtcNow
            }
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAuditLogService.Setup(s => s.GetAllLogsAsync()).ReturnsAsync(logs);

        // Act
        var result = await _controller.GetAuditLogs();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsAssignableFrom<IEnumerable<AuditLogEntry>>(okResult.Value);
        Assert.Single(value);
    }

    [Fact]
    public async Task GetAuditLogs_WithUserIdFilter_CallsGetLogsForUserAsync()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var logs = new List<AuditLogEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = targetUserId,
                ActionType = AuditActionType.ViewEvents,
                ResourceType = AuditResourceType.Event,
                Timestamp = DateTime.UtcNow
            }
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAuditLogService.Setup(s => s.GetLogsForUserAsync(targetUserId)).ReturnsAsync(logs);

        // Act
        var result = await _controller.GetAuditLogs(userId: targetUserId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsAssignableFrom<IEnumerable<AuditLogEntry>>(okResult.Value);
        Assert.Single(value);
        _mockAuditLogService.Verify(s => s.GetLogsForUserAsync(targetUserId), Times.Once);
        _mockAuditLogService.Verify(s => s.GetAllLogsAsync(), Times.Never);
    }

    #endregion

    private void SetAuthenticatedUser(Guid userId, UserRole role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }
}
