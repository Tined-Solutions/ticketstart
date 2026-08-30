using System.Net;
using System.Net.Http.Json;
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
/// Unit tests for MetricsController.
/// Validates: Requirements 11.7
/// </summary>
public class MetricsControllerTests
{
    private readonly Mock<IMetricsService> _mockMetricsService;
    private readonly Mock<ILogger<MetricsController>> _mockLogger;
    private readonly MetricsController _controller;

    public MetricsControllerTests()
    {
        _mockMetricsService = new Mock<IMetricsService>();
        _mockLogger = new Mock<ILogger<MetricsController>>();
        _controller = new MetricsController(_mockMetricsService.Object, _mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GET /api/metrics/events/{id}

    [Fact]
    public async Task GetEventMetrics_ServiceReturnsMetrics_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var metrics = new EventMetrics
        {
            Id = eventId,
            EventId = eventId,
            EventName = "Test Event",
            EventDate = DateTime.UtcNow.AddDays(30),
            TicketsSold = 42,
            TotalRevenue = 4200m,
            RemainingInventory = 58,
            TicketsScanned = 5
        };

        SetAuthenticatedUser(userId, UserRole.Organizador);
        _mockMetricsService.Setup(s => s.GetEventMetricsAsync(eventId)).ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetEventMetrics(eventId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<EventMetrics>(okResult.Value);
        Assert.Equal(eventId, value.EventId);
        Assert.Equal(metrics.EventName, value.EventName);
        Assert.Equal(metrics.TicketsSold, value.TicketsSold);
        Assert.Equal(metrics.TotalRevenue, value.TotalRevenue);
        Assert.Equal(metrics.RemainingInventory, value.RemainingInventory);
        Assert.Equal(metrics.TicketsScanned, value.TicketsScanned);
    }

    [Fact]
    public async Task GetEventMetrics_ServiceReturnsNull_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        SetAuthenticatedUser(userId, UserRole.Organizador);
        _mockMetricsService.Setup(s => s.GetEventMetricsAsync(eventId)).ReturnsAsync((EventMetrics?)null);

        // Act
        var result = await _controller.GetEventMetrics(eventId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task GetEventMetrics_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        // No authenticated user

        // Act
        var result = await _controller.GetEventMetrics(eventId);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    #endregion

    #region GET /api/metrics/organizer

    [Fact]
    public async Task GetOrganizerMetrics_EachItemCarriesStatus()
    {
        // EA-007: every EventMetrics item round-trips its Status so the dashboard
        // can render the moderation badge without extra queries.
        var userId = Guid.NewGuid();
        var metrics = new List<EventMetrics>
        {
            new EventMetrics
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventName = "Pending Event",
                EventDate = DateTime.UtcNow.AddDays(10),
                TicketsSold = 0,
                TotalRevenue = 0m,
                RemainingInventory = 100,
                TicketsScanned = 0,
                Status = EventStatus.Pending
            },
            new EventMetrics
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventName = "Approved Event",
                EventDate = DateTime.UtcNow.AddDays(20),
                TicketsSold = 5,
                TotalRevenue = 500m,
                RemainingInventory = 95,
                TicketsScanned = 1,
                Status = EventStatus.Approved
            },
            new EventMetrics
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventName = "Rejected Event",
                EventDate = DateTime.UtcNow.AddDays(30),
                TicketsSold = 0,
                TotalRevenue = 0m,
                RemainingInventory = 50,
                TicketsScanned = 0,
                Status = EventStatus.Rejected
            }
        };

        SetAuthenticatedUser(userId, UserRole.Organizador);
        _mockMetricsService.Setup(s => s.GetOrganizerMetricsAsync(userId)).ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetOrganizerMetrics();

        // Assert — controller passes the DTO through unchanged, status included
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsAssignableFrom<IEnumerable<EventMetrics>>(okResult.Value);
        var items = value.ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal(EventStatus.Pending, items[0].Status);
        Assert.Equal(EventStatus.Approved, items[1].Status);
        Assert.Equal(EventStatus.Rejected, items[2].Status);
    }

