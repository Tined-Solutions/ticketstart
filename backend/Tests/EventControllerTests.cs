using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Security.Claims;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for EventController.
/// Validates admin modify/delete audit logging and audit failure handling.
/// </summary>
[Collection("EnvConfigTests")]
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
        _mockEventService.Setup(s => s.GetEventByIdAsync(eventId, true)).ReturnsAsync(eventDetails);

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
        _mockEventService.Setup(s => s.GetEventByIdAsync(eventId, true)).ReturnsAsync(eventDetails);
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
        _mockEventService.Setup(s => s.GetEventByIdAsync(eventId, true)).ReturnsAsync(new EventWithAvailability { Id = eventId });

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

    #region EHE-002/003/006/007 — HTTP-level catalog filter + /manage endpoints (WAF)

    [Fact]
    public async Task GetEventById_Active_200()
    {
        // EHE-003: active (future-dated) event returns 200 on the public detail.
        using var factory = new EventCatalogApiFactory();
        var client = factory.CreateClient();
        var organizerId = factory.SeedOrganizer();
        var eventId = factory.SeedEvent("Active Event", factory.Clock.GetUtcNow().UtcDateTime.AddDays(1), organizerId);

        // Act
        var response = await client.GetAsync($"/api/events/{eventId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventWithAvailability>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.Id);
    }

    [Fact]
    public async Task GetEventById_SameDayAfterStart_404()
    {
        // EHE-003: event starts 14:00, clock is 23:00 the same day → expired →
        // public detail returns 404. (FakeTimeProvider cannot move backwards, so
        // both instants live on 2030-01-01.)
        using var factory = new EventCatalogApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2030, 1, 1, 23, 0, 0, TimeSpan.Zero));
        var client = factory.CreateClient();
        var organizerId = factory.SeedOrganizer();
        var eventId = factory.SeedEvent("Same Day", new DateTime(2030, 1, 1, 14, 0, 0, DateTimeKind.Utc), organizerId);

        // Act
        var response = await client.GetAsync($"/api/events/{eventId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_PendingFuture_404()
    {
        // EHE-003: a future-dated Pending event returns 404 on the public detail.
        using var factory = new EventCatalogApiFactory();
        var client = factory.CreateClient();
        var organizerId = factory.SeedOrganizer();
        var eventId = factory.SeedEvent("Pending Event", factory.Clock.GetUtcNow().UtcDateTime.AddDays(1), organizerId, EventStatus.Pending);

        // Act
        var response = await client.GetAsync($"/api/events/{eventId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_RejectedFuture_404()
    {
        // EHE-003: a future-dated Rejected event returns 404 on the public detail.
        using var factory = new EventCatalogApiFactory();
        var client = factory.CreateClient();
        var organizerId = factory.SeedOrganizer();
        var eventId = factory.SeedEvent("Rejected Event", factory.Clock.GetUtcNow().UtcDateTime.AddDays(1), organizerId, EventStatus.Rejected);

        // Act
        var response = await client.GetAsync($"/api/events/{eventId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_ApprovedFuture_200_WithStatus()
    {
        // EHE-003/EA-007: an Approved future event returns 200 with Status surfaced.
        using var factory = new EventCatalogApiFactory();
        var client = factory.CreateClient();
        var organizerId = factory.SeedOrganizer();
        var eventId = factory.SeedEvent("Approved Event", factory.Clock.GetUtcNow().UtcDateTime.AddDays(1), organizerId, EventStatus.Approved);

        // Act
        var response = await client.GetAsync($"/api/events/{eventId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventWithAvailability>();
        Assert.NotNull(body);
        Assert.Equal(EventStatus.Approved, body.Status);
    }

    [Fact]
    public async Task GetEventById_ManagementIncludeExpired_200()
    {
        // EHE-003: the role-gated management variant returns the expired event.
        using var factory = new EventCatalogApiFactory();
        var organizerId = factory.SeedOrganizer();
        var expiredId = factory.SeedEvent("Past Event", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), organizerId);
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);

        // Act
        var response = await client.GetAsync($"/api/events/{expiredId}/manage");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventWithAvailability>();
        Assert.NotNull(body);
        Assert.Equal(expiredId, body.Id);
        Assert.Equal("Past Event", body.Name);
    }

    [Fact]
    public async Task Organizer_ManagementEvent_Expired_200()
    {
        // EHE-006: organizer opens their own past event via the management variant
        // and gets the full (unfiltered) detail.
        using var factory = new EventCatalogApiFactory();
        var organizerId = factory.SeedOrganizer();
        var expiredId = factory.SeedEvent("Expired Own Event", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-3), organizerId);
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);

        // Act
        var response = await client.GetAsync($"/api/events/{expiredId}/manage");

        // Assert — 200 with the past event's data (Date clearly in the past)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventWithAvailability>();
        Assert.NotNull(body);
        Assert.Equal(expiredId, body.Id);
        Assert.True(body.Date < factory.Clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Staff_ManagementList_IncludesScannableOnly()
    {
        // Staff scan chooser (GET /api/events/manage): only scannable events are
        // listed — future events plus events ended within the QR validation window
        // (24h). Events ended beyond that window cannot validate QR codes anymore.
        using var factory = new EventCatalogApiFactory();
        var staffId = factory.SeedStaff();
        var futureId = factory.SeedEvent("Future", factory.Clock.GetUtcNow().UtcDateTime.AddDays(1), factory.SeedOrganizer());
        var recentId = factory.SeedEvent("Recent", factory.Clock.GetUtcNow().UtcDateTime.AddHours(-2), factory.SeedOrganizer());
        var oldId = factory.SeedEvent("Old", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), factory.SeedOrganizer());
        var cookie = await factory.LoginAndGetCookieAsync(staffId);
        using var client = factory.CreateClientWithCookie(cookie);

        // Act
        var response = await client.GetAsync("/api/events/manage");

        // Assert — future + recently-ended appear; the old event is filtered out
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<EventWithAvailability>>();
        Assert.NotNull(body);
        Assert.Contains(body, e => e.Id == futureId);
        Assert.Contains(body, e => e.Id == recentId);
        Assert.DoesNotContain(body, e => e.Id == oldId);

        // Ordering: future events first, recently-ended events after them.
        var futureIndex = body.FindIndex(e => e.Id == futureId);
        var recentIndex = body.FindIndex(e => e.Id == recentId);
        Assert.True(futureIndex < recentIndex, "Future event must come before the recently-ended event");
    }

    [Fact]
    public async Task Staff_ManagementList_Anon_401()
    {
        // EHE-007: anonymous caller → 401 (route matched, RequireStaffRole denies).
        using var factory = new EventCatalogApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/events/manage");

        // Assert — 401, NOT 404 (proves the /manage route exists and is auth-gated)
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Events_ManageRoute_NonStaffOrganizer_403()
    {
        // Route test: GET /api/events/manage must NOT be swallowed by
        // [HttpGet("{id:guid}")] — the GUID constraint rejects the "manage"
        // literal. An authenticated organizer (not Staff/Admin) gets 403 from the
        // RequireStaffRole policy, proving the route resolved to the list action.
        using var factory = new EventCatalogApiFactory();
        var organizerId = factory.SeedOrganizer();
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);

        // Act
        var response = await client.GetAsync("/api/events/manage");

        // Assert — 403 (route matched + policy enforced), not 404 (route miss)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region PEM-002 — Past-event mutation endpoints → 409 event-finalized (WAF)

    private static async Task<ProblemDetails?> ReadProblemDetailsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<ProblemDetails>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    [Fact]
    public async Task UpdateEvent_PastEvent_409_EventFinalized_NoSave()
    {
        // PEM-002/003: PUT on a past event → 409 event-finalized, no row change.
        using var factory = new EventCatalogApiFactory();
        var organizerId = factory.SeedOrganizer();
        var pastId = factory.SeedEvent("Past To Update", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), organizerId);
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        // PEM-005: consultation on the same past event stays 200 (carve-out)
        var manage = await client.GetAsync($"/api/events/{pastId}/manage");
        Assert.Equal(HttpStatusCode.OK, manage.StatusCode);

        // Act — PUT on the past event
        var response = await client.PutAsJsonAsync($"/api/events/{pastId}", new
        {
            name = "Mutated Name",
            description = "x",
            date = factory.Clock.GetUtcNow().UtcDateTime.AddDays(5),
            location = "Y"
        });

        // Assert — 409 RFC 7807 with type "event-finalized"
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.NotNull(problem);
        Assert.Equal("event-finalized", problem!.Type);
        Assert.Equal("Event has already finished", problem.Title);
        Assert.Equal(409, problem.Status);

        // PEM-003: no save — the row still carries its original name
        var after = await client.GetAsync($"/api/events/{pastId}/manage");
        var body = await after.Content.ReadFromJsonAsync<EventWithAvailability>();
        Assert.Equal("Past To Update", body!.Name);
    }

    [Fact]
    public async Task DeleteEvent_PastEvent_409_EventFinalized()
    {
        // PEM-002: DELETE on a past event → 409; the row survives.
        using var factory = new EventCatalogApiFactory();
        var organizerId = factory.SeedOrganizer();
        var pastId = factory.SeedEvent("Past To Delete", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), organizerId);
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        var response = await client.DeleteAsync($"/api/events/{pastId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal("event-finalized", problem!.Type);

        var after = await client.GetAsync($"/api/events/{pastId}/manage");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task UploadEventImage_PastEvent_409_EventFinalized()
    {
        // PEM-002: POST image on a past event → 409 (before any R2 upload).
        using var factory = new EventCatalogApiFactory();
        var organizerId = factory.SeedOrganizer();
        var pastId = factory.SeedEvent("Past With Image", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), organizerId);
        var cookie = await factory.LoginAndGetCookieAsync(organizerId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "image", "img.jpg");

        var response = await client.PostAsync($"/api/events/{pastId}/image", content);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal("event-finalized", problem!.Type);
    }

    [Fact]
    public async Task AddTicketStock_PastEvent_409_EventFinalized()
    {
        // PEM-002: POST stock on a past event → 409.
        using var factory = new EventCatalogApiFactory();
        var adminId = factory.SeedAdmin();
        var organizerId = factory.SeedOrganizer();
        var pastId = factory.SeedEvent("Past Stock", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), organizerId);
        var ttId = factory.SeedTicketType(pastId, 100);
        var cookie = await factory.LoginAndGetCookieAsync(adminId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        var response = await client.PostAsJsonAsync($"/api/admin/events/{pastId}/ticket-types/{ttId}/stock", new { additionalQuantity = 50 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal("event-finalized", problem!.Type);
    }

    [Fact]
    public async Task AddTicketType_PastEvent_409_EventFinalized()
    {
        // PEM-002: POST new ticket type on a past event → 409.
        using var factory = new EventCatalogApiFactory();
        var adminId = factory.SeedAdmin();
        var organizerId = factory.SeedOrganizer();
        var pastId = factory.SeedEvent("Past Type", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), organizerId);
        var cookie = await factory.LoginAndGetCookieAsync(adminId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        var response = await client.PostAsJsonAsync($"/api/admin/events/{pastId}/ticket-types", new { name = "VIP", price = 150m, quantity = 20 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal("event-finalized", problem!.Type);
    }

    [Fact]
    public async Task ApproveEvent_PastEvent_409_EventFinalized()
    {
        // PEM-002/EA-003: POST approve on a past event → 409; GET manage stays 200.
        using var factory = new EventCatalogApiFactory();
        var adminId = factory.SeedAdmin();
        var organizerId = factory.SeedOrganizer();
        var pastId = factory.SeedEvent("Past Approve", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), organizerId, EventStatus.Pending);
        var cookie = await factory.LoginAndGetCookieAsync(adminId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        var response = await client.PostAsync($"/api/admin/events/{pastId}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal("event-finalized", problem!.Type);

        // Consultation carve-out with an admin cookie on the same past event
        var manage = await client.GetAsync($"/api/events/{pastId}/manage");
        Assert.Equal(HttpStatusCode.OK, manage.StatusCode);
    }

    [Fact]
    public async Task RejectEvent_PastEvent_409_EventFinalized()
    {
        // PEM-002/EA-004: POST reject on a past event → 409.
        using var factory = new EventCatalogApiFactory();
        var adminId = factory.SeedAdmin();
        var organizerId = factory.SeedOrganizer();
        var pastId = factory.SeedEvent("Past Reject", factory.Clock.GetUtcNow().UtcDateTime.AddDays(-2), organizerId, EventStatus.Pending);
        var cookie = await factory.LoginAndGetCookieAsync(adminId);
        using var client = factory.CreateClientWithCookie(cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");

        var response = await client.PostAsJsonAsync($"/api/admin/events/{pastId}/reject", new { reason = "too late" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal("event-finalized", problem!.Type);
    }

    #endregion
}

/// <summary>
/// WAF host for deterministic HTTP-level event catalog tests: in-memory database,
/// frozen FakeTimeProvider clock (2030-01-01 UTC), real auth via /api/auth/login.
/// Mutates process-global env vars → serialized via the EnvConfigTests collection.
/// </summary>
public class EventCatalogApiFactory : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new();

    private readonly string _dbName = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, string?> _originalValues = new();
    private readonly Dictionary<Guid, (string Email, string Password)> _users = new();

    public EventCatalogApiFactory()
    {
        Clock.SetUtcNow(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var backendRoot = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "..", "..", ".."));
        builder.UseContentRoot(backendRoot);
        builder.UseEnvironment("Development");

        SetConfigEnvVar("Resend__ApiKey", "test-resend-api-key");
        SetConfigEnvVar("Resend__FromEmail", "tickets@example.com");
        SetConfigEnvVar("CloudflareR2__AccessKey", "test-r2-access-key");
        SetConfigEnvVar("CloudflareR2__SecretKey", "test-r2-secret-key");
        SetConfigEnvVar("CloudflareR2__ServiceUrl", "https://test-account.r2.cloudflarestorage.com");
        SetConfigEnvVar("Jwt__SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789");

        builder.ConfigureServices(services =>
        {
            // Background services try to connect to the real database; remove them.
            var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
            foreach (var hosted in hostedServices)
            {
                services.Remove(hosted);
            }

            // Replace the Npgsql DbContext with an isolated InMemory database.
            // RemoveAll covers every descriptor AddDbContext registered (context,
            // options, and the IDbContextOptionsConfiguration carrying the Npgsql
            // action) — otherwise EF sees two database providers and throws.
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options
                .UseInMemoryDatabase(_dbName)
                // Stock/ticket-type endpoints open a FOR UPDATE transaction; the
                // InMemory provider no-ops it and promotes TransactionIgnoredWarning
                // to an exception unless ignored (mirrors AdminPropertyTests).
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

            // Replace TimeProvider.System with the frozen fake clock (ADR-3).
            var tpDescriptor = services.Single(d => d.ServiceType == typeof(TimeProvider));
            services.Remove(tpDescriptor);
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    private void SetConfigEnvVar(string name, string value)
    {
        _originalValues[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var (name, original) in _originalValues)
        {
            Environment.SetEnvironmentVariable(name, original);
        }

        base.Dispose(disposing);
    }

    public Guid SeedOrganizer() => SeedUser(UserRole.Organizador);

    public Guid SeedStaff() => SeedUser(UserRole.Staff);

    public Guid SeedAdmin() => SeedUser(UserRole.Admin);

    private Guid SeedUser(UserRole role)
    {
        var email = $"seed-{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";
        using var scope = Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var result = authService.CreateUserAsync($"Seed {role}", email, "password123", role).GetAwaiter().GetResult();
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to seed {role} user: {result.Error}");
        }

        _users[result.UserId] = (email, "password123");
        return result.UserId;
    }

    public Guid SeedEvent(string name, DateTime date, Guid organizerId, EventStatus status = EventStatus.Approved)
    {
        var id = Guid.NewGuid();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Events.Add(new Event
        {
            Id = id,
            Name = name,
            Description = "Description",
            Date = date,
            Location = "Venue",
            OrganizerId = organizerId,
            CreatedAt = date,
            UpdatedAt = date,
            Status = status
        });
        db.SaveChanges();
        return id;
    }

    public Guid SeedTicketType(Guid eventId, int quantity = 100)
    {
        var id = Guid.NewGuid();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.TicketTypes.Add(new TicketType
        {
            Id = id,
            EventId = eventId,
            Name = "General",
            Price = 50m,
            Quantity = quantity,
            CreatedAt = Clock.GetUtcNow().UtcDateTime
        });
        db.SaveChanges();
        return id;
    }

    public async Task<string> LoginAndGetCookieAsync(Guid userId)
    {
        var (email, password) = _users[userId];
        using var loginClient = CreateClient();
        var response = await loginClient.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var setCookieHeaders = response.Headers.GetValues("Set-Cookie");
        var tokenCookie = setCookieHeaders.First(c => c.StartsWith("token=", StringComparison.OrdinalIgnoreCase));
        return tokenCookie.Split(';')[0]; // "token=value" — added manually as the Cookie header
    }

    public HttpClient CreateClientWithCookie(string cookie)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }
}
