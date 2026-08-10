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
/// Controller-level RED tests for the admin add-ticket-stock endpoints.
/// Validates ATS-002 (increment), ATS-004 (new type), ATS-005 (audit) and D-5 error mapping.
/// </summary>
public class AdminControllerTicketStockTests
{
    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<ILogger<AdminController>> _mockLogger;
    private readonly AdminController _controller;

    public AdminControllerTicketStockTests()
    {
        _mockEventService = new Mock<IEventService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockLogger = new Mock<ILogger<AdminController>>();
        _controller = new AdminController(
            new Mock<IAdminService>().Object,
            new Mock<IAuthService>().Object,
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

    private static TicketTypeWithAvailability TicketType(Guid id, string name = "General", decimal price = 100m, int quantity = 150)
        => new()
        {
            Id = id,
            Name = name,
            Price = price,
            Quantity = quantity,
            Available = quantity
        };

    #region POST /api/admin/events/{eventId}/ticket-types/{ticketTypeId}/stock

    [Fact]
    public async Task AddTicketStock_ValidRequest_ReturnsOkWithUpdatedTicketType()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var updated = TicketType(ticketTypeId, quantity: 150);

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService
            .Setup(s => s.AddTicketStockAsync(eventId, ticketTypeId, 50))
            .ReturnsAsync(updated);

        // Act
        var result = await _controller.AddTicketStock(eventId, ticketTypeId, new AddTicketStockRequest(50));

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<TicketTypeWithAvailability>(okResult.Value);
        Assert.Equal(ticketTypeId, value.Id);
        Assert.Equal(150, value.Quantity);

        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.AddTicketStock &&
            c.Resource == AuditResourceType.Event &&
            c.ResourceId == eventId &&
            c.Details == $"Admin added 50 tickets to ticket type General (event {eventId})")), Times.Once);
    }

    [Fact]
    public async Task AddTicketStock_UnknownEventOrMismatchedTicketType_ReturnsNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService
            .Setup(s => s.AddTicketStockAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .ThrowsAsync(new KeyNotFoundException("Ticket type not found for event"));

        // Act
        var result = await _controller.AddTicketStock(Guid.NewGuid(), Guid.NewGuid(), new AddTicketStockRequest(50));

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public async Task AddTicketStock_InvalidQuantity_ReturnsBadRequest(int quantity)
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService
            .Setup(s => s.AddTicketStockAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .ThrowsAsync(new ArgumentException("Additional quantity must be greater than zero", "additionalQuantity"));

        // Act
        var result = await _controller.AddTicketStock(Guid.NewGuid(), Guid.NewGuid(), new AddTicketStockRequest(quantity));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task AddTicketStock_AuditDetailsTruncatedToColumnLimit()
    {
        // Arrange — a ticket type name long enough that the details string exceeds 1000 chars
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var longName = new string('X', 1200);
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService
            .Setup(s => s.AddTicketStockAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), 10))
            .ReturnsAsync(TicketType(Guid.NewGuid(), name: longName, quantity: 10));

        // Act
        var result = await _controller.AddTicketStock(Guid.NewGuid(), Guid.NewGuid(), new AddTicketStockRequest(10));

        // Assert — ATS-005: Details must stay within the varchar(1000) column limit
        Assert.IsType<OkObjectResult>(result);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.Details != null && c.Details.Length == 1000)), Times.Once);
    }

    [Fact]
    public async Task AddTicketStock_NoAuthenticatedUser_ReturnsUnauthorized()
    {
        // Act — no claims set
        var result = await _controller.AddTicketStock(Guid.NewGuid(), Guid.NewGuid(), new AddTicketStockRequest(50));

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    #endregion

    #region POST /api/admin/events/{eventId}/ticket-types

    [Fact]
    public async Task AddTicketType_ValidRequest_ReturnsCreatedWithNewTicketType()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var newType = TicketType(Guid.NewGuid(), name: "VIP", price: 150m, quantity: 20);

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService
            .Setup(s => s.AddTicketTypeAsync(eventId, "VIP", 150m, 20))
            .ReturnsAsync(newType);

        // Act
        var result = await _controller.AddTicketType(eventId, new AddTicketTypeRequest("VIP", 150m, 20));

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        var value = Assert.IsType<TicketTypeWithAvailability>(createdResult.Value);
        Assert.Equal("VIP", value.Name);
        Assert.Equal(150m, value.Price);
        Assert.Equal(20, value.Quantity);

        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.AddTicketType &&
            c.Resource == AuditResourceType.Event &&
            c.ResourceId == eventId &&
            c.Details == $"Admin created ticket type VIP (price 150, quantity 20) for event {eventId}")), Times.Once);
    }

    [Fact]
    public async Task AddTicketType_UnknownEvent_ReturnsNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService
            .Setup(s => s.AddTicketTypeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>()))
            .ThrowsAsync(new KeyNotFoundException("Event not found"));

        // Act
        var result = await _controller.AddTicketType(Guid.NewGuid(), new AddTicketTypeRequest("VIP", 150m, 20));

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Theory]
    [InlineData("", 150, 20)]
    [InlineData("VIP", -1, 20)]
    [InlineData("VIP", 150, 1001)]
    public async Task AddTicketType_InvalidPayload_ReturnsBadRequest(string name, decimal price, int quantity)
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService
            .Setup(s => s.AddTicketTypeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>()))
            .ThrowsAsync(new ArgumentException("Invalid payload", nameof(name)));

        // Act
        var result = await _controller.AddTicketType(Guid.NewGuid(), new AddTicketTypeRequest(name, price, quantity));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task AddTicketType_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockEventService
            .Setup(s => s.AddTicketTypeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.AddTicketType(Guid.NewGuid(), new AddTicketTypeRequest("VIP", 150m, 20));

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task AddTicketType_NoAuthenticatedUser_ReturnsUnauthorized()
    {
        // Act — no claims set
        var result = await _controller.AddTicketType(Guid.NewGuid(), new AddTicketTypeRequest("VIP", 150m, 20));

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    #endregion
}
