using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using System.Reflection;
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
            _mockEventService.Object,
            new Mock<IAdminPurchaseService>().Object)
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

    #region EA-003/004 — Approve / Reject event moderation

    [Fact]
    public async Task ApproveEvent_AdminRole_ReturnsOkAndAudits()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var summary = new EventSummary
        {
            Id = eventId,
            Name = "Pending Event",
            Date = DateTime.UtcNow.AddDays(10),
            Location = "Venue",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Status = EventStatus.Approved
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.ApproveEventAsync(eventId)).ReturnsAsync(summary);

        // Act
        var result = await _controller.ApproveEvent(eventId);

        // Assert — 200 with the updated summary + ApproveEvent audit (EA-003)
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<EventSummary>(okResult.Value);
        Assert.Equal(EventStatus.Approved, value.Status);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.ApproveEvent &&
            c.Resource == AuditResourceType.Event &&
            c.ResourceId == eventId)), Times.Once);
    }

    [Fact]
    public async Task RejectEvent_WithReason_ReturnsOkAndAuditsReason()
    {
        // Arrange — optional reason is audit-only, included in Details (EA-004)
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var summary = new EventSummary
        {
            Id = eventId,
            Name = "Pending Event",
            Date = DateTime.UtcNow.AddDays(10),
            Location = "Venue",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Status = EventStatus.Rejected
        };
        const string reason = "Contenido promocional no verificado";

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.RejectEventAsync(eventId, reason)).ReturnsAsync(summary);

        // Act
        var result = await _controller.RejectEvent(eventId, new RejectEventRequest(reason));

        // Assert — 200 + audit carries the reason
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<EventSummary>(okResult.Value);
        Assert.Equal(EventStatus.Rejected, value.Status);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.RejectEvent &&
            c.Resource == AuditResourceType.Event &&
            c.ResourceId == eventId &&
            c.Details!.Contains(reason, StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    [Fact]
    public async Task RejectEvent_LongReason_IsTruncatedTo1000()
    {
        // Arrange — Details is capped at varchar(1000) (EA-004)
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var longReason = new string('x', 1500);
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.RejectEventAsync(eventId, longReason))
            .ReturnsAsync(new EventSummary { Id = eventId, Status = EventStatus.Rejected });

        // Act
        var result = await _controller.RejectEvent(eventId, new RejectEventRequest(longReason));

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.Action == AuditActionType.RejectEvent &&
            c.Details != null &&
            c.Details.Length <= 1000)), Times.Once);
    }

    [Fact]
    public async Task RejectEvent_WithoutReason_ReturnsOk()
    {
        // Arrange — reason MAY be null (EA-004, not mandatory)
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.RejectEventAsync(eventId, null))
            .ReturnsAsync(new EventSummary { Id = eventId, Status = EventStatus.Rejected });

        // Act
        var result = await _controller.RejectEvent(eventId, new RejectEventRequest(null));

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.Action == AuditActionType.RejectEvent &&
            c.ResourceId == eventId)), Times.Once);
    }

    [Fact]
    public async Task ApproveEvent_UnknownEvent_ReturnsNotFound_NoAudit()
    {
        // Arrange — EA-003: unknown event → 404 and NO audit entry
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.ApproveEventAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new KeyNotFoundException("Event not found"));

        // Act
        var result = await _controller.ApproveEvent(Guid.NewGuid());

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task RejectEvent_UnknownEvent_ReturnsNotFound_NoAudit()
    {
        // Arrange — EA-004: unknown event → 404 and NO audit entry
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.RejectEventAsync(It.IsAny<Guid>(), It.IsAny<string?>()))
            .ThrowsAsync(new KeyNotFoundException("Event not found"));

        // Act
        var result = await _controller.RejectEvent(Guid.NewGuid(), new RejectEventRequest("x"));

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task ApproveEvent_NoUserId_ReturnsUnauthorized()
    {
        // Act — no authenticated user
        var result = await _controller.ApproveEvent(Guid.NewGuid());

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ApproveEvent_ServiceThrows_ReturnsInternalServerError()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.ApproveEventAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.ApproveEvent(Guid.NewGuid());

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public void ApproveReject_Endpoints_InheritClassLevelRequireAdminRole()
    {
        // EA-003/004 non-admin scenario: the class-level RequireAdminRole policy
        // covers BOTH new endpoints — no AllowAnonymous at action level.
        var approve = typeof(AdminController).GetMethod(nameof(AdminController.ApproveEvent));
        var reject = typeof(AdminController).GetMethod(nameof(AdminController.RejectEvent));
        Assert.NotNull(approve);
        Assert.NotNull(reject);
        Assert.Null(approve!.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>(false));
        Assert.Null(reject!.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>(false));
    }

    #endregion

    #region PEM-002 — Past-event mutations → 409 event-finalized, no audit

    [Fact]
    public async Task ApproveEvent_PastEvent_Returns409EventFinalized_NoAudit()
    {
        // EA-003 MODIFIED: a past event throws EventFinalizedException from the
        // service; the controller maps it to 409 ProblemDetails and writes NO audit.
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.ApproveEventAsync(eventId))
            .ThrowsAsync(new EventFinalizedException());

        var result = await _controller.ApproveEvent(eventId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(409, problem.Status);
        Assert.Equal("event-finalized", problem.Type);
        Assert.Equal("Event has already finished", problem.Title);
        Assert.Contains("no longer be modified", problem.Detail);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task RejectEvent_PastEvent_Returns409EventFinalized_NoAudit()
    {
        // EA-004 MODIFIED: same 409 mapping for reject, no audit entry.
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.RejectEventAsync(eventId, It.IsAny<string?>()))
            .ThrowsAsync(new EventFinalizedException());

        var result = await _controller.RejectEvent(eventId, new RejectEventRequest("too late"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("event-finalized", problem.Type);
        Assert.Equal("Event has already finished", problem.Title);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task AddTicketStock_PastEvent_Returns409EventFinalized_NoAudit()
    {
        // ATS-002 MODIFIED: stock increment on a past event → 409, no audit.
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ttId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService.Setup(s => s.AddTicketStockAsync(eventId, ttId, 50))
            .ThrowsAsync(new EventFinalizedException());

        var result = await _controller.AddTicketStock(eventId, ttId, new AddTicketStockRequest(50));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("event-finalized", problem.Type);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task AddTicketType_PastEvent_Returns409EventFinalized_NoAudit()
    {
        // ATS-004 MODIFIED: new ticket type on a past event → 409, no audit.
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService.Setup(s => s.AddTicketTypeAsync(eventId, "VIP", 150m, 20))
            .ThrowsAsync(new EventFinalizedException());

        var result = await _controller.AddTicketType(eventId, new AddTicketTypeRequest("VIP", 150m, 20));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("event-finalized", problem.Type);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    #endregion

    #region PUT /api/admin/users/{userId}/role (AUM-001)

    [Fact]
    public async Task UpdateUserRole_Success_ReturnsOkWithSummary_AndAuditsTargetUser()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var summary = new UserSummary
        {
            Id = targetId,
            Name = "Target User",
            Email = "target@example.com",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.UpdateUserRoleAsync(targetId, UserRole.Organizador))
            .ReturnsAsync(summary);

        // Act
        var result = await _controller.UpdateUserRole(targetId, new AdminUpdateUserRoleRequest(UserRole.Organizador));

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<UserSummary>(okResult.Value);
        Assert.Equal(targetId, value.Id);
        Assert.Equal(UserRole.Organizador, value.Role);

        // D10: audit references the TARGET user id, details carry ids + role only.
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.UpdateUserRole &&
            c.Resource == AuditResourceType.User &&
            c.ResourceId == targetId &&
            c.Details == $"Admin updated role for user {targetId} to {UserRole.Organizador}")), Times.Once);
    }

    [Fact]
    public async Task UpdateUserRole_SelfEdit_Returns400_NeitherServiceNorAuditRun()
    {
        // D4: the self-edit guard lives in the controller BEFORE the service call —
        // no role change and no audit row may be persisted (AUM-001 scenario 2).
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        var result = await _controller.UpdateUserRole(adminId, new AdminUpdateUserRoleRequest(UserRole.Staff));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        _mockAdminService.Verify(s => s.UpdateUserRoleAsync(It.IsAny<Guid>(), It.IsAny<UserRole>()), Times.Never);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserRole_UnknownUser_Returns404_NoAudit()
    {
        var adminId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.UpdateUserRoleAsync(unknownId, It.IsAny<UserRole>()))
            .ThrowsAsync(new KeyNotFoundException($"User {unknownId} not found"));

        var result = await _controller.UpdateUserRole(unknownId, new AdminUpdateUserRoleRequest(UserRole.Admin));

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserRole_ServiceThrows_Returns500()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAdminService.Setup(s => s.UpdateUserRoleAsync(targetId, It.IsAny<UserRole>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var result = await _controller.UpdateUserRole(targetId, new AdminUpdateUserRoleRequest(UserRole.Admin));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region POST /api/admin/users/{userId}/reset-password (AUM-003)

    [Fact]
    public async Task ResetPassword_Success_ReturnsOkWithTempPassword_AndAuditsWithoutCredential()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        const string tempPassword = "TempPass123456";
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAuthService.Setup(s => s.ResetPasswordAsync(targetId))
            .ReturnsAsync(new ResetPasswordResult
            {
                Success = true,
                TemporaryPassword = tempPassword,
                UserId = targetId
            });

        // Act
        var result = await _controller.ResetPassword(targetId);

        // Assert — the cleartext credential exists in exactly one place: the body.
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<AdminResetPasswordResponse>(okResult.Value);
        Assert.Equal(tempPassword, value.TemporaryPassword);

        // D10/D11: audit details carry ids only and NEVER the credential.
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.ResetPassword &&
            c.Resource == AuditResourceType.User &&
            c.ResourceId == targetId &&
            c.Details == $"Admin reset password for user {targetId}" &&
            (c.Details == null || !c.Details.Contains(tempPassword, StringComparison.Ordinal)))), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_UnknownUser_Returns404_NoAudit()
    {
        var adminId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAuthService.Setup(s => s.ResetPasswordAsync(unknownId))
            .ReturnsAsync(new ResetPasswordResult { Success = false, Error = "User not found" });

        var result = await _controller.ResetPassword(unknownId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_SelfReset_Returns200_GuardIsRoleOnly()
    {
        // D4: the self guard applies to ROLE EDIT only — self reset is allowed
        // (no lockout risk: the admin sets their own new password afterwards).
        var adminId = Guid.NewGuid();
        const string tempPassword = "SelfResetPass1";
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAuthService.Setup(s => s.ResetPasswordAsync(adminId))
            .ReturnsAsync(new ResetPasswordResult { Success = true, TemporaryPassword = tempPassword, UserId = adminId });

        var result = await _controller.ResetPassword(adminId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<AdminResetPasswordResponse>(okResult.Value);
        Assert.Equal(tempPassword, value.TemporaryPassword);
    }

    [Fact]
    public async Task ResetPassword_ServiceThrows_Returns500()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockAuthService.Setup(s => s.ResetPasswordAsync(targetId))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var result = await _controller.ResetPassword(targetId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
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
