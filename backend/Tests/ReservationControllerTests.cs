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
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
            .ReturnsAsync(reservation);

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal(nameof(ReservationController.GetReservation), createdResult.ActionName);
        
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
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
            .ReturnsAsync(reservation);
        _mockReservationService
            .Setup(s => s.GenerateReservationToken(reservation.Id))
            .Returns(expectedToken);

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
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
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
            .ThrowsAsync(new ArgumentException("Purchaser DNI is required", nameof(request.PurchaserDNI)));

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
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
            .Setup(s => s.CreateReservationAsync(userId, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
            .ReturnsAsync(reservation);

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        
        _mockReservationService.Verify(
            s => s.CreateReservationAsync(userId, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI),
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
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
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
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
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
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
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
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
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
            .Setup(s => s.CreateReservationAsync(null, request.EventId, request.TicketTypeId, request.Quantity, request.PurchaserDNI))
            .ThrowsAsync(new Exception("Unexpected database error"));

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        var serverErrorResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, serverErrorResult.StatusCode);
    }

    #endregion

    #region GetReservation Tests

    [Fact]
    public async Task GetReservation_WithValidId_Returns200Ok()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow,
            Event = new Event
            {
                Id = Guid.NewGuid(),
                Name = "Test Event",
                Description = "Test Description",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Test Location",
                ImageUrl = "https://example.com/image.jpg"
            },
            TicketType = new TicketType
            {
                Id = Guid.NewGuid(),
                Name = "General Admission",
                Price = 50.00m,
                Quantity = 100
            }
        };

        _mockReservationService
            .Setup(s => s.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        // Act
        var result = await _controller.GetReservation(reservationId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        
        var response = Assert.IsType<ReservationResponse>(okResult.Value);
        Assert.Equal(reservation.Id, response.Id);
        Assert.Equal(reservation.EventId, response.EventId);
        Assert.Equal(reservation.TicketTypeId, response.TicketTypeId);
        Assert.Equal(reservation.Quantity, response.Quantity);
        Assert.Equal("Active", response.Status);
        
        Assert.NotNull(response.Event);
        Assert.Equal(reservation.Event.Name, response.Event.Name);
        
        Assert.NotNull(response.TicketType);
        Assert.Equal(reservation.TicketType.Name, response.TicketType.Name);
    }

    [Fact]
    public async Task GetReservation_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var reservationId = Guid.NewGuid();

        _mockReservationService
            .Setup(s => s.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        // Act
        var result = await _controller.GetReservation(reservationId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task GetReservation_WithExpiredReservation_Returns200WithExpiredStatus()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = reservationId,
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 2,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            Status = ReservationStatus.Expired,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15),
            Event = new Event
            {
                Id = Guid.NewGuid(),
                Name = "Test Event",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Test Location"
            },
            TicketType = new TicketType
            {
                Id = Guid.NewGuid(),
                Name = "General Admission",
                Price = 50.00m,
                Quantity = 100
            }
        };

        _mockReservationService
            .Setup(s => s.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        // Act
        var result = await _controller.GetReservation(reservationId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReservationResponse>(okResult.Value);
        Assert.Equal("Expired", response.Status);
    }

    [Fact]
    public async Task GetReservation_WithUnexpectedError_Returns500InternalServerError()
    {
        // Arrange
        var reservationId = Guid.NewGuid();

        _mockReservationService
            .Setup(s => s.GetReservationByIdAsync(reservationId))
            .ThrowsAsync(new Exception("Unexpected database error"));

        // Act
        var result = await _controller.GetReservation(reservationId);

        // Assert
        var serverErrorResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, serverErrorResult.StatusCode);
    }

    #endregion
}
