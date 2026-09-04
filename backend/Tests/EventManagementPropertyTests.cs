using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;
using Moq;
using Amazon.S3;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for event management functionality
/// Validates Requirements 2.2, 2.6, 10.3, 10.4, 10.7
/// </summary>
public class EventManagementPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IEventService _eventService;
    private readonly IConfiguration _configuration;

    public EventManagementPropertyTests()
    {
        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup configuration with R2 settings
        var inMemorySettings = new Dictionary<string, string>
        {
            {"CloudflareR2:BucketName", "test-bucket"},
            {"CloudflareR2:PublicUrl", "https://pub-test.r2.dev"},
            {"CloudflareR2:AccessKeyId", "test-access-key"},
            {"CloudflareR2:SecretAccessKey", "test-secret-key"},
            {"CloudflareR2:AccountId", "test-account-id"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<EventService>();

        // Mock S3 client for image operations
        var mockS3Client = new Mock<IR2StorageClient>();


        _eventService = new EventService(_context, logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new HideExpiredEventsOptions()));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Property 5: Event Rendering Includes All Required Fields

    /// <summary>
    /// Property 5: Event Rendering Includes All Required Fields
    /// For any event, the rendered output SHALL include name, date, location, description, and image URL.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Fact]
    public async Task EventRendering_IncludesAllRequiredFields()
    {
        // Test with multiple events to verify the property holds universally
        var testEvents = new[]
        {
            new
            {
                Name = "Music Festival 2024",
                Description = "Annual summer music festival",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Central Park, NY",
                ImageUrl = "https://example.com/festival.jpg"
            },
            new
            {
                Name = "Tech Conference",
                Description = "Latest in AI and ML",
                Date = DateTime.UtcNow.AddDays(60),
                Location = "Convention Center, SF",
                ImageUrl = "https://example.com/tech.jpg"
            },
            new
            {
                Name = "Food & Wine Expo",
                Description = "Culinary delights from around the world",
                Date = DateTime.UtcNow.AddDays(90),
                Location = "Exhibition Hall, Chicago",
                ImageUrl = "https://example.com/food.jpg"
            }
        };

        foreach (var testEvent in testEvents)
        {
            // Arrange - Create a user to own the event
            var organizerId = Guid.NewGuid();
            var organizer = new User
            {
                Id = organizerId,
                Email = $"organizer-{organizerId}@example.com",
                PasswordHash = "dummy-hash",
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(organizer);
            await _context.SaveChangesAsync();

            // Create event
            var createRequest = new CreateEventRequest
            {
                Name = testEvent.Name,
                Description = testEvent.Description,
                Date = testEvent.Date,
                Location = testEvent.Location,
                ImageUrl = testEvent.ImageUrl,
                TicketTypes = new List<CreateTicketTypeRequest>
                {
                    new CreateTicketTypeRequest { Name = "General", Price = 100, Quantity = 50 }
                }
            };

            var createdEvent = await _eventService.CreateEventAsync(createRequest, organizerId);

            // Act - Retrieve the event (simulating rendering/display)
            var retrievedEvent = await _eventService.GetEventByIdAsync(createdEvent.Id);

            // Assert - Verify ALL required fields are present and non-empty
            Assert.NotNull(retrievedEvent);
            
            // Name must be present
            Assert.NotNull(retrievedEvent.Name);
            Assert.NotEmpty(retrievedEvent.Name);
            Assert.Equal(testEvent.Name, retrievedEvent.Name);
            
            // Date must be present
            Assert.NotEqual(default(DateTime), retrievedEvent.Date);
            Assert.Equal(testEvent.Date, retrievedEvent.Date);
            
            // Location must be present
            Assert.NotNull(retrievedEvent.Location);
            Assert.NotEmpty(retrievedEvent.Location);
            Assert.Equal(testEvent.Location, retrievedEvent.Location);
            
            // Description must be present
            Assert.NotNull(retrievedEvent.Description);
            Assert.Equal(testEvent.Description, retrievedEvent.Description);
            
            // Image URL must be present
            Assert.NotNull(retrievedEvent.ImageUrl);
            Assert.NotEmpty(retrievedEvent.ImageUrl);
            Assert.Equal(testEvent.ImageUrl, retrievedEvent.ImageUrl);
        }
    }

    #endregion

    #region Property 6: Ticket Availability Calculation Correctness

    /// <summary>
    /// Property 6: Ticket Availability Calculation Correctness
    /// For any event with ticket types, the calculated availability SHALL equal 
    /// the ticket type quantity minus sold tickets minus active unexpired reservations.
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Fact]
    public async Task TicketAvailabilityCalculation_EqualsQuantityMinusOccupiedTickets()
    {
        // Test various scenarios with different quantities and active reservation counts.
        // Availability = Quantity - sold - active reservations (mathematical, no counter)
        var testScenarios = new[]
        {
            new { InitialQuantity = 100, ReservedQuantity = 0, ExpectedAvailable = 100 },
            new { InitialQuantity = 50, ReservedQuantity = 25, ExpectedAvailable = 25 },
            new { InitialQuantity = 200, ReservedQuantity = 199, ExpectedAvailable = 1 },
            new { InitialQuantity = 75, ReservedQuantity = 75, ExpectedAvailable = 0 },
            new { InitialQuantity = 10, ReservedQuantity = 3, ExpectedAvailable = 7 }
        };

        foreach (var scenario in testScenarios)
        {
            // Arrange - Create a user and event
            var organizerId = Guid.NewGuid();
            var organizer = new User
            {
                Id = organizerId, Name = "Org",
                Email = $"organizer-{Guid.NewGuid()}@example.com",
                PasswordHash = "dummy-hash",
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(organizer);

            var createRequest = new CreateEventRequest
            {
                Name = $"Test Event {Guid.NewGuid()}",
                Description = "Testing availability calculation",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Test Location",
                ImageUrl = "https://example.com/test.jpg",
                TicketTypes = new List<CreateTicketTypeRequest>
                {
                    new CreateTicketTypeRequest 
                    { 
                        Name = "General Admission", 
                        Price = 50, 
                        Quantity = scenario.InitialQuantity 
                    }
                }
            };

            var createdEvent = await _eventService.CreateEventAsync(createRequest, organizerId);
            var ticketType = createdEvent.TicketTypes.First();

            // Simulate active reservations by creating real reservation rows
            if (scenario.ReservedQuantity > 0)
            {
                _context.Reservations.Add(new Reservation
                {
                    Id = Guid.NewGuid(),
                    EventId = ticketType.EventId,
                    TicketTypeId = ticketType.Id,
                    Quantity = scenario.ReservedQuantity,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Status = ReservationStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            // Act - Retrieve event with availability
            var eventWithAvailability = await _eventService.GetEventByIdAsync(createdEvent.Id);

            // Assert - Verify availability calculation: Quantity - active reservations = Available
            Assert.NotNull(eventWithAvailability);
            Assert.NotEmpty(eventWithAvailability.TicketTypes);
            
            var retrievedType = eventWithAvailability.TicketTypes.First();
            Assert.Equal(scenario.InitialQuantity, retrievedType.Quantity);
            Assert.Equal(scenario.ExpectedAvailable, retrievedType.Available);
            
            // Verify the property: Available = Quantity - active reservations
            var calculatedAvailable = scenario.InitialQuantity - scenario.ReservedQuantity;
            Assert.Equal(calculatedAvailable, retrievedType.Available);
        }
    }

    /// <summary>
    /// Property 6 (Multiple Ticket Types): Availability calculation for events with multiple ticket types
    /// </summary>
    [Fact]
    public async Task TicketAvailabilityCalculation_WorksForMultipleTicketTypes()
    {
        // Arrange - Create event with multiple ticket types
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

        var createRequest = new CreateEventRequest
        {
            Name = "Multi-Type Event",
            Description = "Event with multiple ticket types",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "VIP", Price = 200, Quantity = 20 },
                new CreateTicketTypeRequest { Name = "General", Price = 100, Quantity = 100 },
                new CreateTicketTypeRequest { Name = "Student", Price = 50, Quantity = 50 }
            }
        };

        var createdEvent = await _eventService.CreateEventAsync(createRequest, organizerId);
        
        var vipTicketType = createdEvent.TicketTypes.First(tt => tt.Name == "VIP");
        var generalTicketType = createdEvent.TicketTypes.First(tt => tt.Name == "General");
        var studentTicketType = createdEvent.TicketTypes.First(tt => tt.Name == "Student");

        // Simulate active reservations with real reservation rows (VIP 5, General 75, Student 0)
        _context.Reservations.AddRange(
            new Reservation
            {
                Id = Guid.NewGuid(), EventId = vipTicketType.EventId, TicketTypeId = vipTicketType.Id,
                Quantity = 5, ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Status = ReservationStatus.Active, CreatedAt = DateTime.UtcNow
            },
            new Reservation
            {
                Id = Guid.NewGuid(), EventId = generalTicketType.EventId, TicketTypeId = generalTicketType.Id,
                Quantity = 75, ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Status = ReservationStatus.Active, CreatedAt = DateTime.UtcNow
            });
        await _context.SaveChangesAsync();

        // Act
        var eventWithAvailability = await _eventService.GetEventByIdAsync(createdEvent.Id);

        // Assert - Each ticket type should have correct availability
        Assert.NotNull(eventWithAvailability);
        Assert.Equal(3, eventWithAvailability.TicketTypes.Count);

        var vipType = eventWithAvailability.TicketTypes.First(tt => tt.Name == "VIP");
        Assert.Equal(20, vipType.Quantity);
        Assert.Equal(15, vipType.Available); // 20 - 5 = 15

        var generalType = eventWithAvailability.TicketTypes.First(tt => tt.Name == "General");
        Assert.Equal(100, generalType.Quantity);
        Assert.Equal(25, generalType.Available); // 100 - 75 = 25

        var studentType = eventWithAvailability.TicketTypes.First(tt => tt.Name == "Student");
        Assert.Equal(50, studentType.Quantity);
        Assert.Equal(50, studentType.Available); // 50 - 0 = 50
    }

    #endregion

    #region Property 30: Event Creation Establishes Ownership

    /// <summary>
    /// Property 30: Event Creation Establishes Ownership
    /// For any event created by an organizador, the event SHALL be associated 
    /// with that organizador as the owner.
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Fact]
    public async Task EventCreation_EstablishesOwnership_WithCreatingOrganizador()
    {
        // Test with multiple organizers to verify the property holds universally
        var organizerCount = 5;

        for (int i = 0; i < organizerCount; i++)
        {
            // Arrange - Create a unique organizer
            var organizerId = Guid.NewGuid();
            var organizer = new User
            {
                Id = organizerId,
                Email = $"organizer{i}@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(organizer);
            await _context.SaveChangesAsync();

            // Act - Create an event
            var createRequest = new CreateEventRequest
            {
                Name = $"Event by Organizer {i}",
                Description = $"This event is created by organizer {i}",
                Date = DateTime.UtcNow.AddDays(30 + i),
                Location = $"Location {i}",
                ImageUrl = $"https://example.com/event{i}.jpg",
                TicketTypes = new List<CreateTicketTypeRequest>
                {
                    new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
                }
            };

            var createdEvent = await _eventService.CreateEventAsync(createRequest, organizerId);

            // Assert - Verify ownership is established
            Assert.NotNull(createdEvent);
            Assert.Equal(organizerId, createdEvent.OrganizerId);

            // Verify ownership persisted in database
            var eventFromDb = await _context.Events
                .Include(e => e.Organizer)
                .FirstOrDefaultAsync(e => e.Id == createdEvent.Id);

            Assert.NotNull(eventFromDb);
            Assert.Equal(organizerId, eventFromDb.OrganizerId);
            Assert.NotNull(eventFromDb.Organizer);
            Assert.Equal(organizer.Email, eventFromDb.Organizer.Email);
            Assert.Equal(UserRole.Organizador, eventFromDb.Organizer.Role);
        }
    }

    /// <summary>
    /// Property 30 (Edge Case): Multiple events by same organizer all have correct ownership
    /// </summary>
    [Fact]
    public async Task EventCreation_MultipleEventsBySameOrganizador_AllHaveCorrectOwnership()
    {
        // Arrange - Create one organizer
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "multi-event-organizer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);
        await _context.SaveChangesAsync();

        // Act - Create multiple events by the same organizer
        var eventIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            var createRequest = new CreateEventRequest
            {
                Name = $"Event {i} by Same Organizer",
                Description = $"Event number {i}",
                Date = DateTime.UtcNow.AddDays(30 + i),
                Location = $"Location {i}",
                ImageUrl = $"https://example.com/event{i}.jpg",
                TicketTypes = new List<CreateTicketTypeRequest>
                {
                    new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
                }
            };

            var createdEvent = await _eventService.CreateEventAsync(createRequest, organizerId);
            eventIds.Add(createdEvent.Id);
        }

        // Assert - All events should have the same organizer
        foreach (var eventId in eventIds)
        {
            var eventFromDb = await _context.Events.FindAsync(eventId);
            Assert.NotNull(eventFromDb);
            Assert.Equal(organizerId, eventFromDb.OrganizerId);
        }

        // Verify organizer has all events in navigation property
        var organizerFromDb = await _context.Users
            .Include(u => u.OrganizedEvents)
            .FirstOrDefaultAsync(u => u.Id == organizerId);

        Assert.NotNull(organizerFromDb);
        Assert.Equal(10, organizerFromDb.OrganizedEvents.Count);
        Assert.All(organizerFromDb.OrganizedEvents, e => Assert.Equal(organizerId, e.OrganizerId));
    }

    #endregion

    #region Property 31: Event Validation Rejects Invalid Data

    /// <summary>
    /// Property 31: Event Validation Rejects Invalid Data
    /// For any event creation request missing required fields (name, date, location, 
    /// ticket types, quantities, prices), the system SHALL reject the request 
    /// with a validation error.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Fact]
    public async Task EventValidation_RejectsMissingName()
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
        await _context.SaveChangesAsync();

        var invalidRequest = new CreateEventRequest
        {
            Name = "", // MISSING/EMPTY NAME
            Description = "Valid description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Valid Location",
            ImageUrl = "https://example.com/image.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _eventService.CreateEventAsync(invalidRequest, organizerId)
        );

        Assert.Contains("name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EventValidation_RejectsMissingLocation()
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
        await _context.SaveChangesAsync();

        var invalidRequest = new CreateEventRequest
        {
            Name = "Valid Event",
            Description = "Valid description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "", // MISSING/EMPTY LOCATION
            ImageUrl = "https://example.com/image.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _eventService.CreateEventAsync(invalidRequest, organizerId)
        );

        Assert.Contains("location", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EventValidation_RejectsPastDate()
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
        await _context.SaveChangesAsync();

        var invalidRequest = new CreateEventRequest
        {
            Name = "Valid Event",
            Description = "Valid description",
            Date = DateTime.UtcNow.AddDays(-30), // PAST DATE
            Location = "Valid Location",
            ImageUrl = "https://example.com/image.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _eventService.CreateEventAsync(invalidRequest, organizerId)
        );

        Assert.Contains("date", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EventValidation_RejectsMissingTicketTypes()
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
        await _context.SaveChangesAsync();

        var invalidRequest = new CreateEventRequest
        {
            Name = "Valid Event",
            Description = "Valid description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Valid Location",
            ImageUrl = "https://example.com/image.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>() // EMPTY TICKET TYPES
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _eventService.CreateEventAsync(invalidRequest, organizerId)
        );

        Assert.Contains("ticket type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EventValidation_RejectsInvalidTicketQuantity()
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
        await _context.SaveChangesAsync();

        var invalidRequest = new CreateEventRequest
        {
            Name = "Valid Event",
            Description = "Valid description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Valid Location",
            ImageUrl = "https://example.com/image.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 0 } // INVALID QUANTITY
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _eventService.CreateEventAsync(invalidRequest, organizerId)
        );

        Assert.Contains("quantity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EventValidation_RejectsNegativePrice()
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
        await _context.SaveChangesAsync();

        var invalidRequest = new CreateEventRequest
        {
            Name = "Valid Event",
            Description = "Valid description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Valid Location",
            ImageUrl = "https://example.com/image.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = -10, Quantity = 100 } // NEGATIVE PRICE
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _eventService.CreateEventAsync(invalidRequest, organizerId)
        );

        Assert.Contains("price", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Property 32: Non-Owner Modification Prevention

    /// <summary>
    /// Property 32: Non-Owner Modification Prevention
    /// For any event, modification attempts by users who are not the owner 
    /// (and not admins) SHALL be rejected with a forbidden error.
    /// **Validates: Requirements 10.7**
    /// </summary>
    [Fact]
    public async Task NonOwnerModification_IsRejected_ForOrganizadorRole()
    {
        // Arrange - Create two organizers
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var nonOwner = new User
        {
            Id = Guid.NewGuid(),
            Email = "non-owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, nonOwner);
        await _context.SaveChangesAsync();

        // Create event owned by first organizer
        var createRequest = new CreateEventRequest
        {
            Name = "Owner's Event",
            Description = "This event belongs to the owner",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await _eventService.CreateEventAsync(createRequest, owner.Id);

        // Act & Assert - Non-owner attempts to update the event
        var updateRequest = new UpdateEventRequest
        {
            Name = "Modified by Non-Owner",
            Description = "This should fail",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "New Location",
            ImageUrl = "https://example.com/new.jpg"
        };

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await _eventService.UpdateEventAsync(createdEvent.Id, updateRequest, nonOwner.Id, nonOwner.Role)
        );

        Assert.Contains("permission", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Verify event was NOT modified
        var unchangedEvent = await _context.Events.FindAsync(createdEvent.Id);
        Assert.NotNull(unchangedEvent);
        Assert.Equal("Owner's Event", unchangedEvent.Name);
        Assert.Equal(owner.Id, unchangedEvent.OrganizerId);
    }

    [Fact]
    public async Task NonOwnerDeletion_IsRejected_ForOrganizadorRole()
    {
        // Arrange - Create two organizers
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var nonOwner = new User
        {
            Id = Guid.NewGuid(),
            Email = "non-owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, nonOwner);
        await _context.SaveChangesAsync();

        // Create event owned by first organizer
        var createRequest = new CreateEventRequest
        {
            Name = "Owner's Event",
            Description = "This event belongs to the owner",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await _eventService.CreateEventAsync(createRequest, owner.Id);

        // Act & Assert - Non-owner attempts to delete the event
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await _eventService.DeleteEventAsync(createdEvent.Id, nonOwner.Id, nonOwner.Role)
        );

        // ED-001: the delete guard is now Admin-only and its message changed from
        // the owner-permission wording to the Admin-only wording (no "permission"
        // substring anymore) — assertion updated to the new contract.
        Assert.Contains("administrator", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Verify event was NOT deleted
        var stillExists = await _context.Events.FindAsync(createdEvent.Id);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task NonOwnerModification_IsRejected_ForStaffRole()
    {
        // Arrange - Create organizer and staff user
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "staff@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Staff,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, staffUser);
        await _context.SaveChangesAsync();

        // Create event owned by organizer
        var createRequest = new CreateEventRequest
        {
            Name = "Owner's Event",
            Description = "This event belongs to the organizer",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await _eventService.CreateEventAsync(createRequest, owner.Id);

        // Act & Assert - Staff user attempts to update the event
        var updateRequest = new UpdateEventRequest
        {
            Name = "Modified by Staff",
            Description = "This should fail",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "New Location",
            ImageUrl = "https://example.com/new.jpg"
        };

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await _eventService.UpdateEventAsync(createdEvent.Id, updateRequest, staffUser.Id, staffUser.Role)
        );

        Assert.Contains("permission", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OwnerModification_IsAllowed()
    {
        // Arrange - Create organizer
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(owner);
        await _context.SaveChangesAsync();

        // Create event
        var createRequest = new CreateEventRequest
        {
            Name = "Original Event",
            Description = "Original description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Original Location",
            ImageUrl = "https://example.com/original.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await _eventService.CreateEventAsync(createRequest, owner.Id);

        // Act - Owner updates their own event
        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated Event",
            Description = "Updated description",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location",
            ImageUrl = "https://example.com/updated.jpg"
        };

        var updatedEvent = await _eventService.UpdateEventAsync(createdEvent.Id, updateRequest, owner.Id, owner.Role);

        // Assert - Update should succeed
        Assert.NotNull(updatedEvent);
        Assert.Equal("Updated Event", updatedEvent.Name);
        Assert.Equal("Updated description", updatedEvent.Description);
        Assert.Equal("Updated Location", updatedEvent.Location);
    }

    [Fact]
    public async Task AdminModification_IsAllowed_ForAnyEvent()
    {
        // Arrange - Create organizer and admin
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, admin);
        await _context.SaveChangesAsync();

        // Create event owned by organizer
        var createRequest = new CreateEventRequest
        {
            Name = "Owner's Event",
            Description = "Original description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Original Location",
            ImageUrl = "https://example.com/original.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await _eventService.CreateEventAsync(createRequest, owner.Id);

        // Act - Admin updates someone else's event (should be allowed)
        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated by Admin",
            Description = "Admin made changes",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "New Location",
            ImageUrl = "https://example.com/admin.jpg"
        };

        var updatedEvent = await _eventService.UpdateEventAsync(createdEvent.Id, updateRequest, admin.Id, admin.Role);

        // Assert - Admin update should succeed
        Assert.NotNull(updatedEvent);
        Assert.Equal("Updated by Admin", updatedEvent.Name);
        Assert.Equal("Admin made changes", updatedEvent.Description);
        
        // Event should still belong to original owner
        Assert.Equal(owner.Id, updatedEvent.OrganizerId);
    }

    [Fact]
    public async Task AdminDeletion_IsAllowed_ForAnyEvent()
    {
        // Arrange - Create organizer and admin
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, admin);
        await _context.SaveChangesAsync();

        // Create event owned by organizer
        var createRequest = new CreateEventRequest
        {
            Name = "Owner's Event",
            Description = "This event will be deleted by admin",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await _eventService.CreateEventAsync(createRequest, owner.Id);
        var eventId = createdEvent.Id;

        // Act - Admin deletes someone else's event (should be allowed)
        await _eventService.DeleteEventAsync(eventId, admin.Id, admin.Role);

        // Assert - Event should be deleted
        var deletedEvent = await _context.Events.FindAsync(eventId);
        Assert.Null(deletedEvent);
    }

    #endregion
}
