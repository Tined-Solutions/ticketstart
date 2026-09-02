using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using System.Security.Claims;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// RED tests for the admin purchase endpoints (APR-001/002/003/007/008).
/// Validates the class-level RequireAdminRole policy, 404/409 error mapping and the
/// RefundPurchase audit — with NO Mercado Pago call and NO email in the path.
/// </summary>
public class AdminControllerPurchaseTests
{
    private readonly Mock<IAdminPurchaseService> _mockPurchaseService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<ILogger<AdminController>> _mockLogger;
    private readonly AdminController _controller;

    public AdminControllerPurchaseTests()
    {
        _mockPurchaseService = new Mock<IAdminPurchaseService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockLogger = new Mock<ILogger<AdminController>>();
        _controller = new AdminController(
            new Mock<IAdminService>().Object,
            new Mock<IAuthService>().Object,
            _mockAuditLogService.Object,
            _mockLogger.Object,
            new Mock<IEventService>().Object,
            _mockPurchaseService.Object)
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

    #region APR-001: Admin-only authorization

    [Fact]
    public void AdminController_HasClassLevelRequireAdminRolePolicy()
    {
        // The class-level [Authorize(Policy = "RequireAdminRole")] covers BOTH purchase
        // endpoints (APR-001): a non-admin is rejected by the policy before any action runs.
        var attr = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>(true);
        Assert.NotNull(attr);
        Assert.Equal("RequireAdminRole", attr.Policy);

        // Neither purchase endpoint may override the policy with a weaker one.
        var getPurchases = typeof(AdminController).GetMethod(nameof(AdminController.GetPurchases))!;
        var refund = typeof(AdminController).GetMethod(nameof(AdminController.RefundPurchase))!;
        Assert.Null(getPurchases.GetCustomAttribute<AuthorizeAttribute>(true));
        Assert.Null(refund.GetCustomAttribute<AuthorizeAttribute>(true));
    }

    [Fact]
    public async Task GetPurchases_NoAuthenticatedUser_ReturnsUnauthorized()
    {
        // Act — no claims set
        var result = await _controller.GetPurchases(Guid.NewGuid());

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        _mockPurchaseService.Verify(s => s.GetPurchasesAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RefundPurchase_NoAuthenticatedUser_ReturnsUnauthorized()
    {
        // Act — no claims set
        var result = await _controller.RefundPurchase(Guid.NewGuid(), Guid.NewGuid(), new RefundPurchaseRequest(1, 100m));

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        _mockPurchaseService.Verify(s => s.RefundPurchaseAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<Guid>()), Times.Never);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    #endregion

    #region GET /api/admin/events/{eventId}/purchases — APR-002

    [Fact]
    public async Task GetPurchases_HappyPath_ReturnsListingWithTotalRefunded()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var response = new AdminPurchasesResponse(
            eventId,
            "Test Event",
            new List<AdminPurchaseRow>
            {
                new(Guid.NewGuid(), "juan.perez@gmail.com", "31234561", "General", 2, 200m, DateTime.UtcNow, 2, 200m, true, false)
            },
            200m);

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockPurchaseService.Setup(s => s.GetPurchasesAsync(eventId)).ReturnsAsync(response);

        // Act
        var result = await _controller.GetPurchases(eventId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<AdminPurchasesResponse>(okResult.Value);
        Assert.Equal(200m, value.TotalRefunded);
        Assert.Single(value.Purchases);
    }

    [Fact]
    public async Task GetPurchases_MissingEvent_ReturnsNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockPurchaseService
            .Setup(s => s.GetPurchasesAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new KeyNotFoundException("Event not found"));

        // Act
        var result = await _controller.GetPurchases(Guid.NewGuid());

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    #endregion

    #region POST /api/admin/events/{eventId}/purchases/{reservationId}/refund — APR-003/007/008

    [Fact]
    public async Task RefundPurchase_Success_PassesAmountBodyAndWritesAuditWithoutMotivo()
    {
        // Arrange (APR-003/007/008): body { quantity, amount } flows to the service
        // 4-arg call with the EXACT decimal amount; audit detail includes the quantity
        // AND the amount but no motivo/refund-reason field (APR-007/008).
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        // Act
        var result = await _controller.RefundPurchase(eventId, reservationId, new RefundPurchaseRequest(2, 200m));

        // Assert — 200 + the service received (reservationId, 2, 200m, adminId)
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        _mockPurchaseService.Verify(
            s => s.RefundPurchaseAsync(reservationId, 2, 200m, adminId), Times.Once);

        // Assert — exactly one audit entry: RefundPurchase/Payment, reservation id,
        // details WITH the quantity and the amount and WITHOUT any motivo/refund-reason
        // (APR-007/008)
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.Is<AuditLogContext>(c =>
            c.UserId == adminId &&
            c.Action == AuditActionType.RefundPurchase &&
            c.Resource == AuditResourceType.Payment &&
            c.ResourceId == reservationId &&
            c.Details != null &&
            c.Details.Contains("2 tickets", StringComparison.OrdinalIgnoreCase) &&
            c.Details.Contains("amount", StringComparison.OrdinalIgnoreCase) &&
            c.Details.Contains("200", StringComparison.OrdinalIgnoreCase) &&
            !c.Details.Contains("motivo", StringComparison.OrdinalIgnoreCase) &&
            !c.Details.Contains("reason", StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    [Fact]
    public async Task RefundPurchase_InvalidQuantity_ReturnsConflict()
    {
        // Arrange — APR-003: K ≤ 0 or K > active → InvalidOperationException → 409,
        // no audit written on failure.
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockPurchaseService
            .Setup(s => s.RefundPurchaseAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Cannot refund 0 tickets; 2 active remaining"));

        // Act
        var result = await _controller.RefundPurchase(Guid.NewGuid(), Guid.NewGuid(), new RefundPurchaseRequest(0, 0m));

        // Assert — 409 Conflict, no audit written on failure
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task RefundPurchase_UsedTicket_ReturnsConflict()
    {
        // Arrange — APR-004: used ticket → InvalidOperationException → 409
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockPurchaseService
            .Setup(s => s.RefundPurchaseAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Cannot refund a purchase with used tickets"));

        // Act
        var result = await _controller.RefundPurchase(Guid.NewGuid(), Guid.NewGuid(), new RefundPurchaseRequest(1, 100m));

        // Assert — 409 Conflict, no audit written on failure
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task RefundPurchase_MissingReservation_ReturnsNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockPurchaseService
            .Setup(s => s.RefundPurchaseAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<Guid>()))
            .ThrowsAsync(new KeyNotFoundException("Reservation not found"));

        // Act
        var result = await _controller.RefundPurchase(Guid.NewGuid(), Guid.NewGuid(), new RefundPurchaseRequest(1, 100m));

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    [Fact]
    public async Task RefundPurchase_Success_DoesNotTouchPaymentServiceOrEmail()
    {
        // Arrange — APR-008: the controller path depends only on IAdminPurchaseService +
        // IAuditLogService; there is no MP/email dependency in the action.
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);

        // Act
        var result = await _controller.RefundPurchase(Guid.NewGuid(), Guid.NewGuid(), new RefundPurchaseRequest(1, 100m));

        // Assert — success surfaces and the service was called exactly once
        Assert.IsType<OkObjectResult>(result);
        _mockPurchaseService.Verify(s => s.RefundPurchaseAsync(It.IsAny<Guid>(), 1, It.IsAny<decimal>(), adminId), Times.Once);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Once);
    }

    [Fact]
    public async Task RefundPurchase_ServiceThrows_ReturnsInternalServerError()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockPurchaseService
            .Setup(s => s.RefundPurchaseAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.RefundPurchase(Guid.NewGuid(), Guid.NewGuid(), new RefundPurchaseRequest(1, 100m));

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        _mockAuditLogService.Verify(s => s.LogActionAsync(It.IsAny<AuditLogContext>()), Times.Never);
    }

    #endregion
}
