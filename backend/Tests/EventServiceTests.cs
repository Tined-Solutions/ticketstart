using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Amazon.S3;
using Amazon.S3.Model;
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
        _s3ClientMock = new Mock<IAmazonS3>();
        
        _eventService = new EventService(_context, _logger, _configuration, _s3ClientMock.Object);
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
        Assert.Equal(40, result.TicketTypes.First().Available); // 50 - 10 = 40
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
