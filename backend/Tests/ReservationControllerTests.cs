using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for ReservationController.
/// Validates: Requirements 4.1, 4.3, 16.2, 16.3
/// </summary>
public class ReservationControllerTests
{
    private readonly Mock<IReservationService> _mockReservationService;
    private readonly Mock<ILogger<ReservationController>> _mockLogger;
    private readonly ReservationController _controller;

    public ReservationControllerTests()
    {
        _mockReservationService = new Mock<IReservationService>();
        _mockLogger = new Mock<ILogger<ReservationController>>();
        _controller = new ReservationController(_mockReservationService.Object, _mockLogger.Object);

        // Setup default HttpContext for unauthenticated requests
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Setup default token generation for any reservation
        _mockReservationService
            .Setup(s => s.GenerateReservationToken(It.IsAny<Guid>()))
            .Returns((Guid id) => $"test-token-{id}");

        // ProblemDetailsFactory is required by ControllerBase.Problem(...) (ADR-5).
        // AddProblemDetails() alone does NOT register the MVC factory — register explicitly.
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<ApiBehaviorOptions>(_ => { });
        services.AddSingleton<ProblemDetailsFactory, DefaultProblemDetailsFactory>();
        _controller.ControllerContext.HttpContext.RequestServices = services.BuildServiceProvider();
    }

    #region CreateReservation Tests

    [Fact]
    public async Task CreateReservation_WithValidRequest_Returns201Created()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2,
            PurchaserDNI = "12345678"
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            TicketTypeId = request.TicketTypeId,
            Quantity = request.Quantity,
            PurchaserDNI = request.PurchaserDNI,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ReturnsAsync(reservation);

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal($"/api/reservations/{reservation.Id}", createdResult.Location);
        
        var response = Assert.IsType<ReservationResponse>(createdResult.Value);
        Assert.Equal(reservation.Id, response.Id);
        Assert.Equal(reservation.EventId, response.EventId);
        Assert.Equal(reservation.TicketTypeId, response.TicketTypeId);
        Assert.Equal(reservation.Quantity, response.Quantity);
        Assert.Equal(reservation.ExpiresAt, response.ExpiresAt);
        Assert.Equal("Active", response.Status);
    }

    [Fact]
    public async Task CreateReservation_ResponseIncludesReservationToken()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2,
            PurchaserDNI = "12345678"
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            TicketTypeId = request.TicketTypeId,
            Quantity = request.Quantity,
            PurchaserDNI = request.PurchaserDNI,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var expectedToken = "reservation-token-abc123";
        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ReturnsAsync(reservation);
        _mockReservationService
            .Setup(s => s.GenerateReservationToken(reservation.Id))
            .Returns(expectedToken);

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ReservationResponse>(createdResult.Value);
        Assert.Equal(expectedToken, response.Token);
        _mockReservationService.Verify(s => s.GenerateReservationToken(reservation.Id), Times.Once);
    }

    [Fact]
    public async Task CreateReservation_Returns400WhenDniOmitted()
    {
        // Arrange - request body without purchaserDNI defaults to empty string
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2
            // PurchaserDNI omitted → defaults to string.Empty
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ThrowsAsync(new ArgumentException("Purchaser DNI is required", nameof(request.PurchaserDNI)));

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    /// <summary>
    /// EHE-004 / ADR-5: an expired event must surface as 409 Conflict with
    /// RFC 7807 ProblemDetails (type "event-expired", title "Event has already started"),
    /// via the catch (EventExpiredException) placed ABOVE the generic catch.
    /// </summary>
    [Fact]
    public async Task CreateReservation_ExpiredEvent_Returns409ProblemDetails()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2,
            PurchaserDNI = "12345678"
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ThrowsAsync(new EventExpiredException());

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert — 409 with spec-compliant ProblemDetails
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(409, problem.Status);
        Assert.Equal("event-expired", problem.Type);
        Assert.Equal("Event has already started", problem.Title);
        Assert.Contains("no longer purchasable", problem.Detail);
    }

    [Fact]
    public async Task CreateReservation_WithAuthenticatedUser_PassesUserIdToService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 1,
            PurchaserDNI = "12345678"
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventId = request.EventId,
            TicketTypeId = request.TicketTypeId,
            Quantity = request.Quantity,
            PurchaserDNI = request.PurchaserDNI,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Setup authenticated user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(userId, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ReturnsAsync(reservation);

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        
        _mockReservationService.Verify(
            s => s.CreateReservationAsync(userId, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null),
            Times.Once);
    }

    [Fact]
    public async Task CreateReservation_WithNullRequest_Returns400BadRequest()
    {
        // Act
        var result = await _controller.CreateReservation(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithInvalidQuantity_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 0,
            PurchaserDNI = "12345678"
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ThrowsAsync(new ArgumentException("Quantity must be greater than zero", nameof(request.Quantity)));

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithInsufficientTickets_Returns409Conflict()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 10,
            PurchaserDNI = "12345678"
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ThrowsAsync(new ArgumentException("Insufficient tickets available. Requested: 10, Available: 5", nameof(request.Quantity)));

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithNonExistentEvent_Returns404NotFound()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2,
            PurchaserDNI = "12345678"
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ThrowsAsync(new KeyNotFoundException($"Ticket type {request.TicketTypeId} not found for event {request.EventId}"));

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithConcurrencyConflict_Returns409Conflict()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2,
            PurchaserDNI = "12345678"
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ThrowsAsync(new InvalidOperationException("Unable to create reservation due to concurrent updates. Please try again."));

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithUnexpectedError_Returns500InternalServerError()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2,
            PurchaserDNI = "12345678"
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, null))
            .ThrowsAsync(new Exception("Unexpected database error"));

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var serverErrorResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, serverErrorResult.StatusCode);
    }

    #endregion

    #region Batch 4: PurchaserEmail Tests

    [Fact]
    public async Task Batch4_CreateReservation_WithPurchaserEmail_PersistsAndReturns()
    {
        // RED: controller does not pass PurchaserEmail to service or include it in response yet
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 1,
            PurchaserDNI = "12345678",
            PurchaserEmail = "buyer@test.com",
            ConfirmEmail = "buyer@test.com"
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            TicketTypeId = request.TicketTypeId,
            Quantity = request.Quantity,
            PurchaserDNI = request.PurchaserDNI,
            PurchaserEmail = request.PurchaserEmail,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _mockReservationService
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI, request.PurchaserEmail))
            .ReturnsAsync(reservation);

        var result = await _controller.CreateReservation(request);

        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ReservationResponse>(createdResult.Value);
        Assert.Equal("buyer@test.com", response.PurchaserEmail);
    }

    [Fact]
    public async Task Batch4_CreateReservation_EmailMismatch_Returns400()
    {
        // RED: controller does not validate PurchaserEmail vs ConfirmEmail mismatch
        var request = new CreateReservationRequest
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 1,
            PurchaserDNI = "12345678",
            PurchaserEmail = "buyer@test.com",
            ConfirmEmail = "different@test.com"
        };

        var result = await _controller.CreateReservation(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    #endregion
}
