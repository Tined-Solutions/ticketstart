using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// RED tests for the past-event immutability guard at the EVENT SERVICE layer
/// (PEM-002/003, D-2/ADR-7): each of the 5 EventService mutating methods MUST
/// throw <see cref="EventFinalizedException"/> on a past-dated event BEFORE any
/// save/audit/notification side-effect, and future-dated events MUST still mutate.
/// InMemory DB + FakeTimeProvider frozen at T; events seeded at T-2d.
/// </summary>
public class EventServiceImmutabilityTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAmazonS3> _s3ClientMock;
    private readonly Mock<IEventNotificationQueue> _notificationQueueMock;
    private readonly FakeTimeProvider _clock;
    private readonly Guid _organizerId;

    public EventServiceImmutabilityTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            // AddTicketStock/AddTicketType open a transaction (FOR UPDATE txn);
            // the InMemory provider no-ops it with a warning (mirrors AdminPropertyTests).
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _s3ClientMock = new Mock<IAmazonS3>();
        _notificationQueueMock = new Mock<IEventNotificationQueue>();
        _clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _organizerId = Guid.NewGuid();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private EventService CreateService(HideExpiredEventsOptions? options = null) => new(
        _context,
        new TestLogger<EventService>(),
        BuildConfiguration(),
        _s3ClientMock.Object,
        _notificationQueueMock.Object,
        _clock,
        Options.Create(options ?? new HideExpiredEventsOptions { Enabled = true }));

    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "CloudflareR2:BucketName", "test-bucket" },
            { "CloudflareR2:PublicUrl", "https://test.r2.dev" }
        })
        .Build();

    private async Task<Event> SeedPastEvent(string name = "Past Event")
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Description",
            Date = _clock.GetUtcNow().UtcDateTime.AddDays(-2), // T-2d
            Location = "Venue",
            OrganizerId = _organizerId,
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
            UpdatedAt = _clock.GetUtcNow().UtcDateTime,
            Status = EventStatus.Approved
        };
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        return evt;
    }

    private async Task<(Event Event, TicketType TicketType)> SeedPastEventWithTicketType()
    {
        var evt = await SeedPastEvent();
        var tt = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            Name = "General",
            Price = 50m,
            Quantity = 100,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        _context.TicketTypes.Add(tt);
        await _context.SaveChangesAsync();
        return (evt, tt);
    }

    private static UpdateEventRequest ValidUpdateRequest() => new()
    {
        Name = "Updated Name",
        Description = "Updated",
        Date = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Location = "Updated Venue"
    };

    #region PEM-002/003 — UpdateEventAsync

    [Fact]
    public async Task UpdateEventAsync_PastEvent_ThrowsEventFinalized_NoSave_NoNotification()
    {
        // GIVEN a past event (Date = T-2d) owned by the caller, frozen clock at T
        var evt = await SeedPastEvent("Original Name");
        var service = CreateService();

        // WHEN an update is attempted with a valid future request
        // THEN it throws EventFinalizedException (guard before SaveChanges)
        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            service.UpdateEventAsync(evt.Id, ValidUpdateRequest(), _organizerId, UserRole.Organizador));

        // PEM-003: no row change…
        var persisted = await _context.Events.AsNoTracking().SingleAsync(e => e.Id == evt.Id);
        Assert.Equal("Original Name", persisted.Name);

        // …and no EDC-001 notification enqueue (guard fires before the date-change block)
        _notificationQueueMock.Verify(x => x.EnqueueAsync(It.IsAny<EventNotification>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEventAsync_PastEvent_FlagDisabled_StillThrows()
    {
        // PEM-004 / ADR-6: the immutability rule is HARD — flag OFF must not lift the guard.
        var evt = await SeedPastEvent();
        var service = CreateService(new HideExpiredEventsOptions { Enabled = false });

        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            service.UpdateEventAsync(evt.Id, ValidUpdateRequest(), _organizerId, UserRole.Organizador));
    }

    [Fact]
    public async Task UpdateEventAsync_FutureEvent_StillSucceeds()
    {
        // PEM-003 (future-still-side-effects): future events keep mutating as before.
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Future Event",
            Description = "D",
            Date = _clock.GetUtcNow().UtcDateTime.AddDays(10),
            Location = "V",
            OrganizerId = _organizerId,
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
            UpdatedAt = _clock.GetUtcNow().UtcDateTime,
            Status = EventStatus.Approved
        };
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        var service = CreateService();

        var result = await service.UpdateEventAsync(evt.Id, ValidUpdateRequest(), _organizerId, UserRole.Organizador);

        Assert.Equal("Updated Name", result.Name);
        var persisted = await _context.Events.AsNoTracking().SingleAsync(e => e.Id == evt.Id);
        Assert.Equal("Updated Name", persisted.Name);
    }

    #endregion

    #region PEM-002/003 — DeleteEventAsync

    [Fact]
    public async Task DeleteEventAsync_PastEvent_Organizer_ThrowsUnauthorizedAccessException_EventStillPresent()
    {
        // ED-001 precedence: the Admin-only delete guard runs BEFORE the finalized
        // guard — an organizer gets 403 (UnauthorizedAccessException) on a past
        // event, NEVER 409. (Inverted former owner-delete 409 behavior; the admin
        // variant below keeps the PEM-002 409 contract.)
        // GIVEN a past event owned by the caller
        var evt = await SeedPastEvent();
        var service = CreateService();

        // WHEN deletion is attempted by the organizer
        // THEN it throws UnauthorizedAccessException (403, not 409) and the row is NOT removed
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteEventAsync(evt.Id, _organizerId, UserRole.Organizador));

        Assert.True(await _context.Events.AnyAsync(e => e.Id == evt.Id));
    }

    [Fact]
    public async Task DeleteEventAsync_PastEvent_Admin_ThrowsEventFinalized_EventStillPresent()
    {
        // ED-002: Admin delete authority is unchanged — a past event still hits the
        // PEM-002 finalized guard and throws EventFinalizedException (409), row kept.
        var evt = await SeedPastEvent();
        var service = CreateService();

        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            service.DeleteEventAsync(evt.Id, Guid.NewGuid(), UserRole.Admin));

        Assert.True(await _context.Events.AnyAsync(e => e.Id == evt.Id));
    }

    #endregion

    #region PEM-002/003 — ReplaceEventImageAsync

    [Fact]
    public async Task ReplaceEventImageAsync_PastEvent_ThrowsEventFinalized_S3NeverCalled()
    {
        // GIVEN a past event owned by the caller
        var evt = await SeedPastEvent();
        var service = CreateService();

        // WHEN an image replacement is attempted
        // THEN it throws EventFinalizedException BEFORE any R2 upload
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            service.ReplaceEventImageAsync(evt.Id, _organizerId, UserRole.Organizador, stream, "img.jpg", "image/jpeg"));

        _s3ClientMock.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region PEM-002/003 — AddTicketStockAsync

    [Fact]
    public async Task AddTicketStockAsync_PastEvent_ThrowsEventFinalized_QuantityUnchanged()
    {
        // GIVEN a past event with an existing TicketType (Quantity=100)
        var (evt, tt) = await SeedPastEventWithTicketType();
        var service = CreateService();

        // WHEN an admin increments the stock
        // THEN it throws EventFinalizedException (guard inside the FOR UPDATE txn)
        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            service.AddTicketStockAsync(evt.Id, tt.Id, 50));

        var persisted = await _context.TicketTypes.AsNoTracking().SingleAsync(t => t.Id == tt.Id);
        Assert.Equal(100, persisted.Quantity);
    }

    #endregion

    #region PEM-002/003 — AddTicketTypeAsync

    [Fact]
    public async Task AddTicketTypeAsync_PastEvent_ThrowsEventFinalized_NoRowCreated()
    {
        // GIVEN a past event
        var evt = await SeedPastEvent();
        var service = CreateService();
        var before = await _context.TicketTypes.CountAsync();

        // WHEN an admin creates a new ticket type
        // THEN it throws EventFinalizedException and no row is inserted
        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            service.AddTicketTypeAsync(evt.Id, "VIP", 150m, 20));

        Assert.Equal(before, await _context.TicketTypes.CountAsync());
    }

    #endregion
}