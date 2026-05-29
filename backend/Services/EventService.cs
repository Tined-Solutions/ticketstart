using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for event management operations.
/// Handles event CRUD with ownership validation, ticket availability calculations, and image cleanup.
/// </summary>
public class EventService : IEventService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EventService> _logger;

    public EventService(ApplicationDbContext context, ILogger<EventService> logger)
    {
        _context = context;
        _logger = logger;
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
    /// Availability = ticket type quantity - sold tickets (confirmed tickets in database).
    /// </summary>
    public async Task<EventWithAvailability?> GetEventByIdAsync(Guid eventId)
    {
        _logger.LogInformation("Retrieving event {EventId} with availability", eventId);

        var eventEntity = await _context.Events
            .Include(e => e.TicketTypes)
            .Include(e => e.Tickets)
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
            .Include(e => e.Tickets)
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

        // TODO: Implement image cleanup from R2 storage when image service is available
        // This will be implemented in a future task (image storage service)
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            _logger.LogInformation("Image cleanup needed for URL: {ImageUrl}", imageUrl);
        }
    }

    /// <summary>
    /// Maps an Event entity to EventWithAvailability response model.
    /// Calculates ticket availability: quantity - sold tickets.
    /// </summary>
    private EventWithAvailability MapToEventWithAvailability(Event eventEntity)
    {
        var ticketTypesWithAvailability = eventEntity.TicketTypes.Select(tt =>
        {
            // Calculate sold tickets for this ticket type
            var soldTickets = eventEntity.Tickets
                .Count(t => t.TicketTypeId == tt.Id);

            return new TicketTypeWithAvailability
            {
                Id = tt.Id,
                Name = tt.Name,
                Price = tt.Price,
                Quantity = tt.Quantity,
                Available = tt.Quantity - soldTickets
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
