using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for organizer dashboard metrics.
/// Validates Requirements 11.2, 11.3, 11.4, 11.5, 11.6
/// </summary>
public class MetricsPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IMetricsService _metricsService;

    public MetricsPropertyTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<MetricsService>();

        _metricsService = new MetricsService(_context, logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Property 33: Dashboard Displays Owner's Events Only

    /// <summary>
    /// Property 33: Dashboard Displays Owner's Events Only
    /// For any organizador viewing the dashboard, only events owned by that organizador SHALL be displayed.
    /// **Validates: Requirements 11.2**
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetrics_ReturnsOnlyOwnersEvents()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();

        var owner = new User
        {
            Id = ownerId,
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var otherOrganizer = new User
        {
            Id = otherOrganizerId,
            Email = "other@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, otherOrganizer);

        var ownerEvent1 = CreateEvent(ownerId, "Owner Event 1");
        var ownerEvent2 = CreateEvent(ownerId, "Owner Event 2");
        var otherEvent = CreateEvent(otherOrganizerId, "Other Event");

        _context.Events.AddRange(ownerEvent1, ownerEvent2, otherEvent);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetOrganizerMetricsAsync(ownerId);

        // Assert
        Assert.NotNull(metrics);
        var metricsList = metrics.ToList();
        Assert.Equal(2, metricsList.Count);
        Assert.Contains(metricsList, m => m.EventId == ownerEvent1.Id);
        Assert.Contains(metricsList, m => m.EventId == ownerEvent2.Id);
        Assert.DoesNotContain(metricsList, m => m.EventId == otherEvent.Id);
    }

    /// <summary>
    /// Property 33 (Edge Case): Organizer with no events receives empty metrics.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetrics_NoEvents_ReturnsEmpty()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var owner = new User
        {
            Id = ownerId,
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(owner);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetOrganizerMetricsAsync(ownerId);

        // Assert
        Assert.NotNull(metrics);
        Assert.Empty(metrics);
    }

    #endregion

    #region Property 34: Tickets Sold Calculation Correctness

    /// <summary>
    /// Property 34: Tickets Sold Calculation Correctness
    /// For any event, the displayed tickets sold count SHALL equal the number of confirmed tickets in the database for that event.
    /// **Validates: Requirements 11.3**
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_TicketsSold_MatchesTicketCount()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Ticket Sales Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var scenarios = new[] { 0, 1, 5, 50 };
        foreach (var soldCount in scenarios)
        {
            // Clean previous tickets for this event to isolate the scenario
            var existingTickets = _context.Tickets.Where(t => t.EventId == eventEntity.Id).ToList();
            _context.Tickets.RemoveRange(existingTickets);
            await _context.SaveChangesAsync();

            for (int i = 0; i < soldCount; i++)
            {
                _context.Tickets.Add(new Ticket
                {
                    Id = Guid.NewGuid(),
                    EventId = eventEntity.Id,
                    TicketTypeId = ticketType.Id,
                    PurchaserEmail = $"buyer{i}@example.com",
                    PurchaserDNI = $"DNI{i}",
                    QRCodeData = $"QR-{Guid.NewGuid()}",
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(soldCount, metrics.TicketsSold);
        }
    }

    #endregion

    #region Property 35: Revenue Calculation Correctness

    /// <summary>
    /// Property 35: Revenue Calculation Correctness
    /// For any event, the displayed total revenue SHALL equal the sum of (ticket price × quantity) for all confirmed tickets.
    /// **Validates: Requirements 11.4**
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_TotalRevenue_MatchesSumOfTicketPrices()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Revenue Event");
        var vipType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "VIP",
            Price = 200,
            Quantity = 20,
            CreatedAt = DateTime.UtcNow
        };
        var generalType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(vipType);
        eventEntity.TicketTypes.Add(generalType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Sell 5 VIP tickets and 12 General tickets
        for (int i = 0; i < 5; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = vipType.Id,
                PurchaserEmail = $"vip{i}@example.com",
                PurchaserDNI = $"VIP{i}",
                QRCodeData = $"QR-VIP-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        for (int i = 0; i < 12; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = generalType.Id,
                PurchaserEmail = $"general{i}@example.com",
                PurchaserDNI = $"GEN{i}",
                QRCodeData = $"QR-GEN-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        var expectedRevenue = (5 * vipType.Price) + (12 * generalType.Price);
        Assert.Equal(expectedRevenue, metrics.TotalRevenue);
    }

    /// <summary>
    /// Property 35 (Edge Case): Event with no sold tickets has zero revenue.
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_NoTicketsSold_RevenueIsZero()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "No Sales Event");
        eventEntity.TicketTypes.Add(new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        });
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.TicketsSold);
        Assert.Equal(0m, metrics.TotalRevenue);
    }

    #endregion

    #region Property 36: Remaining Inventory Calculation Correctness

    /// <summary>
    /// Property 36: Remaining Inventory Calculation Correctness
    /// For any event, the displayed remaining inventory SHALL equal the total ticket type quantities minus confirmed tickets sold minus active reservations.
    /// **Validates: Requirements 11.5**
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_RemainingInventory_CalculationIsCorrect()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Inventory Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Sell 10 tickets
        for (int i = 0; i < 10; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = $"buyer{i}@example.com",
                PurchaserDNI = $"DNI{i}",
                QRCodeData = $"QR-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Create 15 active reservations
        for (int i = 0; i < 15; i++)
        {
            _context.Reservations.Add(new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                Quantity = 1,
                PurchaserDNI = $"RES{i}",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        var expectedRemainingInventory = ticketType.Quantity - 10 - 15;
        Assert.Equal(expectedRemainingInventory, metrics.RemainingInventory);
    }

    /// <summary>
    /// Property 36 (Edge Case): Expired reservations do not reduce remaining inventory.
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_ExpiredReservations_DoNotReduceInventory()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Expired Reservations Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Add an expired active reservation (status Active but past expiration)
        _context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 30,
            PurchaserDNI = "EXPIRED",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        });
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(ticketType.Quantity, metrics.RemainingInventory);
    }

    #endregion

    #region Property 37: Scanned Tickets Count Correctness

    /// <summary>
    /// Property 37: Scanned Tickets Count Correctness
    /// For any event, the displayed scanned tickets count SHALL equal the number of tickets marked as used (IsUsed = true).
    /// **Validates: Requirements 11.6**
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_TicketsScanned_MatchesUsedTickets()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Scan Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var scenarios = new[] { 0, 1, 5, 20 };
        foreach (var usedCount in scenarios)
        {
            // Clean previous tickets
            var existingTickets = _context.Tickets.Where(t => t.EventId == eventEntity.Id).ToList();
            _context.Tickets.RemoveRange(existingTickets);
            await _context.SaveChangesAsync();

            // Create 30 tickets, mark `usedCount` as used
            for (int i = 0; i < 30; i++)
            {
                _context.Tickets.Add(new Ticket
                {
                    Id = Guid.NewGuid(),
                    EventId = eventEntity.Id,
                    TicketTypeId = ticketType.Id,
                    PurchaserEmail = $"buyer{i}@example.com",
                    PurchaserDNI = $"DNI{i}",
                    QRCodeData = $"QR-{Guid.NewGuid()}",
                    IsUsed = i < usedCount,
                    UsedAt = i < usedCount ? DateTime.UtcNow : null,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(usedCount, metrics.TicketsScanned);
        }
    }

    /// <summary>
    /// Property 36 (Multiple Ticket Types): Remaining inventory accounts for all ticket types.
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_RemainingInventory_WorksForMultipleTicketTypes()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Multi-Type Inventory Event");
        var vipType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "VIP",
            Price = 200,
            Quantity = 20,
            CreatedAt = DateTime.UtcNow
        };
        var generalType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 80,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(vipType);
        eventEntity.TicketTypes.Add(generalType);
        _context.Events.Add(eventEntity);

        // Sell 5 VIP and 10 General
        for (int i = 0; i < 5; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = vipType.Id,
                PurchaserEmail = $"vip{i}@example.com",
                PurchaserDNI = $"VIP{i}",
                QRCodeData = $"QR-VIP-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        for (int i = 0; i < 10; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = generalType.Id,
                PurchaserEmail = $"general{i}@example.com",
                PurchaserDNI = $"GEN{i}",
                QRCodeData = $"QR-GEN-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Active reservation for 3 General tickets
        _context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = generalType.Id,
            Quantity = 3,
            PurchaserDNI = "RESERVED",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        var expectedRemaining = (vipType.Quantity + generalType.Quantity) - 5 - 10 - 3;
        Assert.Equal(expectedRemaining, metrics.RemainingInventory);
    }

    #endregion

    #region Event Not Found

    [Fact]
    public async Task GetEventMetrics_NonExistentEvent_ReturnsNull()
    {
        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(Guid.NewGuid());

        // Assert
        Assert.Null(metrics);
    }

    #endregion

    #region EA-007 — EventMetrics carries Status

    [Fact]
    public async Task GetOrganizerMetrics_EachEventCarriesItsStatus()
    {
        // EA-007: the projection copies Status so the dashboard can render badges.
        var organizerId = Guid.NewGuid();
        var pending = CreateEvent(organizerId, "Pending Own");
        pending.Status = EventStatus.Pending;
        var approved = CreateEvent(organizerId, "Approved Own");
        approved.Status = EventStatus.Approved;
        var rejected = CreateEvent(organizerId, "Rejected Own");
        rejected.Status = EventStatus.Rejected;
        _context.Events.AddRange(pending, approved, rejected);
        await _context.SaveChangesAsync();

        // Act
        var metrics = (await _metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        // Assert
        Assert.Equal(3, metrics.Count);
        Assert.Equal(EventStatus.Pending, metrics.Single(m => m.EventId == pending.Id).Status);
        Assert.Equal(EventStatus.Approved, metrics.Single(m => m.EventId == approved.Id).Status);
        Assert.Equal(EventStatus.Rejected, metrics.Single(m => m.EventId == rejected.Id).Status);
    }

    [Fact]
    public async Task GetEventMetrics_SingleEvent_ReturnsItsStatus()
    {
        // EA-007: single-event metrics carry Status too.
        var organizerId = Guid.NewGuid();
        var eventEntity = CreateEvent(organizerId, "Rejected Single");
        eventEntity.Status = EventStatus.Rejected;
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(EventStatus.Rejected, metrics.Status);
    }

    #endregion

    private static Event CreateEvent(Guid organizerId, string name)
    {
        var now = DateTime.UtcNow;
        return new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test event for metrics",
            Date = now.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    #region Refunded tickets excluded (APR-005)

    [Fact]
    public async Task GetEventMetrics_RefundedTickets_ExcludedFromSoldAndRevenue()
    {
        // Arrange — 5 tickets, 2 used, 1 refunded (APR-005: refunded stops counting)
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Refunded Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 50,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);

        for (var i = 0; i < 5; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
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
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert — sold = 4 (refunded excluded), revenue = 4 × 50, scanned = 2 (unchanged)
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics.TicketsSold);
        Assert.Equal(200m, metrics.TotalRevenue);
        Assert.Equal(2, metrics.TicketsScanned);
        // 100 inventory - 4 sold - 0 reservations = 96 remaining
        Assert.Equal(96, metrics.RemainingInventory);
    }

    #endregion
}
