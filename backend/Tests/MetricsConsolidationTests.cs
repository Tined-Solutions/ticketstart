using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// B7.2 RED + B7.3 GREEN: MetricsService consolidation tests.
/// Validates that GetOrganizerMetricsAsync returns correct metrics and
/// uses a consolidated query approach (not per-event N+1 loops).
/// </summary>
public class MetricsConsolidationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MetricsService> _logger;

    public MetricsConsolidationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<MetricsService>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    /// <summary>
    /// B7.2 RED: Verifies GetOrganizerMetricsAsync correctly aggregates metrics
    /// for multiple events owned by the organizer.
    /// The consolidated GroupBy implementation (B7.3) must produce identical results.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_ReturnsCorrectAggregatesForAllEvents()
    {
        // Arrange: seed organizer with 3 events, each with tickets and reservations
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "org@example.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var event1 = CreateEvent(organizerId, "Event 1");
        var event2 = CreateEvent(organizerId, "Event 2");
        var event3 = CreateEvent(organizerId, "Event 3");

        _context.Events.AddRange(event1, event2, event3);

        foreach (var evt in new[] { event1, event2, event3 })
        {
            var tt = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = evt.Id,
                Name = "General",
                Price = 50,
                Quantity = 100,
                CreatedAt = DateTime.UtcNow
            };
            _context.TicketTypes.Add(tt);

            // Seed 5 tickets per event, 2 used per event
            for (int i = 0; i < 5; i++)
            {
                _context.Tickets.Add(new Ticket
                {
                    Id = Guid.NewGuid(),
                    EventId = evt.Id,
                    TicketTypeId = tt.Id,
                    PurchaserEmail = $"buyer{i}@test.com",
                    PurchaserDNI = $"DNI{i}",
                    QRCodeData = $"QR-{Guid.NewGuid()}",
                    IsUsed = i < 2,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Seed 2 active reservations per event, quantity 1 each
            for (int i = 0; i < 2; i++)
            {
                _context.Reservations.Add(new Reservation
                {
                    Id = Guid.NewGuid(),
                    UserId = organizerId,
                    EventId = evt.Id,
                    TicketTypeId = tt.Id,
                    Quantity = 1,
                    PurchaserDNI = $"RES{i}",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Status = ReservationStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        // Act
        var metricsService = new MetricsService(_context, _logger);
        var metrics = (await metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        // Assert: 3 events returned, each with correct metrics
        Assert.Equal(3, metrics.Count);

        foreach (var metric in metrics)
        {
            Assert.Equal(5, metric.TicketsSold);
            Assert.Equal(250, metric.TotalRevenue); // 5 tickets × 50 price
            Assert.Equal(2, metric.TicketsScanned);
            // 100 total inventory - 5 sold - 2 active reservations = 93 remaining
            Assert.Equal(93, metric.RemainingInventory);
        }

        // Each event has a distinct ID
        var eventIds = metrics.Select(m => m.EventId).Distinct().ToList();
        Assert.Equal(3, eventIds.Count);
    }

    /// <summary>
    /// B7.2: Verifies organizer with no events returns empty.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_NoEvents_ReturnsEmpty()
    {
        var organizerId = Guid.NewGuid();

        var metricsService = new MetricsService(_context, _logger);
        var metrics = await metricsService.GetOrganizerMetricsAsync(organizerId);

        Assert.NotNull(metrics);
        Assert.Empty(metrics);
    }

    /// <summary>
    /// B7.2: Verifies only organizer's events are returned, not other organizers' events.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_ExcludesOtherOrganizersEvents()
    {
        var organizerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = organizerId, Email = "org@test.com", PasswordHash = "hash", Role = UserRole.Organizador, CreatedAt = DateTime.UtcNow },
            new User { Id = otherOrganizerId, Email = "other@test.com", PasswordHash = "hash", Role = UserRole.Organizador, CreatedAt = DateTime.UtcNow }
        );

        var myEvent = CreateEvent(organizerId, "My Event");
        var otherEvent = CreateEvent(otherOrganizerId, "Other Event");
        _context.Events.AddRange(myEvent, otherEvent);

        var tt = new TicketType { Id = Guid.NewGuid(), EventId = myEvent.Id, Name = "General", Price = 10, Quantity = 10, CreatedAt = DateTime.UtcNow };
        _context.TicketTypes.Add(tt);
        await _context.SaveChangesAsync();

        var metricsService = new MetricsService(_context, _logger);
        var metrics = (await metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        Assert.Single(metrics);
        Assert.Equal(myEvent.Id, metrics[0].EventId);
    }

    private static Event CreateEvent(Guid organizerId, string name)
    {
        var now = DateTime.UtcNow;
        return new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            Date = now.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// APR-005: refunded tickets must not count toward TicketsSold / TotalRevenue in the
    /// consolidated GetOrganizerMetricsAsync path (pre-GroupBy exclusion).
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_RefundedTickets_ExcludedFromSoldAndRevenue()
    {
        // Arrange — event with 5 tickets: 2 used, 1 refunded
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "org-refund@example.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Refunded Event");
        _context.Events.Add(eventEntity);

        var tt = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 50,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(tt);

        for (var i = 0; i < 5; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = tt.Id,
                PurchaserEmail = $"buyer{i}@example.com",
                PurchaserDNI = $"DNI{i}",
                QRCodeData = $"QR-{Guid.NewGuid()}",
                IsUsed = i < 2,
                IsRefunded = i == 4,
                RefundedAt = i == 4 ? DateTime.UtcNow.AddDays(-1) : null,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var metricsService = new MetricsService(_context, _logger);
        var metrics = (await metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        // Assert — sold = 4, revenue = 4 × 50 = 200, scanned = 2 (unchanged)
        var metric = Assert.Single(metrics);
        Assert.Equal(4, metric.TicketsSold);
        Assert.Equal(200m, metric.TotalRevenue);
        Assert.Equal(2, metric.TicketsScanned);
        Assert.Equal(100 - 4 - 0, metric.RemainingInventory); // no active reservations
    }
}
