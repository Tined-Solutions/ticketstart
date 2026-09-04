using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services.Guards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

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
    private readonly IR2StorageClient _r2Client;
    private readonly IEventNotificationQueue _notificationQueue;
    private readonly TimeProvider _clock;
    private readonly IOptions<HideExpiredEventsOptions> _hideExpiredOptions;

    // Allowed image MIME types
    private static readonly HashSet<string> AllowedImageTypes = new()
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    // Maximum image size: 5MB
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    // Admin add-ticket-stock caps (D-7): per-operation high anti-error cap, not a restrictive limit.
    private const int MaxAdditionalStock = 1000;
    private const int MaxTicketQuantityPerOperation = 1000;

    public EventService(ApplicationDbContext context, ILogger<EventService> logger, IConfiguration configuration, IR2StorageClient r2Client, IEventNotificationQueue notificationQueue, TimeProvider timeProvider, IOptions<HideExpiredEventsOptions> hideExpiredOptions)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _r2Client = r2Client;
        _notificationQueue = notificationQueue;
        _clock = timeProvider;
        _hideExpiredOptions = hideExpiredOptions;
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
        
        if (request.Date <= _clock.GetUtcNow().UtcDateTime)
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

        var now = _clock.GetUtcNow().UtcDateTime;
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
            UpdatedAt = now,
            // EA-002: new events always start Pending; CreateEventRequest has no
            // Status field, so no client input can override the initial status.
            Status = EventStatus.Pending
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
    /// Availability is computed mathematically in real time:
    /// Quantity - sold tickets (Tickets rows) - active unexpired reservations.
    /// There is no stock counter.
    /// </summary>
    public async Task<EventWithAvailability?> GetEventByIdAsync(Guid eventId, bool includeExpired = false)
    {
        _logger.LogInformation("Retrieving event {EventId} with availability", eventId);

        IQueryable<Event> query = _context.Events
            .Include(e => e.TicketTypes);

        // EHE-003/ADR-2: public callers must not see expired events. The inline
        // `e.Date > now` predicate keeps the filter translatable to a single SQL
        // predicate — never call e.IsExpired(...) inside an IQueryable (EF Core
        // cannot translate the method call and would client-evaluate). Management
        // callers (EventOwnership/RequireScanAccessRole) pass includeExpired:true.
        if (_hideExpiredOptions.Value.Enabled && !includeExpired)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            query = query.Where(e => e.Date > now);
        }

        var eventEntity = await query
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found", eventId);
            return null;
        }

        var (sold, reserved) = await ComputeAvailabilityAggregatesAsync(
            eventEntity.TicketTypes.Select(tt => tt.Id).ToList());

        return await MapToEventWithAvailabilityAsync(eventEntity, sold, reserved);
    }

    /// <summary>
    /// Retrieves all published events with ticket availability calculations.
    /// Aggregations for all ticket types across all events run as two batched queries
    /// (one COUNT/GROUP BY, one SUM/GROUP BY) to avoid N+1.
    /// </summary>
    public async Task<IEnumerable<EventWithAvailability>> GetAllPublishedEventsAsync()
    {
        _logger.LogInformation("Retrieving all published events");

        IQueryable<Event> query = _context.Events
            .Include(e => e.TicketTypes);

        // EHE-002/ADR-2: public catalog excludes expired events (inline,
        // translatable predicate — see GetEventByIdAsync for the rationale).
        // The staff scan chooser no longer routes here — it uses
        // GetScannableEventsAsync (EHE-007).
        if (_hideExpiredOptions.Value.Enabled)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            query = query.Where(e => e.Date > now);
        }

        // EHE-002: public catalog also excludes unapproved events — only
        // Approved events are buyer-visible (EA-002 moderation gate).
        query = query.Where(e => e.Status == EventStatus.Approved);

        var events = await query
            .ToListAsync();

        var allTicketTypeIds = events
            .SelectMany(e => e.TicketTypes)
            .Select(tt => tt.Id)
            .ToList();

        var (sold, reserved) = await ComputeAvailabilityAggregatesAsync(allTicketTypeIds);

        var result = new List<EventWithAvailability>();
        foreach (var eventEntity in events)
        {
            result.Add(await MapToEventWithAvailabilityAsync(eventEntity, sold, reserved));
        }

        return result;
    }

    /// <summary>
    /// Retrieves the events the staff QR scanner can validate tickets for
    /// (EHE-007): future events plus events that ended within the QR validation
    /// window (TicketService.ValidationWindowHours hours). Ordered with future
    /// events first (ascending), then ended events descending (most recently
    /// ended first). The scanner window is a hard technical rule — it applies
    /// regardless of the HideExpiredEvents feature flag.
    /// </summary>
    public async Task<IEnumerable<EventWithAvailability>> GetScannableEventsAsync()
    {
        _logger.LogInformation("Retrieving scannable events");

        var now = _clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddHours(-TicketService.ValidationWindowHours);

        var events = await _context.Events
            .Include(e => e.TicketTypes)
            // EA-002 moderation gate: only Approved events are scannable — a
            // Pending/Rejected event must never appear in the scanner chooser,
            // even when its date falls inside the validation window.
            .Where(e => e.Status == EventStatus.Approved)
            // Only events whose QR codes can still validate: Date > now - 24h.
            // The inline predicate (and the ordering below) are EF-translatable —
            // never call e.IsExpired(...) inside an IQueryable.
            .Where(e => e.Date > cutoff)
            // Ordering: future events (Date > now) first, ascending by Date;
            // ended events after them, descending by Date (most recently ended
            // first). The ternaries fold into translatable CASE expressions.
            .OrderBy(e => e.Date > now ? 0 : 1)
            .ThenBy(e => e.Date > now ? e.Date : DateTime.MaxValue)
            .ThenByDescending(e => e.Date > now ? DateTime.MinValue : e.Date)
            .ToListAsync();

        var allTicketTypeIds = events
            .SelectMany(e => e.TicketTypes)
            .Select(tt => tt.Id)
            .ToList();

        var (sold, reserved) = await ComputeAvailabilityAggregatesAsync(allTicketTypeIds);

        var result = new List<EventWithAvailability>();
        foreach (var eventEntity in events)
        {
            result.Add(await MapToEventWithAvailabilityAsync(eventEntity, sold, reserved));
        }

        return result;
    }

    /// <summary>
    /// Computes, in two batched queries, the number of sold tickets and the sum of active
    /// unexpired reservation quantities for the given ticket type ids.
    /// </summary>
    private async Task<(IReadOnlyDictionary<Guid, int> Sold, IReadOnlyDictionary<Guid, int> Reserved)> ComputeAvailabilityAggregatesAsync(List<Guid> ticketTypeIds)
    {
        if (ticketTypeIds.Count == 0)
        {
            return (new Dictionary<Guid, int>(), new Dictionary<Guid, int>());
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        var soldByType = await _context.Tickets
            .Where(t => ticketTypeIds.Contains(t.TicketTypeId) && !t.IsRefunded) // APR-005
            .GroupBy(t => t.TicketTypeId)
            .Select(g => new { TicketTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TicketTypeId, x => x.Count);

        var reservedByType = await _context.Reservations
            .Where(r => ticketTypeIds.Contains(r.TicketTypeId) &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > now)
            .GroupBy(r => r.TicketTypeId)
            .Select(g => new { TicketTypeId = g.Key, Sum = g.Sum(r => r.Quantity) })
            .ToDictionaryAsync(x => x.TicketTypeId, x => x.Sum);

        return (soldByType, reservedByType);
    }

    /// <summary>
    /// Increments an existing TicketType.Quantity under the same SELECT ... FOR UPDATE row lock used
    /// by ReservationService.CreateReservationTransactionalAsync (D-1). The operation serializes against
    /// concurrent reservations on the same ticket type: no lost update, no oversell (ATS-002/ATS-003).
    /// Availability is never stored — the response recomputes it mathematically (ATS-006).
    /// </summary>
    public async Task<TicketTypeWithAvailability> AddTicketStockAsync(Guid eventId, Guid ticketTypeId, int additionalQuantity)
    {
        _logger.LogInformation("Admin adding {Quantity} tickets to ticket type {TicketTypeId} (event {EventId})",
            additionalQuantity, ticketTypeId, eventId);

        // Validate before taking the lock where possible (D-7)
        if (additionalQuantity <= 0)
            throw new ArgumentException("Additional quantity must be greater than zero", nameof(additionalQuantity));

        if (additionalQuantity > MaxAdditionalStock)
            throw new ArgumentException($"Additional quantity must not exceed {MaxAdditionalStock}", nameof(additionalQuantity));

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var provider = _context.Database.ProviderName;

                TicketType? ticketType;
                if (provider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                {
                    // Native PostgreSQL row lock: blocks concurrent reservations on the same row.
                    ticketType = await _context.TicketTypes
                        .FromSqlInterpolated($"SELECT * FROM \"TicketTypes\" WHERE \"Id\" = {ticketTypeId} AND \"EventId\" = {eventId} FOR UPDATE")
                        .FirstOrDefaultAsync();
                }
                else if (provider == "Microsoft.EntityFrameworkCore.Sqlite")
                {
                    // SQLite has no FOR UPDATE support. A no-op UPDATE on the row acquires the
                    // database write lock so the check-then-write serializes against concurrent writers.
                    ticketType = await _context.TicketTypes
                        .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId && tt.EventId == eventId);
                    if (ticketType != null)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE \"TicketTypes\" SET \"CreatedAt\" = \"CreatedAt\" WHERE \"Id\" = {ticketTypeId}");
                    }
                }
                else
                {
                    // InMemory provider (tests): no native locking support.
                    ticketType = await _context.TicketTypes
                        .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId && tt.EventId == eventId);
                }

                if (ticketType == null)
                {
                    _logger.LogWarning("Ticket type {TicketTypeId} not found for event {EventId}", ticketTypeId, eventId);
                    throw new KeyNotFoundException($"Ticket type {ticketTypeId} not found for event {eventId}");
                }

                // PEM-001/ADR-7: the guard MUST run INSIDE the FOR UPDATE critical
                // section on the loaded row — load the event (identity-map hit after
                // the TT load) and throw before any capacity mutation.
                var eventEntity = await _context.Events.FindAsync(eventId)
                    ?? throw new KeyNotFoundException($"Event with ID {eventId} not found");
                EventFinalizedGuard.EnsureMutable(eventEntity, _clock);

                ticketType.Quantity += additionalQuantity;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Added {Quantity} tickets to ticket type {TicketTypeId}; new quantity {NewQuantity}",
                    additionalQuantity, ticketTypeId, ticketType.Quantity);

                return await MapTicketTypeWithAvailabilityAsync(ticketType);
            }
            catch
            {
                // Any failure rolls back the entire operation.
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// Creates a new TicketType on an existing event inside a transaction (ATS-004).
    /// Transaction-only — no shared row lock is needed because the new row cannot race with
    /// existing reservations. Availability is never stored (ATS-006).
    /// </summary>
    public async Task<TicketTypeWithAvailability> AddTicketTypeAsync(Guid eventId, string name, decimal price, int quantity)
    {
        _logger.LogInformation("Admin creating ticket type '{Name}' (price {Price}, quantity {Quantity}) for event {EventId}",
            name, price, quantity, eventId);

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    _logger.LogWarning("Event {EventId} not found", eventId);
                    throw new KeyNotFoundException($"Event with ID {eventId} not found");
                }

                // PEM-001/ADR-6: a finalized event is immutable — the guard throws
                // BEFORE the validation/insert + SaveChanges inside the transaction.
                EventFinalizedGuard.EnsureMutable(eventEntity, _clock);

                // Validate inside the transaction (D-7; mirrors CreateEventAsync guards)
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("Ticket type name is required", nameof(name));

                if (name.Trim().Length > 100)
                    throw new ArgumentException("Ticket type name must not exceed 100 characters", nameof(name));

                if (price < 0)
                    throw new ArgumentException("Ticket price cannot be negative", nameof(price));

                if (quantity <= 0)
                    throw new ArgumentException("Ticket quantity must be greater than zero", nameof(quantity));

                if (quantity > MaxTicketQuantityPerOperation)
                    throw new ArgumentException($"Ticket quantity must not exceed {MaxTicketQuantityPerOperation}", nameof(quantity));

                var now = _clock.GetUtcNow().UtcDateTime;
                var ticketType = new TicketType
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    Name = name.Trim(),
                    Price = price,
                    Quantity = quantity,
                    CreatedAt = now
                };

                _context.TicketTypes.Add(ticketType);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Created ticket type '{Name}' ({TicketTypeId}) for event {EventId}",
                    ticketType.Name, ticketType.Id, eventId);

                return await MapTicketTypeWithAvailabilityAsync(ticketType);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// Maps a TicketType entity to the { id, name, price, quantity, available } response shape (D-4).
    /// Availability is recomputed mathematically with the same clamping used by MapToEventWithAvailabilityAsync.
    /// </summary>
    private async Task<TicketTypeWithAvailability> MapTicketTypeWithAvailabilityAsync(TicketType tt)
    {
        var (sold, reserved) = await ComputeAvailabilityAggregatesAsync(new List<Guid> { tt.Id });

        sold.TryGetValue(tt.Id, out var soldCount);
        reserved.TryGetValue(tt.Id, out var reservedCount);
        var available = Math.Max(0, tt.Quantity - soldCount - reservedCount);

        return new TicketTypeWithAvailability
        {
            Id = tt.Id,
            Name = tt.Name,
            Price = tt.Price,
            Quantity = tt.Quantity,
            Available = available
        };
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

        // PEM-001/ADR-6: a finalized event is immutable — the guard throws BEFORE
        // any SaveChanges/audit/notification (the EDC-001 date-change buyer emails
        // below become unreachable for past events). Hard rule, flag-independent.
        EventFinalizedGuard.EnsureMutable(eventEntity, _clock);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Event name is required", nameof(request.Name));
        
        if (string.IsNullOrWhiteSpace(request.Location))
            throw new ArgumentException("Event location is required", nameof(request.Location));
        
        if (request.Date <= _clock.GetUtcNow().UtcDateTime)
            throw new ArgumentException("Event date must be in the future", nameof(request.Date));

        // Update event properties. ImageUrl is special: null (omitted) preserves
        // the existing image so a plain text edit never wipes it; "" clears it
        // explicitly; a value replaces it.
        var oldDate = eventEntity.Date;

        eventEntity.Name = request.Name;
        eventEntity.Description = request.Description;
        eventEntity.Date = request.Date;
        eventEntity.Location = request.Location;
        if (request.ImageUrl != null)
        {
            eventEntity.ImageUrl = request.ImageUrl;
        }
        eventEntity.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} updated successfully by user {UserId}", eventId, userId);

        // EDC-001 / EDC-007: single extensible condition block for change detection.
        // Future location/time changes can be added as additional conditions here.
        var dateChanged = oldDate != request.Date;
        if (dateChanged)
        {
            // EDC-002: query distinct non-refunded buyers, resolving a best-effort
            // recipient name per email for personalized greetings. A buyer email may
            // span several reservations; the name prefers the most recent reservation's
            // non-empty PurchaserName, then the linked User.Name, else null (the email
            // layer falls back to a generic greeting).
            var buyerRecipients = await _context.Tickets
                .Where(t => t.EventId == eventId && !t.IsRefunded)
                .Select(t => new
                {
                    Email = t.PurchaserEmail,
                    PurchaserName = t.Reservation != null ? t.Reservation.PurchaserName : null,
                    UserName = t.Reservation != null && t.Reservation.User != null ? t.Reservation.User.Name : null,
                    ReservationCreatedAt = t.Reservation != null ? (DateTime?)t.Reservation.CreatedAt : null
                })
                .ToListAsync();

            var recipientEmails = buyerRecipients
                .GroupBy(r => r.Email)
                .Select(g => new
                {
                    Email = g.Key,
                    RecipientName = g
                        .Where(r => !string.IsNullOrWhiteSpace(r.PurchaserName))
                        .OrderByDescending(r => r.ReservationCreatedAt ?? DateTime.MinValue)
                        .Select(r => r.PurchaserName)
                        .FirstOrDefault()
                        ?? g
                            .Where(r => !string.IsNullOrWhiteSpace(r.UserName))
                            .OrderByDescending(r => r.ReservationCreatedAt ?? DateTime.MinValue)
                            .Select(r => r.UserName)
                            .FirstOrDefault()
                })
                .ToList();

            // EDC-005: zero buyers → silent no-op
            if (recipientEmails.Count > 0)
            {
                _logger.LogInformation(
                    "Date change detected for event {EventId}: {OldDate} → {NewDate}. " +
                    "Enqueueing {BuyerCount} notifications.",
                    eventId, oldDate, request.Date, recipientEmails.Count);

                var now = _clock.GetUtcNow().UtcDateTime;
                foreach (var recipient in recipientEmails)
                {
                    var notification = new EventNotification
                    {
                        EventId = eventId,
                        EventName = eventEntity.Name,
                        NotificationType = "DateChange",
                        OldDate = oldDate,
                        NewDate = request.Date,
                        RecipientEmail = recipient.Email,
                        RecipientName = recipient.RecipientName,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    // EDC-004: enqueue and return immediately — email failure
                    // does NOT rollback the event update.
                    await _notificationQueue.EnqueueAsync(notification);
                }
            }
        }

        return eventEntity;
    }

    /// <summary>
    /// Deletes an event (Admin-only, ED-001) with image cleanup.
    /// Only users with the Admin role can delete events — organizers are rejected
    /// for ANY event regardless of status or age.
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

        // ED-001: deletion is Admin-only — organizers lose delete authority for ANY
        // event (any status/age). Runs BEFORE the finalized guard so an organizer
        // never receives 409 from delete; no side effects can occur past this point.
        if (userRole != UserRole.Admin)
        {
            _logger.LogWarning("User {UserId} (role {UserRole}) denied delete of event {EventId} — Admin-only (ED-001)", userId, userRole, eventId);
            throw new UnauthorizedAccessException("Only administrators can delete events");
        }

        // PEM-001/ADR-6: a finalized event is immutable — the guard throws BEFORE
        // the Remove + SaveChanges + R2 image cleanup below.
        EventFinalizedGuard.EnsureMutable(eventEntity, _clock);

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
            // Buffer the stream to a known length so the SDK uses regular
            // AWS4-HMAC-SHA256 signing instead of streaming signatures
            // (R2 does not support STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER).
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Upload to R2 using the raw SigV4 client (the AWS SDK cannot
            // negotiate TLS with R2 from Linux containers — see R2StorageClient).
            await _r2Client.PutObjectAsync(bucketName, objectKey, memoryStream, contentType);

            // Construct the public URL
            var imageUrl = $"{publicUrl.TrimEnd('/')}/{objectKey}";

            _logger.LogInformation("Image uploaded successfully to R2: {ImageUrl}", imageUrl);

            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while uploading image to R2");
            throw new InvalidOperationException("Failed to upload image to R2", ex);
        }
    }

    /// <summary>
    /// Replaces an event's image: uploads the new image to R2, updates the event's
    /// ImageUrl, then best-effort deletes the previous image object from R2 so it
    /// does not stay orphaned. Ownership validation mirrors <see cref="UpdateEventAsync"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Event not found.</exception>
    /// <exception cref="UnauthorizedAccessException">User is not the owner and not an Admin.</exception>
    /// <exception cref="ArgumentException">Image validation fails (invalid type or size).</exception>
    public async Task<string> ReplaceEventImageAsync(Guid eventId, Guid userId, UserRole userRole, Stream imageStream, string fileName, string contentType)
    {
        _logger.LogInformation("User {UserId} replacing image for event {EventId}", userId, eventId);

        var eventEntity = await _context.Events.FindAsync(eventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found for image replacement", eventId);
            throw new KeyNotFoundException($"Event with ID {eventId} not found");
        }

        if (eventEntity.OrganizerId != userId && userRole != UserRole.Admin)
        {
            _logger.LogWarning("User {UserId} unauthorized to replace image for event {EventId} owned by {OrganizerId}",
                userId, eventId, eventEntity.OrganizerId);
            throw new UnauthorizedAccessException("You do not have permission to update this event");
        }

        // PEM-001/ADR-6: a finalized event is immutable — the guard throws BEFORE
        // the R2 upload, the ImageUrl swap, and the SaveChanges below.
        EventFinalizedGuard.EnsureMutable(eventEntity, _clock);

        var previousImageUrl = eventEntity.ImageUrl;

        var newImageUrl = await UploadEventImageAsync(imageStream, fileName, contentType);

        eventEntity.ImageUrl = newImageUrl;
        eventEntity.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        // Best-effort cleanup: the old object is orphaned once the event points at
        // the new URL. Failure must not fail the request (mirrors DeleteEventAsync).
        if (!string.IsNullOrWhiteSpace(previousImageUrl))
        {
            var imageDeleted = await DeleteImageAsync(previousImageUrl);
            if (!imageDeleted)
            {
                _logger.LogWarning("Failed to delete previous image for event {EventId}; new image is already in place", eventId);
            }
        }

        return newImageUrl;
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

            // Delete from R2 using the raw SigV4 client (throws on failure).
            await _r2Client.DeleteObjectAsync(bucketName, objectKey);

            _logger.LogInformation("Image deleted successfully from R2: {ObjectKey}", objectKey);
            return true;
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
    /// Availability = Quantity - sold - active unexpired reservations (clamped to 0).
    /// Counts are pre-aggregated in batched queries to avoid N+1.
    /// </summary>
    private static async Task<EventWithAvailability> MapToEventWithAvailabilityAsync(
        Event eventEntity,
        IReadOnlyDictionary<Guid, int> soldCounts,
        IReadOnlyDictionary<Guid, int> reservedCounts)
    {
        var ticketTypesWithAvailability = eventEntity.TicketTypes.Select(tt =>
        {
            soldCounts.TryGetValue(tt.Id, out var sold);
            reservedCounts.TryGetValue(tt.Id, out var reserved);
            var available = Math.Max(0, tt.Quantity - sold - reserved);

            return new TicketTypeWithAvailability
            {
                Id = tt.Id,
                Name = tt.Name,
                Price = tt.Price,
                Quantity = tt.Quantity,
                Available = available
            };
        }).ToList();

        return await Task.FromResult(new EventWithAvailability
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
            Status = eventEntity.Status,
            TicketTypes = ticketTypesWithAvailability
        });
    }
}
