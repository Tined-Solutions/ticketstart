using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Net;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for EventService
/// Validates Requirements 2.1, 2.4, 2.5, 10.1, 10.3, 10.4, 10.5, 10.6, 10.7
/// </summary>
public class EventServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EventService _eventService;
    private readonly ILogger<EventService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Mock<IAmazonS3> _s3ClientMock;
    private readonly Mock<IEventNotificationQueue> _mockNotificationQueue;

    public EventServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _logger = new TestLogger<EventService>();
        
        // Mock configuration
        var configurationData = new Dictionary<string, string?>
        {
            { "CloudflareR2:BucketName", "test-bucket" },
            { "CloudflareR2:PublicUrl", "https://test.r2.dev" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();
        
        // Mock S3 client
        _mockNotificationQueue = new Mock<IEventNotificationQueue>();
        _s3ClientMock = new Mock<IAmazonS3>();
        
        _eventService = new EventService(_context, _logger, _configuration, _s3ClientMock.Object, _mockNotificationQueue.Object, TimeProvider.System,
            Options.Create(new HideExpiredEventsOptions()));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region CreateEventAsync Tests

    [Fact]
    public async Task CreateEventAsync_WithValidData_CreatesEventAndAssignsOwnership()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/image.jpg",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new() { Name = "General", Price = 100m, Quantity = 50 }
            }
        };

        // Act
        var result = await _eventService.CreateEventAsync(request, organizerId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Description, result.Description);
        Assert.Equal(request.Location, result.Location);
        Assert.Equal(organizerId, result.OrganizerId);
        Assert.Single(result.TicketTypes);
        Assert.Equal("General", result.TicketTypes.First().Name);
    }

    [Fact]
    public async Task CreateEventAsync_WithoutName_ThrowsArgumentException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Name = "",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new() { Name = "General", Price = 100m, Quantity = 50 }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _eventService.CreateEventAsync(request, organizerId));
    }

    [Fact]
    public async Task CreateEventAsync_WithoutLocation_ThrowsArgumentException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new() { Name = "General", Price = 100m, Quantity = 50 }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _eventService.CreateEventAsync(request, organizerId));
    }

    [Fact]
    public async Task CreateEventAsync_WithPastDate_ThrowsArgumentException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(-1),
            Location = "Test Location",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new() { Name = "General", Price = 100m, Quantity = 50 }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _eventService.CreateEventAsync(request, organizerId));
    }

    [Fact]
    public async Task CreateEventAsync_WithoutTicketTypes_ThrowsArgumentException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            TicketTypes = new List<CreateTicketTypeRequest>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _eventService.CreateEventAsync(request, organizerId));
    }

    [Fact]
    public async Task CreateEventAsync_WithNegativePrice_ThrowsArgumentException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new() { Name = "General", Price = -10m, Quantity = 50 }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _eventService.CreateEventAsync(request, organizerId));
    }

    [Fact]
    public async Task CreateEventAsync_WithZeroQuantity_ThrowsArgumentException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new() { Name = "General", Price = 100m, Quantity = 0 }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _eventService.CreateEventAsync(request, organizerId));
    }

    #endregion

    #region GetEventByIdAsync Tests

    [Fact]
    public async Task GetEventByIdAsync_WithExistingEvent_ReturnsEventWithAvailability()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/image.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100m,
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventEntity.Id, result.Id);
        Assert.Equal(eventEntity.Name, result.Name);
        Assert.Single(result.TicketTypes);
        Assert.Equal(50, result.TicketTypes.First().Available);
    }

    [Fact]
    public async Task GetEventByIdAsync_CalculatesAvailabilityCorrectly()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100m,
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        };

        // Add 10 sold tickets
        for (int i = 0; i < 10; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = $"test{i}@example.com",
                PurchaserDNI = $"1234567{i}",
                QRCodeData = $"qr-{i}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(40, result.TicketTypes.First().Available); // 50 - 10 sold tickets = 40
    }

    [Fact]
    public async Task GetEventByIdAsync_WithNonExistentEvent_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _eventService.GetEventByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetAllPublishedEventsAsync Tests

    [Fact]
    public async Task GetAllPublishedEventsAsync_ReturnsAllEvents()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        
        for (int i = 0; i < 3; i++)
        {
            var eventEntity = new Event
            {
                Id = Guid.NewGuid(),
                Name = $"Event {i}",
                Description = $"Description {i}",
                Date = DateTime.UtcNow.AddDays(30 + i),
                Location = $"Location {i}",
                OrganizerId = organizerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var ticketType = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                Name = "General",
                Price = 100m,
                Quantity = 50,
                CreatedAt = DateTime.UtcNow
            };

            _context.Events.Add(eventEntity);
            _context.TicketTypes.Add(ticketType);
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetAllPublishedEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
    }

    [Fact]
    public async Task GetAllPublishedEventsAsync_WithNoEvents_ReturnsEmptyList()
    {
        // Act
        var result = await _eventService.GetAllPublishedEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region UpdateEventAsync Tests

    [Fact]
    public async Task UpdateEventAsync_ByOwner_UpdatesEvent()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Description = "Original Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Original Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location",
            ImageUrl = "https://example.com/new-image.jpg"
        };

        // Act
        var result = await _eventService.UpdateEventAsync(eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal("Updated Location", result.Location);
    }

    [Fact]
    public async Task UpdateEventAsync_ByAdmin_UpdatesEvent()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Description = "Original Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Original Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated by Admin",
            Description = "Updated Description",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location",
            ImageUrl = "https://example.com/new-image.jpg"
        };

        // Act
        var result = await _eventService.UpdateEventAsync(eventEntity.Id, updateRequest, adminId, UserRole.Admin);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated by Admin", result.Name);
    }

    [Fact]
    public async Task UpdateEventAsync_WhenImageUrlOmitted_PreservesExistingImage()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Description = "Original Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Original Location",
            ImageUrl = "https://test.r2.dev/events/original.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // ImageUrl deliberately omitted — a plain text edit must not wipe the image
        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location"
        };

        // Act
        var result = await _eventService.UpdateEventAsync(eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://test.r2.dev/events/original.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task UpdateEventAsync_WhenImageUrlExplicitlyEmpty_ClearsImage()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Description = "Original Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Original Location",
            ImageUrl = "https://test.r2.dev/events/original.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Explicit empty string is the "remove image" signal
        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location",
            ImageUrl = string.Empty
        };

        // Act
        var result = await _eventService.UpdateEventAsync(eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.ImageUrl);
    }

    [Fact]
    public async Task UpdateEventAsync_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Description = "Original Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Original Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _eventService.UpdateEventAsync(eventEntity.Id, updateRequest, otherUserId, UserRole.Organizador));
    }

    [Fact]
    public async Task UpdateEventAsync_WithNonExistentEvent_ThrowsKeyNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateEventRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location"
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _eventService.UpdateEventAsync(nonExistentId, updateRequest, userId, UserRole.Organizador));
    }

    [Fact]
    public async Task UpdateEventAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Description = "Original Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Original Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "",
            Description = "Updated Description",
            Date = DateTime.UtcNow.AddDays(60),
            Location = "Updated Location"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.UpdateEventAsync(eventEntity.Id, updateRequest, organizerId, UserRole.Organizador));
    }

    #endregion

    #region DeleteEventAsync Tests

    [Fact]
    public async Task DeleteEventAsync_ByOwner_DeletesEvent()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        await _eventService.DeleteEventAsync(eventEntity.Id, organizerId, UserRole.Organizador);

        // Assert
        var deletedEvent = await _context.Events.FindAsync(eventEntity.Id);
        Assert.Null(deletedEvent);
    }

    [Fact]
    public async Task DeleteEventAsync_ByAdmin_DeletesEvent()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        await _eventService.DeleteEventAsync(eventEntity.Id, adminId, UserRole.Admin);

        // Assert
        var deletedEvent = await _context.Events.FindAsync(eventEntity.Id);
        Assert.Null(deletedEvent);
    }

    [Fact]
    public async Task DeleteEventAsync_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _eventService.DeleteEventAsync(eventEntity.Id, otherUserId, UserRole.Organizador));
    }

    [Fact]
    public async Task DeleteEventAsync_WithNonExistentEvent_ThrowsKeyNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _eventService.DeleteEventAsync(nonExistentId, userId, UserRole.Organizador));
    }

    [Fact]
    public async Task DeleteEventAsync_WithImageUrl_DeletesEventSuccessfully()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://test.r2.dev/events/test-image.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        await _eventService.DeleteEventAsync(eventEntity.Id, organizerId, UserRole.Organizador);

        // Assert
        var deletedEvent = await _context.Events.FindAsync(eventEntity.Id);
        Assert.Null(deletedEvent);
        // Note: Image deletion is handled gracefully - even if it fails, event deletion succeeds
    }

    [Fact]
    public async Task DeleteEventAsync_WithImageUrl_DeletesImageFromR2()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://test.r2.dev/events/test-image.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        DeleteObjectRequest? capturedRequest = null;
        _s3ClientMock
            .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
            .Callback<DeleteObjectRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });

        // Act
        await _eventService.DeleteEventAsync(eventEntity.Id, organizerId, UserRole.Organizador);

        // Assert
        var deletedEvent = await _context.Events.FindAsync(eventEntity.Id);
        Assert.Null(deletedEvent);
        Assert.NotNull(capturedRequest);
        Assert.Equal("test-bucket", capturedRequest.BucketName);
        Assert.Equal("events/test-image.jpg", capturedRequest.Key);
        _s3ClientMock.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteEventAsync_WhenImageDeletionFails_StillDeletesEvent()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://test.r2.dev/events/test-image.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        _s3ClientMock
            .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("Delete failed"));

        // Act
        await _eventService.DeleteEventAsync(eventEntity.Id, organizerId, UserRole.Organizador);

        // Assert
        var deletedEvent = await _context.Events.FindAsync(eventEntity.Id);
        Assert.Null(deletedEvent);
        _s3ClientMock.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default), Times.Once);
    }

    #endregion

    #region B3.6: Availability computed from sold tickets + active reservations

    [Fact]
    public async Task GetEventByIdAsync_ComputesAvailability_FromActiveReservations()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateEventWithTickets(10);

        _context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 3,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(result);
        var tt = Assert.Single(result.TicketTypes);
        Assert.Equal(7, tt.Available); // 10 - 3 active reservation = 7
    }

    [Fact]
    public async Task GetEventByIdAsync_CountsSoldTickets_InAvailability()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateEventWithTickets(10);

        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), TicketTypeId = ticketType.Id,
            EventId = eventEntity.Id, PurchaserEmail = "a@b.com",
            PurchaserDNI = "111", QRCodeData = "qr1", CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), TicketTypeId = ticketType.Id,
            EventId = eventEntity.Id, PurchaserEmail = "c@d.com",
            PurchaserDNI = "222", QRCodeData = "qr2", CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert — sold tickets count against availability
        Assert.NotNull(result);
        var tt = Assert.Single(result.TicketTypes);
        Assert.Equal(8, tt.Available); // 10 - 2 sold tickets = 8
    }

    [Fact]
    public async Task GetAllPublishedEventsAsync_ComputesAvailability_FromActiveReservations()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateEventWithTickets(20);

        _context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 8,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var results = await _eventService.GetAllPublishedEventsAsync();
        var result = results.FirstOrDefault(e => e.Id == eventEntity.Id);

        // Assert
        Assert.NotNull(result);
        var tt = Assert.Single(result.TicketTypes);
        Assert.Equal(12, tt.Available); // 20 - 8 active reservation = 12
    }

    private async Task<(Event, TicketType)> CreateEventWithTickets(int quantity)
    {
        var organizerId = Guid.NewGuid();
        var user = new User
        {
            Id = organizerId, Name = "Org", Email = "org@test.com",
            PasswordHash = "h", Role = UserRole.Organizador, CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var evt = new Event
        {
            Id = Guid.NewGuid(), Name = "Test", Description = "D",
            Date = DateTime.UtcNow.AddDays(1), Location = "L",
            OrganizerId = organizerId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(evt);

        var tt = new TicketType
        {
            Id = Guid.NewGuid(), EventId = evt.Id, Name = "GA",
            Price = 50m, Quantity = quantity, CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(tt);
        await _context.SaveChangesAsync();

        return (evt, tt);
    }

    #endregion

    #region EHE-002/003 — Expired-event filtering (catalog + detail)

    /// <summary>
    /// Builds an EventService over the shared InMemory context with a frozen clock
    /// and the HideExpiredEvents flag bound to the given options (ADR-3/ADR-4).
    /// </summary>
    private EventService CreateServiceWithClockAndOptions(TimeProvider clock, HideExpiredEventsOptions options) => new(
        _context, _logger, _configuration, _s3ClientMock.Object, _mockNotificationQueue.Object, clock,
        Options.Create(options));

    private static Event CreateEventEntity(Guid organizerId, string name, DateTime date) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Description",
        Date = date,
        Location = "Location",
        OrganizerId = organizerId,
        CreatedAt = date,
        UpdatedAt = date
    };

    [Fact]
    public async Task GetAllPublished_FlagEnabled_ExcludesExpired()
    {
        // EHE-002: flag ON — the public list must not surface expired events.
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = true });

        var organizerId = Guid.NewGuid();
        var future = CreateEventEntity(organizerId, "Future", fake.GetUtcNow().UtcDateTime.AddDays(1));
        var past = CreateEventEntity(organizerId, "Past", fake.GetUtcNow().UtcDateTime.AddDays(-1));
        _context.Events.AddRange(future, past);
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetAllPublishedEventsAsync();

        // Assert — only the future-dated event is visible
        var ids = result.Select(e => e.Id).ToList();
        Assert.Contains(future.Id, ids);
        Assert.DoesNotContain(past.Id, ids);
    }

    [Fact]
    public async Task GetAllPublished_AllExpired_Empty()
    {
        // EHE-002: all events expired → empty list.
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = true });

        var organizerId = Guid.NewGuid();
        _context.Events.Add(CreateEventEntity(organizerId, "Past A", fake.GetUtcNow().UtcDateTime.AddHours(-2)));
        _context.Events.Add(CreateEventEntity(organizerId, "Past B", fake.GetUtcNow().UtcDateTime.AddDays(-10)));
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetAllPublishedEventsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllPublished_MixOrderIndependent()
    {
        // EHE-002: interleaved past/future dates — only future-dated events come
        // back regardless of insertion order.
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = true });

        var now = fake.GetUtcNow().UtcDateTime;
        var organizerId = Guid.NewGuid();
        var a = CreateEventEntity(organizerId, "A past", now.AddDays(-5));
        var b = CreateEventEntity(organizerId, "B future", now.AddDays(1));
        var c = CreateEventEntity(organizerId, "C past", now.AddDays(-1));
        var d = CreateEventEntity(organizerId, "D future", now.AddDays(10));
        var e = CreateEventEntity(organizerId, "E past", now.AddHours(-3));
        _context.Events.AddRange(a, c, e, b, d); // deliberately interleaved insertion order
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetAllPublishedEventsAsync();

        // Assert — set equality, order-independent
        var ids = result.Select(x => x.Id).ToHashSet();
        Assert.Equal(2, ids.Count);
        Assert.Contains(b.Id, ids);
        Assert.Contains(d.Id, ids);
        Assert.DoesNotContain(a.Id, ids);
        Assert.DoesNotContain(c.Id, ids);
        Assert.DoesNotContain(e.Id, ids);
    }

    [Fact]
    public async Task GetAllPublished_FlagDisabled_ReturnsExpired()
    {
        // EHE-009 runtime rollback (catalog side): with HideExpiredEvents.Enabled=false
        // the Where filter is a no-op and the public list returns ALL events, expired
        // included — identical to pre-change behavior.
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = false });

        var organizerId = Guid.NewGuid();
        var future = CreateEventEntity(organizerId, "Future", fake.GetUtcNow().UtcDateTime.AddDays(1));
        var past = CreateEventEntity(organizerId, "Past", fake.GetUtcNow().UtcDateTime.AddDays(-1));
        _context.Events.AddRange(future, past);
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetAllPublishedEventsAsync();

        // Assert — both events visible: the filter must not apply when the flag is off
        var ids = result.Select(e => e.Id).ToHashSet();
        Assert.Equal(2, ids.Count);
        Assert.Contains(future.Id, ids);
        Assert.Contains(past.Id, ids);
    }

    [Fact]
    public async Task GetEventById_Public_Expired_Null()
    {
        // EHE-003: public detail (default includeExpired=false) hides expired
        // events → null (the controller maps that to 404).
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = true });

        var expired = CreateEventEntity(Guid.NewGuid(), "Expired", fake.GetUtcNow().UtcDateTime.AddDays(-1));
        _context.Events.Add(expired);
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetEventByIdAsync(expired.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetEventById_ManagementIncludeExpired_200()
    {
        // EHE-003: the management variant (includeExpired:true) returns the event
        // regardless of expiry — this is the call behind GET /api/events/{id}/manage.
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = true });

        var expired = CreateEventEntity(Guid.NewGuid(), "Expired", fake.GetUtcNow().UtcDateTime.AddDays(-1));
        _context.Events.Add(expired);
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetEventByIdAsync(expired.Id, includeExpired: true);

        // Assert — non-null, full detail
        Assert.NotNull(result);
        Assert.Equal(expired.Id, result.Id);
        Assert.Equal("Expired", result.Name);
    }

    #endregion

    #region GetScannableEventsAsync Tests

    [Fact]
    public async Task GetScannableEvents_IncludesFutureAndRecentlyEnded_ExcludesOlderThan24h()
    {
        // Scanner chooser: future events plus events ended within the 24h QR
        // validation window (TicketService.ValidationWindowHours) are listed;
        // anything older cannot validate QR codes and is filtered out.
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = true });

        var now = fake.GetUtcNow().UtcDateTime;
        var organizerId = Guid.NewGuid();
        var future = CreateEventEntity(organizerId, "Future", now.AddDays(1));
        var recent = CreateEventEntity(organizerId, "Recent", now.AddHours(-2));
        var old = CreateEventEntity(organizerId, "Old", now.AddDays(-2));
        _context.Events.AddRange(future, recent, old);
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetScannableEventsAsync();

        // Assert — only future + recently-ended are scannable
        var ids = result.Select(e => e.Id).ToHashSet();
        Assert.Equal(2, ids.Count);
        Assert.Contains(future.Id, ids);
        Assert.Contains(recent.Id, ids);
        Assert.DoesNotContain(old.Id, ids);
    }

    [Fact]
    public async Task GetScannableEvents_ExcludesEventEndedExactlyAtWindowBoundary()
    {
        // Borderline: an event that ended exactly 24h ago sits at the edge of the
        // window (Date > cutoff is strict). This mirrors QR validation, which also
        // rejects a timestamp equal to Date + 24h.
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = true });

        var now = fake.GetUtcNow().UtcDateTime;
        var boundary = CreateEventEntity(Guid.NewGuid(), "Boundary", now.AddHours(-TicketService.ValidationWindowHours));
        _context.Events.Add(boundary);
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetScannableEventsAsync();

        // Assert
        Assert.DoesNotContain(result, e => e.Id == boundary.Id);
    }

    [Fact]
    public async Task GetScannableEvents_OrdersFutureAscendingThenEndedDescending()
    {
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = true });

        var now = fake.GetUtcNow().UtcDateTime;
        var organizerId = Guid.NewGuid();
        var futureLater = CreateEventEntity(organizerId, "Future Later", now.AddDays(5));
        var futureSoon = CreateEventEntity(organizerId, "Future Soon", now.AddDays(1));
        var endedRecent = CreateEventEntity(organizerId, "Ended Recent", now.AddHours(-2));
        var endedOlder = CreateEventEntity(organizerId, "Ended Older", now.AddHours(-10));
        _context.Events.AddRange(endedOlder, futureLater, endedRecent, futureSoon); // interleaved
        await _context.SaveChangesAsync();

        // Act
        var result = (await service.GetScannableEventsAsync()).ToList();

        // Assert — future events ascending first, then ended events descending (most recent first)
        var order = result.Select(e => e.Id).ToList();
        Assert.Equal(new[] { futureSoon.Id, futureLater.Id, endedRecent.Id, endedOlder.Id }, order);
    }

    [Fact]
    public async Task GetScannableEvents_IndependentOfHideExpiredFlag()
    {
        // The scanner window is a hard technical rule, not a product toggle: even
        // with HideExpiredEvents.Enabled=false the 24h scannable window still applies.
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateServiceWithClockAndOptions(fake, new HideExpiredEventsOptions { Enabled = false });

        var now = fake.GetUtcNow().UtcDateTime;
        var organizerId = Guid.NewGuid();
        var future = CreateEventEntity(organizerId, "Future", now.AddDays(1));
        var recent = CreateEventEntity(organizerId, "Recent", now.AddHours(-2));
        var old = CreateEventEntity(organizerId, "Old", now.AddDays(-2));
        _context.Events.AddRange(future, recent, old);
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetScannableEventsAsync();

        // Assert — the window applies regardless of the flag
        var ids = result.Select(e => e.Id).ToHashSet();
        Assert.Equal(2, ids.Count);
        Assert.Contains(future.Id, ids);
        Assert.Contains(recent.Id, ids);
        Assert.DoesNotContain(old.Id, ids);
    }

    #endregion
}

/// <summary>
/// Simple test logger implementation for unit tests
/// </summary>
internal class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}
