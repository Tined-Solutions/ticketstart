using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Services;
using Amazon.S3;
using Amazon.S3.Model;
using Moq;
using Xunit;
using System.Net;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for EventService image upload functionality
/// Validates Requirements 3.1, 3.2, 3.3, 3.4
/// </summary>
public class EventImageUploadTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EventService _eventService;
    private readonly ILogger<EventService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Mock<IAmazonS3> _s3ClientMock;
    private readonly Mock<IEventNotificationQueue> _mockNotificationQueue;

    public EventImageUploadTests()
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
        
        _eventService = new EventService(_context, _logger, _configuration, _s3ClientMock.Object, _mockNotificationQueue.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region UploadEventImageAsync Tests

    [Fact]
    public async Task UploadEventImageAsync_WithValidJpegImage_UploadsSuccessfully()
    {
        // Arrange
        var imageData = new byte[1024]; // 1KB image
        var imageStream = new MemoryStream(imageData);
        var fileName = "test-image.jpg";
        var contentType = "image/jpeg";

        _s3ClientMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var result = await _eventService.UploadEventImageAsync(imageStream, fileName, contentType);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("https://test.r2.dev/events/", result);
        Assert.EndsWith(".jpg", result);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task UploadEventImageAsync_WithValidPngImage_UploadsSuccessfully()
    {
        // Arrange
        var imageData = new byte[1024]; // 1KB image
        var imageStream = new MemoryStream(imageData);
        var fileName = "test-image.png";
        var contentType = "image/png";

        _s3ClientMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var result = await _eventService.UploadEventImageAsync(imageStream, fileName, contentType);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("https://test.r2.dev/events/", result);
        Assert.EndsWith(".png", result);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task UploadEventImageAsync_WithValidWebPImage_UploadsSuccessfully()
    {
        // Arrange
        var imageData = new byte[1024]; // 1KB image
        var imageStream = new MemoryStream(imageData);
        var fileName = "test-image.webp";
        var contentType = "image/webp";

        _s3ClientMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var result = await _eventService.UploadEventImageAsync(imageStream, fileName, contentType);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("https://test.r2.dev/events/", result);
        Assert.EndsWith(".webp", result);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task UploadEventImageAsync_WithInvalidImageType_ThrowsArgumentException()
    {
        // Arrange
        var imageData = new byte[1024];
        var imageStream = new MemoryStream(imageData);
        var fileName = "test-image.gif";
        var contentType = "image/gif";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.UploadEventImageAsync(imageStream, fileName, contentType));
        
        Assert.Contains("Invalid image type", exception.Message);
        Assert.Contains("JPEG, PNG, WebP", exception.Message);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task UploadEventImageAsync_WithImageExceeding5MB_ThrowsArgumentException()
    {
        // Arrange
        var imageData = new byte[6 * 1024 * 1024]; // 6MB image (exceeds 5MB limit)
        var imageStream = new MemoryStream(imageData);
        var fileName = "large-image.jpg";
        var contentType = "image/jpeg";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.UploadEventImageAsync(imageStream, fileName, contentType));
        
        Assert.Contains("exceeds maximum allowed size of 5MB", exception.Message);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task UploadEventImageAsync_WithExactly5MB_UploadsSuccessfully()
    {
        // Arrange
        var imageData = new byte[5 * 1024 * 1024]; // Exactly 5MB
        var imageStream = new MemoryStream(imageData);
        var fileName = "max-size-image.jpg";
        var contentType = "image/jpeg";

        _s3ClientMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var result = await _eventService.UploadEventImageAsync(imageStream, fileName, contentType);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("https://test.r2.dev/events/", result);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task UploadEventImageAsync_WithEmptyContentType_ThrowsArgumentException()
    {
        // Arrange
        var imageData = new byte[1024];
        var imageStream = new MemoryStream(imageData);
        var fileName = "test-image.jpg";
        var contentType = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.UploadEventImageAsync(imageStream, fileName, contentType));
        
        Assert.Contains("Invalid image type", exception.Message);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task UploadEventImageAsync_GeneratesUniqueIdentifiers()
    {
        // Arrange
        var imageData = new byte[1024];
        var fileName = "test-image.jpg";
        var contentType = "image/jpeg";

        _s3ClientMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act - Upload two images
        var imageStream1 = new MemoryStream(imageData);
        var result1 = await _eventService.UploadEventImageAsync(imageStream1, fileName, contentType);
        
        var imageStream2 = new MemoryStream(imageData);
        var result2 = await _eventService.UploadEventImageAsync(imageStream2, fileName, contentType);

        // Assert - URLs should be different (unique GUIDs)
        Assert.NotEqual(result1, result2);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task UploadEventImageAsync_ReturnsCorrectPublicUrl()
    {
        // Arrange
        var imageData = new byte[1024];
        var imageStream = new MemoryStream(imageData);
        var fileName = "test-image.jpg";
        var contentType = "image/jpeg";

        _s3ClientMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var result = await _eventService.UploadEventImageAsync(imageStream, fileName, contentType);

        // Assert
        Assert.StartsWith("https://test.r2.dev/events/", result);
        Assert.Matches(@"https://test\.r2\.dev/events/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.jpg", result);
    }

    [Fact]
    public async Task UploadEventImageAsync_PassesCorrectParametersToS3Client()
    {
        // Arrange
        var imageData = new byte[1024];
        var imageStream = new MemoryStream(imageData);
        var fileName = "test-image.jpg";
        var contentType = "image/jpeg";

        PutObjectRequest? capturedRequest = null;
        _s3ClientMock
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .Callback<PutObjectRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        await _eventService.UploadEventImageAsync(imageStream, fileName, contentType);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("test-bucket", capturedRequest.BucketName);
        Assert.StartsWith("events/", capturedRequest.Key);
        Assert.Equal(contentType, capturedRequest.ContentType);
        Assert.Equal(imageStream, capturedRequest.InputStream);
    }

    #endregion
}
