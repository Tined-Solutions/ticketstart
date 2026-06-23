using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for reservation management functionality
/// Validates Requirements 4.1, 4.2, 4.4
/// </summary>
public class ReservationPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IReservationService _reservationService;

    public ReservationPropertyTests()
    {
        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ReservationService>();

        _reservationService = new ReservationService(_context, logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Property 10: Reservation Creation Sets Correct Expiration

    /// <summary>
    /// Property 10: Reservation Creation Sets Correct Expiration
    /// For any ticket selection, creating a reservation SHALL set the expiration 
    /// time to exactly 10 minutes from creation.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public async Task ReservationCreation_SetsExpirationToExactly10Minutes()
    {
        // Test multiple scenarios to verify property holds universally
        var testScenarios = new[]
        {
            new { UserId = (Guid?)Guid.NewGuid(), Quantity = 1 },
            new { UserId = (Guid?)Guid.NewGuid(), Quantity = 5 },
            new { UserId = (Guid?)null, Quantity = 3 }, // Guest purchase
            new { UserId = (Guid?)Guid.NewGuid(), Quantity = 10 }
        };

        foreach (var scenario in testScenarios)
        {
            // Arrange - Create event with ticket types
            var organizerId = Guid.NewGuid();
            var organizer = new User
            {
                Id = organizerId,
                Email = $"organizer-{Guid.NewGuid()}@example.com",
                PasswordHash = "dummy-hash",
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(organizer);

            var eventEntity = new Event
            {
                Id = Guid.NewGuid(),
                Name = $"Test Event {Guid.NewGuid()}",
                Description = "Test event for reservation expiration",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Test Location",
                ImageUrl = "https://example.com/test.jpg",
                OrganizerId = organizerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Events.Add(eventEntity);

            var ticketType = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                Name = "General Admission",
                Price = 50,
                Quantity = 100,
                CreatedAt = DateTime.UtcNow
            };
            _context.TicketTypes.Add(ticketType);
            await _context.SaveChangesAsync();

            // Act - Create reservation and capture time immediately
            var beforeCreation = DateTime.UtcNow;
            var reservation = await _reservationService.CreateReservationAsync(
                scenario.UserId, 
                eventEntity.Id, 
                ticketType.Id, 
                scenario.Quantity
            );
            var afterCreation = DateTime.UtcNow;

            // Assert - Expiration should be exactly 10 minutes from creation
            Assert.NotNull(reservation);
            
            // Calculate expected expiration range (accounting for execution time)
            var minExpiration = beforeCreation.AddMinutes(10);
            var maxExpiration = afterCreation.AddMinutes(10);

            // Verify expiration is within the expected range
            Assert.True(reservation.ExpiresAt >= minExpiration, 
                $"Expiration {reservation.ExpiresAt} should be >= {minExpiration}");
            Assert.True(reservation.ExpiresAt <= maxExpiration, 
                $"Expiration {reservation.ExpiresAt} should be <= {maxExpiration}");

            // Verify the difference from creation to expiration is approximately 10 minutes
            var timeDifference = reservation.ExpiresAt - reservation.CreatedAt;
            var expectedDifference = TimeSpan.FromMinutes(10);
            
            // Allow 1 second tolerance for execution time
            Assert.True(Math.Abs((timeDifference - expectedDifference).TotalSeconds) <= 1,
                $"Expected 10 minutes expiration, but got {timeDifference.TotalMinutes} minutes");
        }
    }

    #endregion

    #region Property 11: Reservation Decrements Inventory

    /// <summary>
    /// Property 11: Reservation Decrements Inventory
    /// For any reservation with quantity N, the available ticket inventory 
    /// SHALL decrease by N.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Fact]
    public async Task ReservationCreation_DecrementsInventoryByQuantity()
    {
        // Test various quantities to verify property holds universally
        var testScenarios = new[]
        {
            new { InitialQuantity = 100, ReservationQuantity = 1, ExpectedAvailable = 99 },
            new { InitialQuantity = 50, ReservationQuantity = 10, ExpectedAvailable = 40 },
            new { InitialQuantity = 200, ReservationQuantity = 50, ExpectedAvailable = 150 },
            new { InitialQuantity = 25, ReservationQuantity = 25, ExpectedAvailable = 0 },
            new { InitialQuantity = 75, ReservationQuantity = 3, ExpectedAvailable = 72 }
        };

        foreach (var scenario in testScenarios)
        {
            // Arrange - Create event with ticket type
            var organizerId = Guid.NewGuid();
            var organizer = new User
            {
                Id = organizerId,
                Email = $"organizer-{Guid.NewGuid()}@example.com",
                PasswordHash = "dummy-hash",
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(organizer);

            var eventEntity = new Event
            {
                Id = Guid.NewGuid(),
                Name = $"Test Event {Guid.NewGuid()}",
                Description = "Test event for inventory decrement",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Test Location",
                ImageUrl = "https://example.com/test.jpg",
                OrganizerId = organizerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Events.Add(eventEntity);

            var ticketType = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                Name = "General Admission",
                Price = 50,
                Quantity = scenario.InitialQuantity,
                CreatedAt = DateTime.UtcNow
            };
            _context.TicketTypes.Add(ticketType);
            await _context.SaveChangesAsync();

            // Calculate initial available inventory (no reservations or tickets yet)
            var initialAvailable = scenario.InitialQuantity;

            // Act - Create reservation
            var userId = Guid.NewGuid();
            var reservation = await _reservationService.CreateReservationAsync(
                userId, 
                eventEntity.Id, 
                ticketType.Id, 
                scenario.ReservationQuantity
            );

            // Assert - Verify reservation was created
            Assert.NotNull(reservation);
            Assert.Equal(scenario.ReservationQuantity, reservation.Quantity);
            Assert.Equal(ReservationStatus.Active, reservation.Status);

            // Calculate new available inventory after reservation
            var activeReservations = await _context.Reservations
                .Where(r => r.TicketTypeId == ticketType.Id &&
                            r.Status == ReservationStatus.Active &&
                            r.ExpiresAt > DateTime.UtcNow)
                .SumAsync(r => r.Quantity);

            var soldTickets = await _context.Tickets
                .CountAsync(t => t.TicketTypeId == ticketType.Id);

            var currentAvailable = ticketType.Quantity - soldTickets - activeReservations;

            // Verify inventory decreased by exactly the reservation quantity
            Assert.Equal(scenario.ExpectedAvailable, currentAvailable);
            Assert.Equal(initialAvailable - scenario.ReservationQuantity, currentAvailable);
        }
    }

    /// <summary>
    /// Property 11 (Multiple Reservations): Multiple reservations decrement inventory correctly
    /// </summary>
    [Fact]
    public async Task MultipleReservations_DecrementInventoryCumulatively()
    {
        // Arrange - Create event with ticket type
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

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Multi-Reservation Event",
            Description = "Test event for multiple reservations",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 50,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        // Act - Create multiple reservations by different users
        var reservationQuantities = new[] { 5, 10, 15, 20 };
        var totalReserved = 0;

        foreach (var quantity in reservationQuantities)
        {
            var userId = Guid.NewGuid();
            var reservation = await _reservationService.CreateReservationAsync(
                userId, 
                eventEntity.Id, 
                ticketType.Id, 
                quantity
            );

            Assert.NotNull(reservation);
            totalReserved += quantity;

            // Verify available inventory after each reservation
            var activeReservations = await _context.Reservations
                .Where(r => r.TicketTypeId == ticketType.Id &&
                            r.Status == ReservationStatus.Active &&
                            r.ExpiresAt > DateTime.UtcNow)
                .SumAsync(r => r.Quantity);

            var soldTickets = await _context.Tickets
                .CountAsync(t => t.TicketTypeId == ticketType.Id);

            var currentAvailable = ticketType.Quantity - soldTickets - activeReservations;

            // Available should be initial quantity minus total reserved so far
            Assert.Equal(100 - totalReserved, currentAvailable);
        }

        // Assert final state
        Assert.Equal(50, totalReserved); // 5 + 10 + 15 + 20 = 50
        
        var finalActiveReservations = await _context.Reservations
            .Where(r => r.TicketTypeId == ticketType.Id &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > DateTime.UtcNow)
            .SumAsync(r => r.Quantity);

        Assert.Equal(50, finalActiveReservations);
        
        var finalAvailable = 100 - finalActiveReservations;
        Assert.Equal(50, finalAvailable);
    }

    #endregion

    #region Property 12: Active Reservations Prevent Double-Booking

    /// <summary>
    /// Property 12: Active Reservations Prevent Double-Booking
    /// For any active reservation, other users SHALL NOT be able to reserve 
    /// the same tickets until the reservation expires or is confirmed.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Fact]
    public async Task ActiveReservation_PreventsDoubleBooking()
    {
        // Arrange - Create event with limited ticket inventory
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

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Limited Capacity Event",
            Description = "Event with limited tickets",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 50,
            Quantity = 10, // Limited quantity
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        // Act - User 1 reserves all available tickets
        var user1Id = Guid.NewGuid();
        var reservation1 = await _reservationService.CreateReservationAsync(
            user1Id, 
            eventEntity.Id, 
            ticketType.Id, 
            10 // Reserve all 10 tickets
        );

        // Assert - Reservation 1 should succeed
        Assert.NotNull(reservation1);
        Assert.Equal(10, reservation1.Quantity);
        Assert.Equal(ReservationStatus.Active, reservation1.Status);

        // Act - User 2 attempts to reserve tickets while User 1's reservation is active
        var user2Id = Guid.NewGuid();
        
        // Should throw exception because no tickets are available
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _reservationService.CreateReservationAsync(
                user2Id, 
                eventEntity.Id, 
                ticketType.Id, 
                1 // Try to reserve just 1 ticket
            );
        });

        // Assert - Exception should indicate insufficient tickets
        Assert.Contains("Insufficient tickets", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Verify only User 1's reservation exists
        var allActiveReservations = await _context.Reservations
            .Where(r => r.TicketTypeId == ticketType.Id &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        Assert.Single(allActiveReservations);
        Assert.Equal(user1Id, allActiveReservations[0].UserId);
        Assert.Equal(10, allActiveReservations[0].Quantity);
    }

    /// <summary>
    /// Property 12 (Partial Availability): Allows reservations up to available quantity
    /// </summary>
    [Fact]
    public async Task ActiveReservation_AllowsPartialBookingUpToAvailableQuantity()
    {
        // Arrange - Create event with ticket inventory
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

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Partial Booking Event",
            Description = "Event for partial booking test",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 50,
            Quantity = 20,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        // Act - User 1 reserves 12 tickets
        var user1Id = Guid.NewGuid();
        var reservation1 = await _reservationService.CreateReservationAsync(
            user1Id, 
            eventEntity.Id, 
            ticketType.Id, 
            12
        );

        // Assert - Reservation 1 should succeed
        Assert.NotNull(reservation1);
        Assert.Equal(12, reservation1.Quantity);

        // Act - User 2 can reserve up to remaining 8 tickets
        var user2Id = Guid.NewGuid();
        var reservation2 = await _reservationService.CreateReservationAsync(
            user2Id, 
            eventEntity.Id, 
            ticketType.Id, 
            8 // Exactly the remaining amount
        );

        // Assert - Reservation 2 should succeed
        Assert.NotNull(reservation2);
        Assert.Equal(8, reservation2.Quantity);

        // Act - User 3 attempts to reserve any ticket (none available)
        var user3Id = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _reservationService.CreateReservationAsync(
                user3Id, 
                eventEntity.Id, 
                ticketType.Id, 
                1
            );
        });

        // Assert - User 3 should be rejected
        Assert.Contains("Insufficient tickets", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Verify all reservations
        var allActiveReservations = await _context.Reservations
            .Where(r => r.TicketTypeId == ticketType.Id &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        Assert.Equal(2, allActiveReservations.Count);
        Assert.Equal(20, allActiveReservations.Sum(r => r.Quantity));
    }

    /// <summary>
    /// Property 12 (With Sold Tickets): Considers both sold tickets and active reservations
    /// </summary>
    [Fact]
    public async Task ActiveReservation_ConsidersBothSoldTicketsAndActiveReservations()
    {
        // Arrange - Create event with ticket inventory
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

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Mixed Inventory Event",
            Description = "Event with sold tickets and reservations",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 50,
            Quantity = 30,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        // Create 10 sold tickets (confirmed purchases)
        for (int i = 0; i < 10; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = $"buyer{i}@example.com",
                PurchaserDNI = $"DNI{i}",
                QRCodeData = $"QR-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tickets.Add(ticket);
        }
        await _context.SaveChangesAsync();

        // Act - User 1 reserves 15 tickets (should work: 30 total - 10 sold = 20 available)
        var user1Id = Guid.NewGuid();
        var reservation1 = await _reservationService.CreateReservationAsync(
            user1Id, 
            eventEntity.Id, 
            ticketType.Id, 
            15
        );

        // Assert - Reservation 1 should succeed
        Assert.NotNull(reservation1);
        Assert.Equal(15, reservation1.Quantity);

        // Act - User 2 can reserve up to remaining 5 tickets (30 - 10 sold - 15 reserved = 5)
        var user2Id = Guid.NewGuid();
        var reservation2 = await _reservationService.CreateReservationAsync(
            user2Id, 
            eventEntity.Id, 
            ticketType.Id, 
            5
        );

        // Assert - Reservation 2 should succeed
        Assert.NotNull(reservation2);
        Assert.Equal(5, reservation2.Quantity);

        // Act - User 3 attempts to reserve (none available)
        var user3Id = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _reservationService.CreateReservationAsync(
                user3Id, 
                eventEntity.Id, 
                ticketType.Id, 
                1
            );
        });

        // Assert - User 3 should be rejected
        Assert.Contains("Insufficient tickets", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Verify inventory accounting: 10 sold + 15 reserved + 5 reserved = 30 total
        var soldCount = await _context.Tickets.CountAsync(t => t.TicketTypeId == ticketType.Id);
        var reservedCount = await _context.Reservations
            .Where(r => r.TicketTypeId == ticketType.Id &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > DateTime.UtcNow)
            .SumAsync(r => r.Quantity);

        Assert.Equal(10, soldCount);
        Assert.Equal(20, reservedCount);
        Assert.Equal(30, soldCount + reservedCount); // All tickets accounted for
    }

    /// <summary>
    /// Property 12 (After Expiration): Allows new reservations after expiration
    /// </summary>
    [Fact]
    public async Task ExpiredReservation_AllowsNewReservations()
    {
        // Arrange - Create event with ticket inventory
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

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Expiration Test Event",
            Description = "Event for testing expired reservations",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 50,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        // Create an expired reservation manually (simulating past expiration)
        var expiredReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 10,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Already expired
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-11)
        };
        _context.Reservations.Add(expiredReservation);
        await _context.SaveChangesAsync();

        // Act - User 2 attempts to reserve tickets (should work because first reservation expired)
        var user2Id = Guid.NewGuid();
        var reservation2 = await _reservationService.CreateReservationAsync(
            user2Id, 
            eventEntity.Id, 
            ticketType.Id, 
            10
        );

        // Assert - New reservation should succeed because expired reservations don't count
        Assert.NotNull(reservation2);
        Assert.Equal(10, reservation2.Quantity);
        Assert.Equal(ReservationStatus.Active, reservation2.Status);
        Assert.True(reservation2.ExpiresAt > DateTime.UtcNow);

        // Verify only active, non-expired reservations are counted
        var activeReservations = await _context.Reservations
            .Where(r => r.TicketTypeId == ticketType.Id &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        Assert.Single(activeReservations);
        Assert.Equal(user2Id, activeReservations[0].UserId);
    }

    #endregion
}
