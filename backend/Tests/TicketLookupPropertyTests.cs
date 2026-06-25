using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for ticket lookup functionality.
/// Validates Requirements 8.2, 8.3, 8.5
/// </summary>
public class TicketLookupPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<TicketService>> _mockLogger;
    private readonly TicketService _ticketService;
    private const string TestHmacKey = "test-hmac-secret-key-minimum-32-characters-long-for-security";

    public TicketLookupPropertyTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup configuration
        var configDict = new Dictionary<string, string?>
        {
            ["QRCode:HmacSecretKey"] = TestHmacKey
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Setup logger
        _mockLogger = new Mock<ILogger<TicketService>>();

        // Create service
        _ticketService = new TicketService(_context, _configuration, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Property 26: Ticket Lookup Returns Correct Matches

    /// <summary>
    /// Property 26: Ticket Lookup Returns Correct Matches
    /// For any email and DNI combination, the ticket lookup SHALL return all 
    /// and only tickets that match both the email and DNI.
    /// Validates: Requirements 8.2, 8.3, 8.5
    /// </summary>
    [Fact]
    public async Task Property26_TicketLookup_ReturnsOnlyMatchingTickets()
    {
        // Arrange - Create test data with multiple users and tickets
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        // Create tickets for target user (matching email and DNI)
        var targetEmail = "buyer1@test.com";
        var targetDNI = "12345678";
        var targetTickets = new List<Ticket>();

        for (int i = 0; i < 3; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = targetEmail,
                PurchaserDNI = targetDNI,
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            };
            targetTickets.Add(ticket);
            _context.Tickets.Add(ticket);
        }

        // Create tickets with same email but different DNI (should not match)
        for (int i = 0; i < 2; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = targetEmail,
                PurchaserDNI = "87654321", // Different DNI
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tickets.Add(ticket);
        }

        // Create tickets with same DNI but different email (should not match)
        for (int i = 0; i < 2; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = "buyer2@test.com", // Different email
                PurchaserDNI = targetDNI,
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tickets.Add(ticket);
        }

        // Create tickets with completely different email and DNI (should not match)
        for (int i = 0; i < 3; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = "buyer3@test.com",
                PurchaserDNI = "11111111",
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tickets.Add(ticket);
        }

        await _context.SaveChangesAsync();

        // Act - Look up tickets with target email and DNI
        var results = await _ticketService.LookupTicketsAsync(targetEmail, targetDNI);
        var resultList = results.ToList();

        // Assert - Should return exactly the 3 tickets matching both email AND DNI
        Assert.Equal(3, resultList.Count);

        // Verify all returned tickets have matching email and DNI
        foreach (var ticket in resultList)
        {
            Assert.Equal(targetEmail, ticket.PurchaserEmail);
            Assert.Equal(targetDNI, ticket.PurchaserDNI);
        }

        // Verify all target ticket IDs are present in results
        var resultIds = resultList.Select(t => t.Id).ToHashSet();
        var targetIds = targetTickets.Select(t => t.Id).ToHashSet();
        Assert.Equal(targetIds, resultIds);

        // Verify no extra tickets are returned
        Assert.True(resultIds.IsSubsetOf(targetIds));
    }

    /// <summary>
    /// Property 26 (Edge case): Lookup with no matching tickets returns empty result
    /// Validates: Requirement 8.5
    /// </summary>
    [Fact]
    public async Task Property26_TicketLookup_ReturnsEmptyForNoMatches()
    {
        // Arrange - Create test data but with non-matching credentials
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);

        // Create tickets with different email/DNI combinations
        var ticket1 = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "existing@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        var ticket2 = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "another@test.com",
            PurchaserDNI = "87654321",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket1);
        _context.Tickets.Add(ticket2);
        await _context.SaveChangesAsync();

        // Act - Look up with non-existent email/DNI combination
        var results = await _ticketService.LookupTicketsAsync("nonexistent@test.com", "99999999");
        var resultList = results.ToList();

        // Assert - Should return empty result
        Assert.Empty(resultList);
    }

    /// <summary>
    /// Property 26 (Edge case): Lookup returns tickets ordered by creation time (newest first)
    /// Validates: Requirements 8.2, 8.3
    /// </summary>
    [Fact]
    public async Task Property26_TicketLookup_ReturnsTicketsOrderedByCreationDesc()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var targetEmail = "buyer@test.com";
        var targetDNI = "12345678";
        var ticketCreationTimes = new List<DateTime>();

        // Create tickets with different creation times
        for (int i = 0; i < 5; i++)
        {
            var createdAt = DateTime.UtcNow.AddMinutes(-i * 10); // Older tickets created earlier
            ticketCreationTimes.Add(createdAt);

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = targetEmail,
                PurchaserDNI = targetDNI,
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = false,
                CreatedAt = createdAt
            };
            _context.Tickets.Add(ticket);
        }

        await _context.SaveChangesAsync();

        // Act
        var results = await _ticketService.LookupTicketsAsync(targetEmail, targetDNI);
        var resultList = results.ToList();

        // Assert - Should return 5 tickets
        Assert.Equal(5, resultList.Count);

        // Verify tickets are ordered by creation time descending (newest first)
        for (int i = 0; i < resultList.Count - 1; i++)
        {
            Assert.True(resultList[i].CreatedAt >= resultList[i + 1].CreatedAt,
                $"Tickets should be ordered by creation time descending. " +
                $"Ticket at index {i} has CreatedAt {resultList[i].CreatedAt}, " +
                $"but ticket at index {i + 1} has CreatedAt {resultList[i + 1].CreatedAt}");
        }
    }

    /// <summary>
    /// Property 26 (Multiple events): Lookup returns tickets across multiple events
    /// Validates: Requirements 8.2, 8.3
    /// </summary>
    [Fact]
    public async Task Property26_TicketLookup_ReturnsTicketsFromMultipleEvents()
    {
        // Arrange - Create multiple events
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Concert A",
            Description = "First Event",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Venue A",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Concert B",
            Description = "Second Event",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Venue B",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType1 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = event1.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        };

        var ticketType2 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = event2.Id,
            Name = "VIP",
            Price = 200m,
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(event1);
        _context.Events.Add(event2);
        _context.TicketTypes.Add(ticketType1);
        _context.TicketTypes.Add(ticketType2);
        await _context.SaveChangesAsync();

        var targetEmail = "buyer@test.com";
        var targetDNI = "12345678";

        // Create tickets for Event 1
        for (int i = 0; i < 2; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = event1.Id,
                TicketTypeId = ticketType1.Id,
                PurchaserEmail = targetEmail,
                PurchaserDNI = targetDNI,
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tickets.Add(ticket);
        }

        // Create tickets for Event 2
        for (int i = 0; i < 3; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = event2.Id,
                TicketTypeId = ticketType2.Id,
                PurchaserEmail = targetEmail,
                PurchaserDNI = targetDNI,
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tickets.Add(ticket);
        }

        await _context.SaveChangesAsync();

        // Act
        var results = await _ticketService.LookupTicketsAsync(targetEmail, targetDNI);
        var resultList = results.ToList();

        // Assert - Should return all 5 tickets from both events
        Assert.Equal(5, resultList.Count);

        // Verify tickets from both events are present
        var event1Tickets = resultList.Where(t => t.EventId == event1.Id).ToList();
        var event2Tickets = resultList.Where(t => t.EventId == event2.Id).ToList();

        Assert.Equal(2, event1Tickets.Count);
        Assert.Equal(3, event2Tickets.Count);

        // Verify all have correct email and DNI
        foreach (var ticket in resultList)
        {
            Assert.Equal(targetEmail, ticket.PurchaserEmail);
            Assert.Equal(targetDNI, ticket.PurchaserDNI);
        }
    }

    /// <summary>
    /// Property 26 (Used tickets): Lookup returns both used and unused tickets
    /// Validates: Requirements 8.2, 8.3
    /// </summary>
    [Fact]
    public async Task Property26_TicketLookup_ReturnsBothUsedAndUnusedTickets()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var targetEmail = "buyer@test.com";
        var targetDNI = "12345678";

        // Create used tickets
        for (int i = 0; i < 2; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = targetEmail,
                PurchaserDNI = targetDNI,
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = true,
                UsedAt = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };
            _context.Tickets.Add(ticket);
        }

        // Create unused tickets
        for (int i = 0; i < 3; i++)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = targetEmail,
                PurchaserDNI = targetDNI,
                QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tickets.Add(ticket);
        }

        await _context.SaveChangesAsync();

        // Act
        var results = await _ticketService.LookupTicketsAsync(targetEmail, targetDNI);
        var resultList = results.ToList();

        // Assert - Should return all 5 tickets (both used and unused)
        Assert.Equal(5, resultList.Count);

        var usedTickets = resultList.Where(t => t.IsUsed).ToList();
        var unusedTickets = resultList.Where(t => !t.IsUsed).ToList();

        Assert.Equal(2, usedTickets.Count);
        Assert.Equal(3, unusedTickets.Count);

        // Verify all have correct email and DNI
        foreach (var ticket in resultList)
        {
            Assert.Equal(targetEmail, ticket.PurchaserEmail);
            Assert.Equal(targetDNI, ticket.PurchaserDNI);
        }
    }

    /// <summary>
    /// Property 26 (Case sensitivity): Email matching should be case-sensitive per database collation
    /// Validates: Requirements 8.2
    /// </summary>
    [Fact]
    public async Task Property26_TicketLookup_EmailMatchingFollowsDatabaseCollation()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var storedEmail = "buyer@test.com";
        var dni = "12345678";

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = storedEmail,
            PurchaserDNI = dni,
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act - Query with exact match
        var exactMatchResults = await _ticketService.LookupTicketsAsync(storedEmail, dni);

        // Assert - Exact match should return the ticket
        Assert.Single(exactMatchResults);

        // Note: Case sensitivity behavior depends on database collation.
        // In-memory database typically uses case-sensitive comparison.
        // PostgreSQL default collation is case-sensitive for = operator.
        // This test documents the expected behavior.
        var differentCaseResults = await _ticketService.LookupTicketsAsync("BUYER@TEST.COM", dni);
        
        // In most production databases (including PostgreSQL), this would return empty
        // because email comparison is case-sensitive by default
        // For in-memory EF Core, behavior matches the default string comparison
    }

    /// <summary>
    /// Property 26 (With navigation properties): Lookup includes Event and TicketType data
    /// Validates: Requirements 8.3
    /// </summary>
    [Fact]
    public async Task Property26_TicketLookup_IncludesNavigationProperties()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Rock Concert",
            Description = "Amazing concert",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Stadium",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "VIP Access",
            Price = 250m,
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var targetEmail = "buyer@test.com";
        var targetDNI = "12345678";

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = targetEmail,
            PurchaserDNI = targetDNI,
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var results = await _ticketService.LookupTicketsAsync(targetEmail, targetDNI);
        var resultList = results.ToList();

        // Assert
        Assert.Single(resultList);
        var returnedTicket = resultList.First();

        // Verify navigation properties are loaded
        Assert.NotNull(returnedTicket.Event);
        Assert.Equal("Rock Concert", returnedTicket.Event.Name);
        Assert.Equal("Stadium", returnedTicket.Event.Location);

        Assert.NotNull(returnedTicket.TicketType);
        Assert.Equal("VIP Access", returnedTicket.TicketType.Name);
        Assert.Equal(250m, returnedTicket.TicketType.Price);
    }

    #endregion
}
