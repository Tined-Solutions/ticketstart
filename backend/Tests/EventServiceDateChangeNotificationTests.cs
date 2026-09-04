using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Amazon.S3;
using Moq;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Tests for EventService date-change notification behavior.
/// Validates spec EDC-001 (date change detection), EDC-002 (buyer query),
/// EDC-004 (isolation — no email send from EventService), EDC-005 (zero buyers no-op),
/// EDC-006 (repeat notifications), EDC-007 (single extensible condition block).
/// </summary>
public class EventServiceDateChangeNotificationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEventNotificationQueue> _mockQueue;
    private readonly Mock<IR2StorageClient> _mockS3;
    private readonly TestLogger<EventService> _logger;
    private readonly IConfiguration _configuration;
    private readonly EventService _eventService;

    public EventServiceDateChangeNotificationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockQueue = new Mock<IEventNotificationQueue>();
        _mockS3 = new Mock<IR2StorageClient>();
        _logger = new TestLogger<EventService>();

        var configData = new Dictionary<string, string?>
        {
            { "CloudflareR2:BucketName", "test-bucket" },
            { "CloudflareR2:PublicUrl", "https://test.r2.dev" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _eventService = new EventService(_context, _logger, _configuration, _mockS3.Object, _mockQueue.Object, TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new HideExpiredEventsOptions()));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task UpdateEventAsync_DateChanged_EnqueuesPerBuyer()
    {
        // Arrange: event with date + two non-refunded buyers
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Rock Fest",
            Description = "Desc",
            Date = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            Location = "Venue",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        // Two buyers: alice@test.com, bob@test.com
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), EventId = eventEntity.Id,
            TicketTypeId = Guid.NewGuid(), PurchaserEmail = "alice@test.com",
            PurchaserDNI = "111", QRCodeData = "qr1", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), EventId = eventEntity.Id,
            TicketTypeId = Guid.NewGuid(), PurchaserEmail = "bob@test.com",
            PurchaserDNI = "222", QRCodeData = "qr2", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Rock Fest",
            Description = "Desc",
            Date = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), // changed!
            Location = "Venue"
        };

        // Act
        var result = await _eventService.UpdateEventAsync(
            eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        // Assert: EventService enqueued TWO notifications
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 11, 1), result.Date);

        _mockQueue.Verify(
            q => q.EnqueueAsync(It.Is<EventNotification>(n =>
                n.EventId == eventEntity.Id &&
                n.EventName == "Rock Fest" &&
                n.NotificationType == "DateChange" &&
                n.NewDate == new DateTime(2026, 11, 1) &&
                n.OldDate == new DateTime(2026, 10, 15))),
            Times.Exactly(2));

        // One for alice, one for bob
        _mockQueue.Verify(
            q => q.EnqueueAsync(It.Is<EventNotification>(n => n.RecipientEmail == "alice@test.com")),
            Times.Once);
        _mockQueue.Verify(
            q => q.EnqueueAsync(It.Is<EventNotification>(n => n.RecipientEmail == "bob@test.com")),
            Times.Once);
    }

    [Fact]
    public async Task UpdateEventAsync_SameDate_NoEnqueue()
    {
        // EDC-001 Scenario: Non-date edits are silent
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Rock Fest",
            Description = "Desc",
            Date = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            Location = "Venue",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), EventId = eventEntity.Id,
            TicketTypeId = Guid.NewGuid(), PurchaserEmail = "alice@test.com",
            PurchaserDNI = "111", QRCodeData = "qr1", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Rock Fest Renamed",
            Description = "Desc",
            Date = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc), // SAME date
            Location = "Venue"
        };

        await _eventService.UpdateEventAsync(
            eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        _mockQueue.Verify(
            q => q.EnqueueAsync(It.IsAny<EventNotification>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateEventAsync_RefundedBuyerExcluded()
    {
        // EDC-002 Scenario: Refunded buyers excluded
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Rock Fest",
            Description = "Desc",
            Date = new DateTime(2026, 10, 15),
            Location = "Venue",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        // One non-refunded, one refunded
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), EventId = eventEntity.Id,
            TicketTypeId = Guid.NewGuid(), PurchaserEmail = "alice@test.com",
            PurchaserDNI = "111", QRCodeData = "qr1", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), EventId = eventEntity.Id,
            TicketTypeId = Guid.NewGuid(), PurchaserEmail = "refunded@test.com",
            PurchaserDNI = "222", QRCodeData = "qr2", IsRefunded = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Rock Fest",
            Description = "Desc",
            Date = new DateTime(2026, 11, 1), // changed
            Location = "Venue"
        };

        await _eventService.UpdateEventAsync(
            eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        // Only one enqueue, and it's for alice
        _mockQueue.Verify(
            q => q.EnqueueAsync(It.IsAny<EventNotification>()),
            Times.Once);
        _mockQueue.Verify(
            q => q.EnqueueAsync(It.Is<EventNotification>(n => n.RecipientEmail == "alice@test.com")),
            Times.Once);
        _mockQueue.Verify(
            q => q.EnqueueAsync(It.Is<EventNotification>(n => n.RecipientEmail == "refunded@test.com")),
            Times.Never);
    }

    [Fact]
    public async Task UpdateEventAsync_ZeroBuyers_NoOp()
    {
        // EDC-005: Zero buyers = silent no-op
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Empty Event",
            Description = "Desc",
            Date = new DateTime(2026, 10, 15),
            Location = "Venue",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Empty Event",
            Description = "Desc",
            Date = new DateTime(2026, 11, 1), // changed
            Location = "Venue"
        };

        await _eventService.UpdateEventAsync(
            eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        _mockQueue.Verify(
            q => q.EnqueueAsync(It.IsAny<EventNotification>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateEventAsync_RepeatedNotify_PerChange()
    {
        // EDC-006: Every date change triggers a new notification
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Rock Fest",
            Description = "Desc",
            Date = new DateTime(2026, 10, 15),
            Location = "Venue",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), EventId = eventEntity.Id,
            TicketTypeId = Guid.NewGuid(), PurchaserEmail = "alice@test.com",
            PurchaserDNI = "111", QRCodeData = "qr1", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // First change: 10/15 → 11/01
        await _eventService.UpdateEventAsync(
            eventEntity.Id,
            new UpdateEventRequest { Name = "RF", Description = "D", Location = "V", Date = new DateTime(2026, 11, 1) },
            organizerId, UserRole.Organizador);

        // Second change: 11/01 → 10/15 (back)
        await _eventService.UpdateEventAsync(
            eventEntity.Id,
            new UpdateEventRequest { Name = "RF", Description = "D", Location = "V", Date = new DateTime(2026, 10, 15) },
            organizerId, UserRole.Organizador);

        // Two enqueue calls total (one per change)
        _mockQueue.Verify(
            q => q.EnqueueAsync(It.IsAny<EventNotification>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateEventAsync_DistinctBuyers_DeDupedByEmail()
    {
        // EDC-002 Scenario: Two tickets, same email → one notification
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Rock Fest",
            Description = "Desc",
            Date = new DateTime(2026, 10, 15),
            Location = "Venue",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        // Same buyer, two non-refunded tickets
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), EventId = eventEntity.Id,
            TicketTypeId = Guid.NewGuid(), PurchaserEmail = "alice@test.com",
            PurchaserDNI = "111", QRCodeData = "qr1", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), EventId = eventEntity.Id,
            TicketTypeId = Guid.NewGuid(), PurchaserEmail = "alice@test.com",
            PurchaserDNI = "111", QRCodeData = "qr2", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Rock Fest",
            Description = "Desc",
            Date = new DateTime(2026, 11, 1),
            Location = "Venue"
        };

        await _eventService.UpdateEventAsync(
            eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        // Only ONE notification despite two tickets
        _mockQueue.Verify(
            q => q.EnqueueAsync(It.IsAny<EventNotification>()),
            Times.Once);
        _mockQueue.Verify(
            q => q.EnqueueAsync(It.Is<EventNotification>(n => n.RecipientEmail == "alice@test.com")),
            Times.Once);
    }
}
