using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;
using Moq;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;
using GenStatic = FsCheck.Fluent.Gen;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for image storage functionality
/// Validates Requirements 3.2, 3.4, 3.6
/// </summary>
public class ImageStoragePropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventService> _logger;

    public ImageStoragePropertyTests()
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

        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<EventService>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Property 7: Image ID Uniqueness

    /// <summary>
    /// Property 7: Image ID Uniqueness
    /// For any set of uploaded images, all generated image identifiers SHALL be unique.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public async Task ImageUpload_GeneratesUniqueIdentifiers_ForMultipleUploads()
    {
        // Arrange - Mock S3 client to capture uploaded object keys
        var uploadedKeys = new List<string>();
        var mockS3Client = new Mock<IR2StorageClient>();
        
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Stream, string, CancellationToken>((bucket, key, stream, contentType, ct) =>
            {
                uploadedKeys.Add(key);
            })
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        // Test with multiple image uploads to verify uniqueness
        var imageUploadCount = 50;
        var uploadedUrls = new List<string>();

        // Act - Upload multiple images
        for (int i = 0; i < imageUploadCount; i++)
        {
            var imageContent = $"fake-image-content-{i}";
            var imageStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(imageContent));
            
            var imageUrl = await eventService.UploadEventImageAsync(
                imageStream, 
                $"test-image-{i}.jpg", 
                "image/jpeg"
            );

            uploadedUrls.Add(imageUrl);
        }

        // Assert - All URLs should be unique
        var uniqueUrls = uploadedUrls.Distinct().ToList();
        Assert.Equal(imageUploadCount, uniqueUrls.Count);
        Assert.Equal(imageUploadCount, uploadedUrls.Count);

        // Verify all generated keys are unique
        var uniqueKeys = uploadedKeys.Distinct().ToList();
        Assert.Equal(imageUploadCount, uniqueKeys.Count);
        Assert.Equal(imageUploadCount, uploadedKeys.Count);

        // Verify all keys follow the expected pattern: events/{guid}.{extension}
        foreach (var key in uploadedKeys)
        {
            Assert.StartsWith("events/", key);
            Assert.Matches(@"^events/[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\.(jpg|png|webp)$", key);
        }
    }

    /// <summary>
    /// Property 7 (Edge Case): Concurrent uploads generate unique identifiers
    /// </summary>
    [Fact]
    public async Task ImageUpload_GeneratesUniqueIdentifiers_ForConcurrentUploads()
    {
        // Arrange - Mock S3 client to capture uploaded object keys
        var uploadedKeys = new System.Collections.Concurrent.ConcurrentBag<string>();
        var mockS3Client = new Mock<IR2StorageClient>();
        
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Stream, string, CancellationToken>((bucket, key, stream, contentType, ct) =>
            {
                uploadedKeys.Add(key);
            })
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        // Act - Upload images concurrently
        var concurrentUploadCount = 20;
        var uploadTasks = new List<Task<string>>();

        for (int i = 0; i < concurrentUploadCount; i++)
        {
            var imageIndex = i;
            var task = Task.Run(async () =>
            {
                var imageContent = $"concurrent-image-{imageIndex}";
                var imageStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(imageContent));
                
                return await eventService.UploadEventImageAsync(
                    imageStream, 
                    $"concurrent-{imageIndex}.jpg", 
                    "image/jpeg"
                );
            });
            
            uploadTasks.Add(task);
        }

        var uploadedUrls = await Task.WhenAll(uploadTasks);

        // Assert - All URLs should be unique despite concurrent uploads
        var uniqueUrls = uploadedUrls.Distinct().ToList();
        Assert.Equal(concurrentUploadCount, uniqueUrls.Count);

        // Verify all generated keys are unique
        var uniqueKeys = uploadedKeys.Distinct().ToList();
        Assert.Equal(concurrentUploadCount, uniqueKeys.Count);
    }

    /// <summary>
    /// Property 7 (Edge Case): Same filename multiple times generates unique identifiers
    /// </summary>
    [Fact]
    public async Task ImageUpload_GeneratesUniqueIdentifiers_ForSameFilename()
    {
        // Arrange - Mock S3 client
        var uploadedKeys = new List<string>();
        var mockS3Client = new Mock<IR2StorageClient>();
        
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Stream, string, CancellationToken>((bucket, key, stream, contentType, ct) =>
            {
                uploadedKeys.Add(key);
            })
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        // Act - Upload the same filename multiple times
        var sameFilename = "duplicate-name.jpg";
        var uploadCount = 10;
        var uploadedUrls = new List<string>();

        for (int i = 0; i < uploadCount; i++)
        {
            var imageContent = $"content-{i}"; // Different content, same filename
            var imageStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(imageContent));
            
            var imageUrl = await eventService.UploadEventImageAsync(
                imageStream, 
                sameFilename, 
                "image/jpeg"
            );

            uploadedUrls.Add(imageUrl);
        }

        // Assert - Even with the same filename, all identifiers should be unique
        var uniqueUrls = uploadedUrls.Distinct().ToList();
        Assert.Equal(uploadCount, uniqueUrls.Count);

        var uniqueKeys = uploadedKeys.Distinct().ToList();
        Assert.Equal(uploadCount, uniqueKeys.Count);
    }

    #endregion

    #region Property 8: Invalid Image File Rejection

    /// <summary>
    /// Property 8: Invalid Image File Rejection
    /// For any file that does not meet image type or size requirements, 
    /// the upload SHALL be rejected with a validation error.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Theory]
    [InlineData("application/pdf", "document.pdf")]
    [InlineData("text/plain", "file.txt")]
    [InlineData("application/json", "data.json")]
    [InlineData("video/mp4", "video.mp4")]
    [InlineData("audio/mpeg", "audio.mp3")]
    [InlineData("application/zip", "archive.zip")]
    [InlineData("image/gif", "animated.gif")]
    [InlineData("image/bmp", "bitmap.bmp")]
    [InlineData("image/svg+xml", "vector.svg")]
    public async Task ImageUpload_RejectsInvalidFileTypes(string contentType, string fileName)
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        var fileContent = "fake-file-content";
        var fileStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fileContent));

        // Act & Assert - Invalid file type should throw ArgumentException
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await eventService.UploadEventImageAsync(fileStream, fileName, contentType)
        );

        Assert.Contains("Invalid image type", exception.Message);
        Assert.Contains(contentType, exception.Message);

        // Verify S3 was never called
        mockS3Client.Verify(
            x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>
    /// Property 8 (Valid Types): Accepts valid image types
    /// </summary>
    [Theory]
    [InlineData("image/jpeg", "photo.jpg")]
    [InlineData("image/png", "graphic.png")]
    [InlineData("image/webp", "modern.webp")]
    [InlineData("IMAGE/JPEG", "uppercase.jpg")] // Case insensitive
    [InlineData("Image/Png", "mixedcase.png")] // Case insensitive
    public async Task ImageUpload_AcceptsValidFileTypes(string contentType, string fileName)
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        var imageContent = "valid-image-content";
        var imageStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(imageContent));

        // Act - Should not throw
        var imageUrl = await eventService.UploadEventImageAsync(imageStream, fileName, contentType);

        // Assert
        Assert.NotNull(imageUrl);
        Assert.Contains("https://pub-test.r2.dev/events/", imageUrl);

        // Verify S3 was called once
        mockS3Client.Verify(
            x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    /// <summary>
    /// Property 8 (Size Limit): Rejects files exceeding 5MB size limit
    /// </summary>
    [Fact]
    public async Task ImageUpload_RejectsFilesExceedingSizeLimit()
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        // Create a stream larger than 5MB
        var oversizedContent = new byte[6 * 1024 * 1024]; // 6MB
        var oversizedStream = new MemoryStream(oversizedContent);

        // Act & Assert - Oversized file should throw ArgumentException
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await eventService.UploadEventImageAsync(oversizedStream, "large.jpg", "image/jpeg")
        );

        Assert.Contains("Image size exceeds maximum allowed size", exception.Message);
        Assert.Contains("5MB", exception.Message);

        // Verify S3 was never called
        mockS3Client.Verify(
            x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>
    /// Property 8 (Size Limit): Accepts files at or below 5MB size limit
    /// </summary>
    [Theory]
    [InlineData(1024)] // 1KB
    [InlineData(1024 * 1024)] // 1MB
    [InlineData(3 * 1024 * 1024)] // 3MB
    [InlineData(5 * 1024 * 1024)] // Exactly 5MB (boundary test)
    public async Task ImageUpload_AcceptsFilesWithinSizeLimit(int fileSize)
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        var validContent = new byte[fileSize];
        var validStream = new MemoryStream(validContent);

        // Act - Should not throw
        var imageUrl = await eventService.UploadEventImageAsync(validStream, "valid.jpg", "image/jpeg");

        // Assert
        Assert.NotNull(imageUrl);
        Assert.Contains("https://pub-test.r2.dev/events/", imageUrl);

        // Verify S3 was called once
        mockS3Client.Verify(
            x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    /// <summary>
    /// Property 8 (Edge Case): Rejects empty or null content type
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ImageUpload_RejectsEmptyOrNullContentType(string? contentType)
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        var imageContent = "some-content";
        var imageStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(imageContent));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await eventService.UploadEventImageAsync(imageStream, "test.jpg", contentType!)
        );

        Assert.Contains("Invalid image type", exception.Message);

        // Verify S3 was never called
        mockS3Client.Verify(
            x => x.PutObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    #endregion

    #region Property 9: Event Deletion Removes Associated Images

    /// <summary>
    /// Property 9: Event Deletion Removes Associated Images
    /// For any event with an associated image, deleting the event SHALL remove 
    /// the image from R2 storage.
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Fact]
    public async Task EventDeletion_RemovesAssociatedImage_FromR2Storage()
    {
        // Arrange - Track S3 delete operations
        var deletedKeys = new List<string>();
        var mockS3Client = new Mock<IR2StorageClient>();
        
        mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((bucket, key, ct) =>
            {
                deletedKeys.Add(key);
            })
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        // Create organizer and event with image
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

        var imageUrl = "https://pub-test.r2.dev/events/12345678-1234-1234-1234-123456789abc.jpg";
        var createRequest = new CreateEventRequest
        {
            Name = "Event with Image",
            Description = "This event has an associated image",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = imageUrl,
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await eventService.CreateEventAsync(createRequest, organizerId);
        Assert.Equal(imageUrl, createdEvent.ImageUrl);

        // Act - Delete the event
        await eventService.DeleteEventAsync(createdEvent.Id, Guid.NewGuid(), UserRole.Admin);

        // Assert - Event should be deleted from database
        var deletedEvent = await _context.Events.FindAsync(createdEvent.Id);
        Assert.Null(deletedEvent);

        // Assert - Image should be deleted from R2
        Assert.Single(deletedKeys);
        Assert.Equal("events/12345678-1234-1234-1234-123456789abc.jpg", deletedKeys[0]);

        mockS3Client.Verify(
            x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    /// <summary>
    /// Property 9 (Multiple Events): Deleting multiple events removes their images
    /// </summary>
    [Fact]
    public async Task EventDeletion_RemovesMultipleAssociatedImages()
    {
        // Arrange
        var deletedKeys = new List<string>();
        var mockS3Client = new Mock<IR2StorageClient>();
        
        mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((bucket, key, ct) =>
            {
                deletedKeys.Add(key);
            })
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        // Create organizer
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

        // Create multiple events with different images
        var eventCount = 5;
        var createdEvents = new List<Event>();
        var expectedDeletedKeys = new List<string>();

        for (int i = 0; i < eventCount; i++)
        {
            var imageId = Guid.NewGuid();
            var imageUrl = $"https://pub-test.r2.dev/events/{imageId}.jpg";
            var expectedKey = $"events/{imageId}.jpg";
            expectedDeletedKeys.Add(expectedKey);

            var createRequest = new CreateEventRequest
            {
                Name = $"Event {i}",
                Description = $"Event with image {i}",
                Date = DateTime.UtcNow.AddDays(30 + i),
                Location = $"Location {i}",
                ImageUrl = imageUrl,
                TicketTypes = new List<CreateTicketTypeRequest>
                {
                    new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
                }
            };

            var createdEvent = await eventService.CreateEventAsync(createRequest, organizerId);
            createdEvents.Add(createdEvent);
        }

        // Act - Delete all events
        foreach (var evt in createdEvents)
        {
            await eventService.DeleteEventAsync(evt.Id, Guid.NewGuid(), UserRole.Admin);
        }

        // Assert - All events should be deleted
        foreach (var evt in createdEvents)
        {
            var deletedEvent = await _context.Events.FindAsync(evt.Id);
            Assert.Null(deletedEvent);
        }

        // Assert - All images should be deleted
        Assert.Equal(eventCount, deletedKeys.Count);
        
        foreach (var expectedKey in expectedDeletedKeys)
        {
            Assert.Contains(expectedKey, deletedKeys);
        }

        mockS3Client.Verify(
            x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(eventCount)
        );
    }

    /// <summary>
    /// Property 9 (Edge Case): Event deletion without image doesn't fail
    /// </summary>
    [Fact]
    public async Task EventDeletion_WithoutImage_CompletesSuccessfully()
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        // Create organizer and event WITHOUT image
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

        var createRequest = new CreateEventRequest
        {
            Name = "Event without Image",
            Description = "No image URL",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = string.Empty, // No image
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await eventService.CreateEventAsync(createRequest, organizerId);
        Assert.True(string.IsNullOrWhiteSpace(createdEvent.ImageUrl));

        // Act - Delete the event (should not fail)
        await eventService.DeleteEventAsync(createdEvent.Id, Guid.NewGuid(), UserRole.Admin);

        // Assert - Event should be deleted
        var deletedEvent = await _context.Events.FindAsync(createdEvent.Id);
        Assert.Null(deletedEvent);

        // Assert - S3 delete should NOT be called since there's no image
        mockS3Client.Verify(
            x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>
    /// Property 9 (Edge Case): Event deletion with empty image URL doesn't fail
    /// </summary>
    [Fact]
    public async Task EventDeletion_WithEmptyImageUrl_CompletesSuccessfully()
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

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

        var createRequest = new CreateEventRequest
        {
            Name = "Event with Empty Image",
            Description = "Empty image URL",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = "", // Empty string
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await eventService.CreateEventAsync(createRequest, organizerId);

        // Act - Delete the event
        await eventService.DeleteEventAsync(createdEvent.Id, Guid.NewGuid(), UserRole.Admin);

        // Assert - Event should be deleted
        var deletedEvent = await _context.Events.FindAsync(createdEvent.Id);
        Assert.Null(deletedEvent);

        // Assert - S3 delete should NOT be called for empty URL
        mockS3Client.Verify(
            x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    /// <summary>
    /// Property 9 (Resilience): Event deletion succeeds even if image deletion fails
    /// </summary>
    [Fact]
    public async Task EventDeletion_SucceedsEvenWhenImageDeletionFails()
    {
        // Arrange - Mock S3 to fail on delete
        var mockS3Client = new Mock<IR2StorageClient>();
        mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Simulated S3 failure"));

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

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

        var imageUrl = "https://pub-test.r2.dev/events/12345678-1234-1234-1234-123456789abc.jpg";
        var createRequest = new CreateEventRequest
        {
            Name = "Event with Image",
            Description = "Testing resilience",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            ImageUrl = imageUrl,
            TicketTypes = new List<CreateTicketTypeRequest>
            {
                new CreateTicketTypeRequest { Name = "General", Price = 50, Quantity = 100 }
            }
        };

        var createdEvent = await eventService.CreateEventAsync(createRequest, organizerId);

        // Act - Delete should NOT throw even though S3 delete fails
        await eventService.DeleteEventAsync(createdEvent.Id, Guid.NewGuid(), UserRole.Admin);

        // Assert - Event should still be deleted from database
        var deletedEvent = await _context.Events.FindAsync(createdEvent.Id);
        Assert.Null(deletedEvent);

        // Verify S3 delete was attempted
        mockS3Client.Verify(
            x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion

    #region Property 10: EIM-005 — UpdateEventAsync cleanup invariant

    /// <summary>
    /// Property 10 (EIM-005): for ANY previous/next image-url pair, the R2 delete
    /// is invoked by UpdateEventAsync iff the previous URL is non-empty ∧ the next
    /// URL is non-null ∧ they differ. The generated values are constrained to the
    /// configured PublicUrl base (or "" / null) so the invariant isolates the
    /// cleanup GUARD — DeleteImageAsync itself refuses URLs outside our base.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(R2ImageUrlArb) })]
    public async Task UpdateEvent_CleanupInvariant_DeleteCalledIffOldNonEmptyNewNonNullAndDifferent(string? previousImageUrl, string? newImageUrl)
    {
        // Arrange — a recording R2 client and an event owning the previous image
        var deletedKeys = new List<string>();
        var mockS3Client = new Mock<IR2StorageClient>();
        mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((bucket, key, ct) => deletedKeys.Add(key))
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        var organizerId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Event",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = previousImageUrl ?? string.Empty,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var request = new UpdateEventRequest
        {
            Name = "Event",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = newImageUrl
        };

        // Act
        await eventService.UpdateEventAsync(eventEntity.Id, request, organizerId, UserRole.Organizador);

        // Assert — the invariant: delete called exactly when the guard fires
        var oldIsNonEmpty = !string.IsNullOrWhiteSpace(previousImageUrl);
        var newIsNonNull = newImageUrl != null;
        var different = !string.Equals(previousImageUrl ?? string.Empty, newImageUrl ?? string.Empty, StringComparison.Ordinal);
        var shouldDelete = oldIsNonEmpty && newIsNonNull && different;

        Assert.Equal(shouldDelete ? 1 : 0, deletedKeys.Count);
        if (shouldDelete)
        {
            Assert.Equal(previousImageUrl!, $"https://pub-test.r2.dev/{deletedKeys.Single()}");
        }
    }

    #endregion

    #region Event Image Replacement (EIM-005 via UpdateEventAsync)

    /// <summary>
    /// EIM-005: replacing an event's image (PUT carrying a NEW imageUrl) persists
    /// the new URL and best-effort deletes the previous R2 object after the save.
    /// </summary>
    [Fact]
    public async Task UpdateEvent_ReplacedImage_DeletesPreviousObject()
    {
        // Arrange
        var deletedKeys = new List<string>();
        var mockS3Client = new Mock<IR2StorageClient>();
        mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((bucket, key, ct) => deletedKeys.Add(key))
            .Returns(Task.CompletedTask);

        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

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

        var previousImageUrl = "https://pub-test.r2.dev/events/old-guid.jpg";
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Event with Image",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = previousImageUrl,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Event with Image",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = "https://pub-test.r2.dev/events/new-guid.jpg"
        };

        // Act — the new URL is attached via PUT; no upload happens in UpdateEventAsync
        await eventService.UpdateEventAsync(eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        // Assert — event points at the new image and the previous object is gone
        var updatedEvent = await _context.Events.FindAsync(eventEntity.Id);
        Assert.NotNull(updatedEvent);
        Assert.Equal("https://pub-test.r2.dev/events/new-guid.jpg", updatedEvent.ImageUrl);

        Assert.Single(deletedKeys);
        Assert.Equal("events/old-guid.jpg", deletedKeys[0]);

        mockS3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// EIM-005: attaching a new image to an event WITHOUT a previous image never
    /// calls R2 delete.
    /// </summary>
    [Fact]
    public async Task UpdateEvent_NewImage_NoPreviousImage_DoesNotDelete()
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

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

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Event without Image",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = string.Empty,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Event without Image",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = "https://pub-test.r2.dev/events/new-guid.jpg"
        };

        // Act
        await eventService.UpdateEventAsync(eventEntity.Id, updateRequest, organizerId, UserRole.Organizador);

        // Assert — nothing to clean up
        var updatedEvent = await _context.Events.FindAsync(eventEntity.Id);
        Assert.NotNull(updatedEvent);
        Assert.Equal("https://pub-test.r2.dev/events/new-guid.jpg", updatedEvent.ImageUrl);
        mockS3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// EIM-007: a non-owner (non-admin) cannot persist a new imageUrl — the
    /// UpdateEventAsync ownership guard rejects before any R2 call.
    /// </summary>
    [Fact]
    public async Task UpdateEvent_ByNonOwnerWithNewImage_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        var organizerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Event",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = "https://pub-test.r2.dev/events/some.jpg",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Name = "Event",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = "https://pub-test.r2.dev/events/other.jpg"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            eventService.UpdateEventAsync(eventEntity.Id, updateRequest, otherUserId, UserRole.Organizador));

        mockS3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// EIM-005: persisting an imageUrl on a missing event throws and never
    /// touches R2.
    /// </summary>
    [Fact]
    public async Task UpdateEvent_OnMissingEventWithImage_ThrowsKeyNotFoundException()
    {
        // Arrange
        var mockS3Client = new Mock<IR2StorageClient>();
        var eventService = new EventService(_context, _logger, _configuration, mockS3Client.Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System, Options.Create(new HideExpiredEventsOptions()));

        var updateRequest = new UpdateEventRequest
        {
            Name = "Event",
            Description = "Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Location",
            ImageUrl = "https://pub-test.r2.dev/events/new-guid.jpg"
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            eventService.UpdateEventAsync(Guid.NewGuid(), updateRequest, Guid.NewGuid(), UserRole.Organizador));

        mockS3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}

/// <summary>
/// FsCheck generator for the EIM-005 cleanup invariant (Property 10): valid R2
/// URLs under the configured test PublicUrl base, plus the "" / null boundary
/// values the cleanup guard treats specially.
/// </summary>
public static class R2ImageUrlArb
{
    public static Arbitrary<string?> R2ImageUrl() =>
        new R2ImageUrlArbitrary();

    private class R2ImageUrlArbitrary : Arbitrary<string?>
    {
        public R2ImageUrlArbitrary()
        {
            Generator = GenStatic.Elements(
                "https://pub-test.r2.dev/events/old-one.jpg",
                "https://pub-test.r2.dev/events/old-two.png",
                "https://pub-test.r2.dev/events/old-three.webp",
                "",
                null);
        }

        public override Gen<string?> Generator { get; }

        public override IEnumerable<string?> Shrinker(string? value) => Enumerable.Empty<string?>();
    }
}
