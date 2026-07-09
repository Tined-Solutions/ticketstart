using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for EventController.
/// Validates admin modify/delete audit logging and audit failure handling.
/// </summary>
public class EventControllerTests
{
    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<ILogger<EventController>> _mockLogger;
    private readonly EventController _controller;

    public EventControllerTests()
    {
        _mockEventService = new Mock<IEventService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockLogger = new Mock<ILogger<EventController>>();
        _controller = new EventController(_mockEventService.Object, _mockAuditLogService.Object, _mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region PUT /api/events/{id} - Admin audit

    [Fact]
    public async Task UpdateEvent_AdminRole_LogsUpdateEventAudit()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var request = new UpdateEventRequest
        {
            Name = "Updated Event",
            Description = "Updated",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location"
        };
        var updatedEvent = new Event
        {
            Id = eventId,
            Name = request.Name,
            Description = request.Description,
            Date = request.Date,
            Location = request.Location,
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var eventDetails = new EventWithAvailability
        {
            Id = eventId,
            Name = request.Name,
            Description = request.Description,
            Date = request.Date,
            Location = request.Location,
            OrganizerId = updatedEvent.OrganizerId
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService.Setup(s => s.UpdateEventAsync(eventId, request, adminId, UserRole.Admin)).ReturnsAsync(updatedEvent);
        _mockEventService.Setup(s => s.GetEventByIdAsync(eventId)).ReturnsAsync(eventDetails);

        // Act
        var result = await _controller.UpdateEvent(eventId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<EventWithAvailability>(okResult.Value);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.UpdateEvent &&
            c.Resource == AuditResourceType.Event &&
            c.ResourceId == eventId)), Times.Once);
    }

    [Fact]
    public async Task UpdateEvent_AdminRole_AuditLogFails_StillReturnsOk()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var request = new UpdateEventRequest
        {
            Name = "Updated Event",
            Description = "Updated",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location"
        };
        var updatedEvent = new Event
        {
            Id = eventId,
            Name = request.Name,
            Description = request.Description,
            Date = request.Date,
            Location = request.Location,
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var eventDetails = new EventWithAvailability
        {
            Id = eventId,
            Name = request.Name,
            Description = request.Description,
            Date = request.Date,
            Location = request.Location,
            OrganizerId = updatedEvent.OrganizerId
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService.Setup(s => s.UpdateEventAsync(eventId, request, adminId, UserRole.Admin)).ReturnsAsync(updatedEvent);
        _mockEventService.Setup(s => s.GetEventByIdAsync(eventId)).ReturnsAsync(eventDetails);
        _mockAuditLogService.Setup(s => s.LogActionAsync(It.IsAny<AuditLogContext>())).ThrowsAsync(new InvalidOperationException("Audit failure"));

        // Act
        var result = await _controller.UpdateEvent(eventId, request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
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

    #region DELETE /api/events/{id} - Admin audit

    [Fact]
    public async Task DeleteEvent_AdminRole_LogsDeleteEventAudit()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService.Setup(s => s.DeleteEventAsync(eventId, adminId, UserRole.Admin)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteEvent(eventId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.DeleteEvent &&
            c.Resource == AuditResourceType.Event &&
            c.ResourceId == eventId)), Times.Once);
    }

    [Fact]
    public async Task DeleteEvent_AdminRole_AuditLogFails_StillReturnsNoContent()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService.Setup(s => s.DeleteEventAsync(eventId, adminId, UserRole.Admin)).Returns(Task.CompletedTask);
        _mockAuditLogService.Setup(s => s.LogActionAsync(It.IsAny<AuditLogContext>())).ThrowsAsync(new InvalidOperationException("Audit failure"));

        // Act
        var result = await _controller.DeleteEvent(eventId);

        // Assert
        Assert.IsType<NoContentResult>(result);
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

    #region Non-admin owner does not log audit

    [Fact]
    public async Task UpdateEvent_OwnerRole_DoesNotLogAdminAudit()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var request = new UpdateEventRequest
        {
            Name = "Updated Event",
            Description = "Updated",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location"
        };
        var updatedEvent = new Event
        {
            Id = eventId,
            Name = request.Name,
            Description = request.Description,
            Date = request.Date,
            Location = request.Location,
            OrganizerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        SetAuthenticatedUser(ownerId, UserRole.Organizador);
        _mockEventService.Setup(s => s.UpdateEventAsync(eventId, request, ownerId, UserRole.Organizador)).ReturnsAsync(updatedEvent);
        _mockEventService.Setup(s => s.GetEventByIdAsync(eventId)).ReturnsAsync(new EventWithAvailability { Id = eventId });

        // Act
        await _controller.UpdateEvent(eventId, request);

        // Assert
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
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