    [Fact]
    public async Task GetOrganizerMetrics_ReturnsOkWithMetrics()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var metrics = new List<EventMetrics>
        {
            new EventMetrics
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventName = "Event 1",
                EventDate = DateTime.UtcNow.AddDays(10),
                TicketsSold = 10,
                TotalRevenue = 1000m,
                RemainingInventory = 90,
                TicketsScanned = 2
            },
            new EventMetrics
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventName = "Event 2",
                EventDate = DateTime.UtcNow.AddDays(20),
                TicketsSold = 25,
                TotalRevenue = 2500m,
                RemainingInventory = 75,
                TicketsScanned = 8
            }
        };

        SetAuthenticatedUser(userId, UserRole.Organizador);
        _mockMetricsService.Setup(s => s.GetOrganizerMetricsAsync(userId)).ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetOrganizerMetrics();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsAssignableFrom<IEnumerable<EventMetrics>>(okResult.Value);
        Assert.Equal(2, value.Count());
    }

    [Fact]
    public async Task GetOrganizerMetrics_NoUserId_ReturnsUnauthorized()
    {
        // Arrange
        // No authenticated user

        // Act
        var result = await _controller.GetOrganizerMetrics();

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task GetOrganizerMetrics_AdminRole_ReturnsOk()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var metrics = new List<EventMetrics>
        {
            new EventMetrics
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventName = "Admin View Event",
                EventDate = DateTime.UtcNow.AddDays(15),
                TicketsSold = 5,
                TotalRevenue = 500m,
                RemainingInventory = 95,
                TicketsScanned = 1
            }
        };

        SetAuthenticatedUser(adminId, UserRole.Admin);
        _mockMetricsService.Setup(s => s.GetOrganizerMetricsAsync(adminId)).ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetOrganizerMetrics();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsAssignableFrom<IEnumerable<EventMetrics>>(okResult.Value);
        Assert.Single(value);
    }

    [Fact]
    public async Task GetEventMetrics_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        SetAuthenticatedUser(userId, UserRole.Organizador);
        _mockMetricsService.Setup(s => s.GetEventMetricsAsync(eventId)).ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.GetEventMetrics(eventId);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    #endregion

    private void SetAuthenticatedUser(Guid userId, UserRole role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }
}

/// <summary>
/// WAF keep-alive coverage for GET /api/metrics/events/{id} (role-access
/// EHE-006, remove-organizer-delete-metrics): the per-event metrics page was
/// removed from the frontend and the endpoint is now UI-less, so pipeline
/// coverage is the only bit-rot guard. The mocked unit tests above bypass the
/// real <c>EventOwnership</c> authorization policy — these do not.
/// </summary>
[Collection("EnvConfigTests")]
public class MetricsEndpointWafTests
{
    [Fact]
    public async Task GetEventMetrics_Owner_200()
    {
        // role-access per-event-metrics-owner-200: the owner keeps per-event
        // metrics over the real EventOwnership pipeline (backend unchanged).
        using var factory = new EventCatalogApiFactory();
        var organizerId = factory.SeedOrganizer();
        var eventId = factory.SeedEvent("Own Metrics Event", factory.Clock.GetUtcNow().UtcDateTime.AddDays(7), organizerId);
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);

        var response = await client.GetAsync($"/api/metrics/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventMetrics>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal("Own Metrics Event", body.EventName);
    }

    [Fact]
    public async Task GetEventMetrics_Admin_200()
    {
        // role-access per-event-metrics-admin-200: an Admin keeps per-event
        // metrics for any event (backend unchanged).
        using var factory = new EventCatalogApiFactory();
        var adminId = factory.SeedAdmin();
        var organizerId = factory.SeedOrganizer();
        var eventId = factory.SeedEvent("Admin Metrics Event", factory.Clock.GetUtcNow().UtcDateTime.AddDays(7), organizerId);
        var cookie = await factory.LoginAndGetCookieAsync(adminId);
        using var client = factory.CreateClientWithCookie(cookie);

        var response = await client.GetAsync($"/api/metrics/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventMetrics>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
    }
}
