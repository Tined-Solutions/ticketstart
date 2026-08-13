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
    /// <param name="includeExpired">
    /// When false (default), expired events are hidden from the caller — the query
    /// filters to future-dated events (EHE-003). Pass true from role-gated
    /// management paths (EventOwnership / RequireStaffRole) so organizers, staff
    /// and admins keep full access to past events (EHE-006/007).
    /// </param>
    /// <returns>The event with calculated ticket availability, or null if not found</returns>
    Task<EventWithAvailability?> GetEventByIdAsync(Guid eventId, bool includeExpired = false);

    /// <summary>
    /// Retrieves all published events with ticket availability calculations.
    /// </summary>
    /// <param name="includeExpired">
    /// When false (default), expired events are excluded from the public catalog
    /// (EHE-002). Pass true from the staff-gated management list so past events
    /// stay available for scanning (EHE-007).
    /// </param>
    /// <returns>List of all events with availability information</returns>
    Task<IEnumerable<EventWithAvailability>> GetAllPublishedEventsAsync(bool includeExpired = false);

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

    /// <summary>
    /// Replaces an event's image: uploads the new image to R2, updates the event's
    /// ImageUrl, then best-effort deletes the previous image object from R2 so it
    /// does not stay orphaned. Ownership validation mirrors <see cref="UpdateEventAsync"/>.
    /// </summary>
    /// <param name="eventId">ID of the event owning the image</param>
    /// <param name="userId">ID of the user attempting the replacement</param>
    /// <param name="userRole">Role of the user attempting the replacement</param>
    /// <param name="imageStream">Stream containing the image data</param>
    /// <param name="fileName">Original filename of the image</param>
    /// <param name="contentType">MIME type of the image</param>
    /// <returns>The public URL of the newly uploaded image in R2 storage</returns>
    /// <exception cref="KeyNotFoundException">Event not found.</exception>
    /// <exception cref="UnauthorizedAccessException">User is not the owner and not an Admin.</exception>
    /// <exception cref="ArgumentException">Image validation fails (invalid type or size).</exception>
    Task<string> ReplaceEventImageAsync(Guid eventId, Guid userId, UserRole userRole, Stream imageStream, string fileName, string contentType);

    /// <summary>
    /// Increments an existing TicketType.Quantity under SELECT...FOR UPDATE. Mirrors ReservationService.
    /// </summary>
    /// <param name="eventId">ID of the event owning the ticket type</param>
    /// <param name="ticketTypeId">ID of the ticket type to increment</param>
    /// <param name="additionalQuantity">Positive quantity to add to the existing stock (≤ MaxAdditionalStock)</param>
    /// <returns>The updated ticket type with recomputed availability</returns>
    /// <exception cref="KeyNotFoundException">Event or ticket type not found, or EventId mismatch.</exception>
    /// <exception cref="ArgumentException">additionalQuantity <= 0 or > MaxAdditionalStock.</exception>
    Task<TicketTypeWithAvailability> AddTicketStockAsync(Guid eventId, Guid ticketTypeId, int additionalQuantity);

    /// <summary>
    /// Creates a new TicketType on an existing event (transaction-only, no row lock).
    /// </summary>
    /// <param name="eventId">ID of the event to attach the new ticket type to</param>
    /// <param name="name">Ticket type name (non-empty, trimmed, ≤ 100 chars)</param>
    /// <param name="price">Ticket price (≥ 0)</param>
    /// <param name="quantity">Initial stock quantity (&gt; 0 and ≤ MaxTicketQuantityPerOperation)</param>
    /// <returns>The created ticket type with recomputed availability</returns>
    /// <exception cref="KeyNotFoundException">Event not found.</exception>
    /// <exception cref="ArgumentException">Invalid name/price/quantity.</exception>
    Task<TicketTypeWithAvailability> AddTicketTypeAsync(Guid eventId, string name, decimal price, int quantity);
}

/// <summary>
/// Request body for incrementing the stock of an existing ticket type.
/// </summary>
public record AddTicketStockRequest(int AdditionalQuantity);

/// <summary>
/// Request body for creating a new ticket type on an existing event.
/// </summary>
public record AddTicketTypeRequest(string Name, decimal Price, int Quantity);

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
    /// <summary>
    /// Image URL semantics: null (omitted) preserves the existing image,
    /// an empty string clears it, a value replaces it. Never wipe the image
    /// just because a text edit did not include it.
    /// </summary>
    public string? ImageUrl { get; set; }
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
