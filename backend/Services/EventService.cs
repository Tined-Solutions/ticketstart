using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for event management operations.
/// Handles event CRUD with ownership validation, ticket availability calculations, and image cleanup.
/// </summary>
public class EventService : IEventService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EventService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAmazonS3 _s3Client;

    // Allowed image MIME types
    private static readonly HashSet<string> AllowedImageTypes = new()
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    // Maximum image size: 5MB
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    public EventService(ApplicationDbContext context, ILogger<EventService> logger, IConfiguration configuration, IAmazonS3 s3Client)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _s3Client = s3Client;
    }

    /// <summary>
    /// Creates a new event and assigns ownership to the organizer.
    /// Validates required fields and creates associated ticket types.
    /// </summary>
    public async Task<Event> CreateEventAsync(CreateEventRequest request, Guid organizerId)
    {
        _logger.LogInformation("Creating event '{EventName}' for organizer {OrganizerId}", request.Name, organizerId);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Event name is required", nameof(request.Name));
        
        if (string.IsNullOrWhiteSpace(request.Location))
            throw new ArgumentException("Event location is required", nameof(request.Location));
        
        if (request.Date <= DateTime.UtcNow)
            throw new ArgumentException("Event date must be in the future", nameof(request.Date));
        
        if (request.TicketTypes == null || !request.TicketTypes.Any())
            throw new ArgumentException("At least one ticket type is required", nameof(request.TicketTypes));

        // Validate ticket types
        foreach (var ticketType in request.TicketTypes)
        {
            if (string.IsNullOrWhiteSpace(ticketType.Name))
                throw new ArgumentException("Ticket type name is required");
            
            if (ticketType.Price < 0)
                throw new ArgumentException("Ticket price cannot be negative");
            
            if (ticketType.Quantity <= 0)
                throw new ArgumentException("Ticket quantity must be greater than zero");
        }

        var now = DateTime.UtcNow;
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Date = request.Date,
            Location = request.Location,
            ImageUrl = request.ImageUrl,
            OrganizerId = organizerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Create ticket types
        foreach (var ticketTypeRequest in request.TicketTypes)
        {
            var ticketType = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                Name = ticketTypeRequest.Name,
                Price = ticketTypeRequest.Price,
                Quantity = ticketTypeRequest.Quantity,
                CreatedAt = now
            };
            eventEntity.TicketTypes.Add(ticketType);
        }

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Event '{EventName}' created successfully with ID {EventId}", request.Name, eventEntity.Id);

        return eventEntity;
    }

    /// <summary>
    /// Retrieves an event by ID with ticket availability calculation.
    /// Availability = ticket type quantity - currently reserved tickets.
    /// No longer loads all Tickets via Include (O(1) availability check).
    /// </summary>
    public async Task<EventWithAvailability?> GetEventByIdAsync(Guid eventId)
    {
        _logger.LogInformation("Retrieving event {EventId} with availability", eventId);

        var eventEntity = await _context.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found", eventId);
            return null;
        }

        return MapToEventWithAvailability(eventEntity);
    }

    /// <summary>
    /// Retrieves all published events with ticket availability calculations.
    /// </summary>
    public async Task<IEnumerable<EventWithAvailability>> GetAllPublishedEventsAsync()
    {
        _logger.LogInformation("Retrieving all published events");

        var events = await _context.Events
            .Include(e => e.TicketTypes)
            .ToListAsync();

        return events.Select(MapToEventWithAvailability);
    }

    /// <summary>
    /// Updates an event with ownership validation.
    /// Only the event owner or Admin can update.
    /// </summary>
    public async Task<Event> UpdateEventAsync(Guid eventId, UpdateEventRequest request, Guid userId, UserRole userRole)
    {
        _logger.LogInformation("User {UserId} attempting to update event {EventId}", userId, eventId);

        var eventEntity = await _context.Events.FindAsync(eventId);
        
        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found for update", eventId);
            throw new KeyNotFoundException($"Event with ID {eventId} not found");
        }

        // Validate ownership (owner or admin can update)
        if (eventEntity.OrganizerId != userId && userRole != UserRole.Admin)
        {
            _logger.LogWarning("User {UserId} unauthorized to update event {EventId} owned by {OrganizerId}", 
                userId, eventId, eventEntity.OrganizerId);
            throw new UnauthorizedAccessException("You do not have permission to update this event");
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Event name is required", nameof(request.Name));
        
        if (string.IsNullOrWhiteSpace(request.Location))
            throw new ArgumentException("Event location is required", nameof(request.Location));
        
        if (request.Date <= DateTime.UtcNow)
            throw new ArgumentException("Event date must be in the future", nameof(request.Date));

        // Update event properties
        eventEntity.Name = request.Name;
        eventEntity.Description = request.Description;
        eventEntity.Date = request.Date;
        eventEntity.Location = request.Location;
        eventEntity.ImageUrl = request.ImageUrl;
        eventEntity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} updated successfully by user {UserId}", eventId, userId);

        return eventEntity;
    }

    /// <summary>
    /// Deletes an event with ownership validation and image cleanup.
    /// Only the event owner or Admin can delete.
    /// Removes associated images from storage (if image service is available).
    /// </summary>
    public async Task DeleteEventAsync(Guid eventId, Guid userId, UserRole userRole)
    {
        _logger.LogInformation("User {UserId} attempting to delete event {EventId}", userId, eventId);

        var eventEntity = await _context.Events.FindAsync(eventId);
        
        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found for deletion", eventId);
            throw new KeyNotFoundException($"Event with ID {eventId} not found");
        }

        // Validate ownership (owner or admin can delete)
        if (eventEntity.OrganizerId != userId && userRole != UserRole.Admin)
        {
            _logger.LogWarning("User {UserId} unauthorized to delete event {EventId} owned by {OrganizerId}", 
                userId, eventId, eventEntity.OrganizerId);
            throw new UnauthorizedAccessException("You do not have permission to delete this event");
        }

        // Store image URL for cleanup
        var imageUrl = eventEntity.ImageUrl;

        // Delete event (cascade will handle related entities)
        _context.Events.Remove(eventEntity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} deleted successfully by user {UserId}", eventId, userId);

        // Clean up associated image from R2 storage
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var imageDeleted = await DeleteImageAsync(imageUrl);
            if (!imageDeleted)
            {
                _logger.LogWarning("Failed to delete image for event {EventId}, but event was deleted successfully", eventId);
            }
        }
    }

    /// <summary>
    /// Uploads an event image to Cloudflare R2 storage.
    /// Validates image file type (JPEG, PNG, WebP) and size (max 5MB).
    /// Generates a unique GUID-based identifier for the image.
    /// </summary>
    public async Task<string> UploadEventImageAsync(Stream imageStream, string fileName, string contentType)
    {
        _logger.LogInformation("Uploading event image: {FileName}, ContentType: {ContentType}", fileName, contentType);

        // Validate content type
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedImageTypes.Contains(contentType.ToLowerInvariant()))
        {
            _logger.LogWarning("Invalid image type: {ContentType}. Allowed types: JPEG, PNG, WebP", contentType);
            throw new ArgumentException($"Invalid image type. Allowed types: JPEG, PNG, WebP. Received: {contentType}", nameof(contentType));
        }

        // Validate file size
        if (imageStream.Length > MaxImageSizeBytes)
        {
            _logger.LogWarning("Image size {Size} bytes exceeds maximum allowed size of {MaxSize} bytes", imageStream.Length, MaxImageSizeBytes);
            throw new ArgumentException($"Image size exceeds maximum allowed size of 5MB. Received: {imageStream.Length / 1024.0 / 1024.0:F2}MB", nameof(imageStream));
        }

        // Generate unique identifier for the image
        var imageId = Guid.NewGuid();
        var fileExtension = GetFileExtension(contentType);
        var objectKey = $"events/{imageId}{fileExtension}";

        // Get R2 configuration
        var bucketName = _configuration["CloudflareR2:BucketName"];
        var publicUrl = _configuration["CloudflareR2:PublicUrl"];

        if (string.IsNullOrWhiteSpace(bucketName))
        {
            _logger.LogError("CloudflareR2:BucketName configuration is missing");
            throw new InvalidOperationException("R2 bucket name is not configured");
        }

        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            _logger.LogError("CloudflareR2:PublicUrl configuration is missing");
            throw new InvalidOperationException("R2 public URL is not configured");
        }

        try
        {
            // Upload to R2 using AWS S3 SDK
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                InputStream = imageStream,
                ContentType = contentType,
                AutoCloseStream = false
            };

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogError("Failed to upload image to R2. Status code: {StatusCode}", response.HttpStatusCode);
                throw new InvalidOperationException($"Failed to upload image to R2. Status code: {response.HttpStatusCode}");
            }

            // Construct the public URL
            var imageUrl = $"{publicUrl.TrimEnd('/')}/{objectKey}";

            _logger.LogInformation("Image uploaded successfully to R2: {ImageUrl}", imageUrl);

            return imageUrl;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error while uploading image to R2: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
            throw new InvalidOperationException($"Failed to upload image to R2: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while uploading image to R2");
            throw new InvalidOperationException("Failed to upload image to R2", ex);
        }
    }

    /// <summary>
    /// Deletes an event image from Cloudflare R2 storage.
    /// Extracts the object key from the R2 URL and deletes the object.
    /// Handles deletion failures gracefully by logging errors without throwing exceptions.
    /// </summary>
    /// <param name="imageUrl">The full R2 URL of the image to delete</param>
    /// <returns>True if deletion succeeded, false if it failed</returns>
    private async Task<bool> DeleteImageAsync(string imageUrl)
    {
        try
        {
            _logger.LogInformation("Attempting to delete image from R2: {ImageUrl}", imageUrl);

            // Get R2 configuration
            var bucketName = _configuration["CloudflareR2:BucketName"];
            var publicUrl = _configuration["CloudflareR2:PublicUrl"];

            if (string.IsNullOrWhiteSpace(bucketName))
            {
                _logger.LogError("CloudflareR2:BucketName configuration is missing, cannot delete image");
                return false;
            }

            if (string.IsNullOrWhiteSpace(publicUrl))
            {
                _logger.LogError("CloudflareR2:PublicUrl configuration is missing, cannot delete image");
                return false;
            }

            // Extract the object key from the URL
            // URL format: https://pub-xxxxx.r2.dev/events/guid.jpg
            // We need to extract: events/guid.jpg
            var publicUrlBase = publicUrl.TrimEnd('/');
            if (!imageUrl.StartsWith(publicUrlBase, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Image URL {ImageUrl} does not match configured public URL {PublicUrl}", imageUrl, publicUrlBase);
                return false;
            }

            var objectKey = imageUrl.Substring(publicUrlBase.Length).TrimStart('/');

            if (string.IsNullOrWhiteSpace(objectKey))
            {
                _logger.LogWarning("Could not extract object key from image URL: {ImageUrl}", imageUrl);
                return false;
            }

            // Delete from R2 using AWS S3 SDK
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.NoContent || 
                response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Image deleted successfully from R2: {ObjectKey}", objectKey);
                return true;
            }
            else
            {
                _logger.LogWarning("Unexpected status code when deleting image from R2: {StatusCode}", response.HttpStatusCode);
                return false;
            }
        }
        catch (AmazonS3Exception ex)
        {
            // Log but don't throw - we want event deletion to succeed even if image deletion fails
            _logger.LogError(ex, "AWS S3 error while deleting image from R2: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            // Log but don't throw - we want event deletion to succeed even if image deletion fails
            _logger.LogError(ex, "Unexpected error while deleting image from R2");
            return false;
        }
    }

    /// <summary>
    /// Gets the file extension based on content type.
    /// </summary>
    private static string GetFileExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg" // Default fallback
        };
    }

    /// <summary>
    /// Maps an Event entity to EventWithAvailability response model.
    /// Calculates ticket availability: quantity - currently reserved (O(1), no Tickets table scan).
    /// </summary>
    private static EventWithAvailability MapToEventWithAvailability(Event eventEntity)
    {
        var ticketTypesWithAvailability = eventEntity.TicketTypes.Select(tt =>
        {
            return new TicketTypeWithAvailability
            {
                Id = tt.Id,
                Name = tt.Name,
                Price = tt.Price,
                Quantity = tt.Quantity,
                Available = tt.Quantity - tt.CurrentlyReserved
            };
        }).ToList();

        return new EventWithAvailability
        {
            Id = eventEntity.Id,
            Name = eventEntity.Name,
            Description = eventEntity.Description,
            Date = eventEntity.Date,
            Location = eventEntity.Location,
            ImageUrl = eventEntity.ImageUrl,
            OrganizerId = eventEntity.OrganizerId,
            CreatedAt = eventEntity.CreatedAt,
            UpdatedAt = eventEntity.UpdatedAt,
            TicketTypes = ticketTypesWithAvailability
        };
    }
}
