using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for event management operations.
/// Handles event CRUD operations with ownership validation and ticket availability calculations.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Creates a new event and assigns ownership to the specified organizer.
    /// </summary>
    /// <param name="request">Event creation request containing event details and ticket types</param>
    /// <param name="organizerId">ID of the user creating the event (becomes the owner)</param>
    /// <returns>The created event with all details</returns>
    Task<Event> CreateEventAsync(CreateEventRequest request, Guid organizerId);

    /// <summary>
    /// Retrieves an event by ID with ticket availability calculation.
    /// Availability = ticket type quantity - sold tickets.
    /// </summary>
    /// <param name="eventId">ID of the event to retrieve</param>
    /// <returns>The event with calculated ticket availability, or null if not found</returns>
    Task<EventWithAvailability?> GetEventByIdAsync(Guid eventId);

    /// <summary>
    /// Retrieves all published events with ticket availability calculations.
    /// </summary>
    /// <returns>List of all events with availability information</returns>
    Task<IEnumerable<EventWithAvailability>> GetAllPublishedEventsAsync();

    /// <summary>
    /// Updates an event with ownership validation.
    /// Only the event owner or Admin role can update events.
    /// </summary>
    /// <param name="eventId">ID of the event to update</param>
    /// <param name="request">Update request containing modified event details</param>
    /// <param name="userId">ID of the user attempting the update</param>
    /// <param name="userRole">Role of the user attempting the update</param>
    /// <returns>The updated event</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not the owner and not an Admin</exception>
    /// <exception cref="KeyNotFoundException">Thrown when event is not found</exception>
    Task<Event> UpdateEventAsync(Guid eventId, UpdateEventRequest request, Guid userId, UserRole userRole);

    /// <summary>
    /// Deletes an event with ownership validation and image cleanup.
    /// Only the event owner or Admin role can delete events.
    /// Removes associated images from storage.
    /// </summary>
    /// <param name="eventId">ID of the event to delete</param>
    /// <param name="userId">ID of the user attempting the deletion</param>
    /// <param name="userRole">Role of the user attempting the deletion</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not the owner and not an Admin</exception>
    /// <exception cref="KeyNotFoundException">Thrown when event is not found</exception>
    Task DeleteEventAsync(Guid eventId, Guid userId, UserRole userRole);

    /// <summary>
    /// Uploads an event image to Cloudflare R2 storage.
    /// Validates image file type (JPEG, PNG, WebP) and size (max 5MB).
    /// Generates a unique GUID-based identifier for the image.
    /// </summary>
    /// <param name="imageStream">Stream containing the image data</param>
    /// <param name="fileName">Original filename of the image</param>
    /// <param name="contentType">MIME type of the image</param>
    /// <returns>The public URL of the uploaded image in R2 storage</returns>
    /// <exception cref="ArgumentException">Thrown when image validation fails (invalid type or size)</exception>
    Task<string> UploadEventImageAsync(Stream imageStream, string fileName, string contentType);
}

/// <summary>
/// Request model for creating a new event.
/// </summary>
public class CreateEventRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public List<CreateTicketTypeRequest> TicketTypes { get; set; } = new();
}

/// <summary>
/// Request model for creating a ticket type within an event.
/// </summary>
public class CreateTicketTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Request model for updating an existing event.
/// </summary>
public class UpdateEventRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

/// <summary>
/// Response model for events with calculated ticket availability.
/// </summary>
public class EventWithAvailability
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public Guid OrganizerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TicketTypeWithAvailability> TicketTypes { get; set; } = new();
}

/// <summary>
/// Ticket type with calculated availability.
/// Availability = Quantity - Sold Tickets
/// </summary>
public class TicketTypeWithAvailability
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int Available { get; set; }
}
