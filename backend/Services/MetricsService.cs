using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for calculating organizer dashboard metrics.
/// Performs real-time calculations based on current ticket, reservation, and event data.
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(ApplicationDbContext context, ILogger<MetricsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Calculates metrics for a single event in real-time.
    /// </summary>
    public async Task<EventMetrics?> GetEventMetricsAsync(Guid eventId)
    {
        _logger.LogInformation("Calculating metrics for event {EventId}", eventId);

        var eventEntity = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found for metrics calculation", eventId);
            return null;
        }

        var metrics = await CalculateMetricsAsync(eventEntity);

        _logger.LogInformation(
            "Metrics for event {EventId}: Sold={TicketsSold}, Revenue={TotalRevenue}, Remaining={RemainingInventory}, Scanned={TicketsScanned}",
            eventId, metrics.TicketsSold, metrics.TotalRevenue, metrics.RemainingInventory, metrics.TicketsScanned);

        return metrics;
    }

    /// <summary>
    /// Calculates metrics for all events owned by the specified organizer.
    /// Uses consolidated GroupBy projections — one query per aggregate dimension
    /// (tickets, inventory, reservations) instead of per-event N+1 loops.
    /// </summary>
    public async Task<IEnumerable<EventMetrics>> GetOrganizerMetricsAsync(Guid organizerId)
    {
        _logger.LogInformation("Calculating metrics for organizer {OrganizerId}", organizerId);

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == organizerId)
            .OrderBy(e => e.Date)
            .ToListAsync();

        if (events.Count == 0)
        {
            _logger.LogInformation("No events found for organizer {OrganizerId}", organizerId);
            return Enumerable.Empty<EventMetrics>();
        }

        var eventIds = events.Select(e => e.Id).ToList();

        // Single GroupBy query: ticket aggregates (sold, revenue, scanned) per event.
        // Refunded tickets are excluded from sold/revenue (APR-005); TicketsScanned is
        // unchanged because a refund is blocked when IsUsed (no overlap).
        var ticketAggregates = await _context.Tickets
            .AsNoTracking()
            .Where(t => eventIds.Contains(t.EventId) && !t.IsRefunded)
            .GroupBy(t => t.EventId)
            .Select(g => new
            {
                EventId = g.Key,
                TicketsSold = g.Count(),
                TicketsScanned = g.Count(t => t.IsUsed),
                Revenue = g.Join(
                    _context.TicketTypes.AsNoTracking(),
                    t => t.TicketTypeId,
                    tt => tt.Id,
                    (t, tt) => tt.Price).Sum()
            })
            .ToListAsync();

        // Single GroupBy query: inventory totals per event
        var inventoryAggregates = await _context.TicketTypes
            .AsNoTracking()
            .Where(tt => eventIds.Contains(tt.EventId))
            .GroupBy(tt => tt.EventId)
            .Select(g => new
            {
                EventId = g.Key,
                TotalInventory = g.Sum(tt => (int?)tt.Quantity) ?? 0
            })
            .ToListAsync();

        // Single GroupBy query: active reservations per event
        var reservationAggregates = await _context.Reservations
            .AsNoTracking()
            .Where(r => eventIds.Contains(r.EventId) &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > DateTime.UtcNow)
            .GroupBy(r => r.EventId)
            .Select(g => new
            {
                EventId = g.Key,
                ActiveReservations = g.Sum(r => (int?)r.Quantity) ?? 0
            })
            .ToListAsync();

        // Merge results: O(events) with O(1) lookups (dictionaries)
        var ticketLookup = ticketAggregates.ToDictionary(a => a.EventId);
        var inventoryLookup = inventoryAggregates.ToDictionary(a => a.EventId);
        var reservationLookup = reservationAggregates.ToDictionary(a => a.EventId);

        var metrics = events.Select(e =>
        {
            ticketLookup.TryGetValue(e.Id, out var t);
            inventoryLookup.TryGetValue(e.Id, out var inv);
            reservationLookup.TryGetValue(e.Id, out var res);

            var ticketsSold = t?.TicketsSold ?? 0;
            var activeReservations = res?.ActiveReservations ?? 0;
            var totalInventory = inv?.TotalInventory ?? 0;

            return new EventMetrics
            {
                Id = e.Id,
                EventId = e.Id,
                EventName = e.Name,
                EventDate = e.Date,
                TicketsSold = ticketsSold,
                TotalRevenue = t?.Revenue ?? 0m,
                RemainingInventory = totalInventory - ticketsSold - activeReservations,
                TicketsScanned = t?.TicketsScanned ?? 0
            };
        }).ToList();

        _logger.LogInformation("Retrieved consolidated metrics for {EventCount} events owned by organizer {OrganizerId}",
            metrics.Count, organizerId);

        return metrics;
    }

    /// <summary>
    /// Calculates metrics for a single event entity.
    /// </summary>
    private async Task<EventMetrics> CalculateMetricsAsync(Event eventEntity)
    {
        var eventId = eventEntity.Id;

        // Total tickets sold: confirmed tickets in the database for this event.
        // Refunded tickets do not count (APR-005).
        var ticketsSold = await _context.Tickets
            .AsNoTracking()
            .CountAsync(t => t.EventId == eventId && !t.IsRefunded);

        // Total revenue: sum of ticket type prices for each sold ticket.
        // Refunded tickets do not count (APR-005).
        var totalRevenue = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.EventId == eventId && !t.IsRefunded)
            .Join(
                _context.TicketTypes.AsNoTracking(),
                ticket => ticket.TicketTypeId,
                ticketType => ticketType.Id,
                (ticket, ticketType) => ticketType.Price)
            .SumAsync(price => price);

        // Tickets scanned: tickets marked as used
        var ticketsScanned = await _context.Tickets
            .AsNoTracking()
            .CountAsync(t => t.EventId == eventId && t.IsUsed);

        // Total inventory across all ticket types for the event
        var totalInventory = await _context.TicketTypes
            .AsNoTracking()
            .Where(tt => tt.EventId == eventId)
            .SumAsync(tt => (int?)tt.Quantity) ?? 0;

        // Active reservations: non-expired reservations with Active status
        var activeReservations = await _context.Reservations
            .AsNoTracking()
            .Where(r => r.EventId == eventId &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > DateTime.UtcNow)
            .SumAsync(r => (int?)r.Quantity) ?? 0;

        var remainingInventory = totalInventory - ticketsSold - activeReservations;

        return new EventMetrics
        {
            Id = eventEntity.Id,
            EventId = eventEntity.Id,
            EventName = eventEntity.Name,
            EventDate = eventEntity.Date,
            TicketsSold = ticketsSold,
            TotalRevenue = totalRevenue,
            RemainingInventory = remainingInventory,
            TicketsScanned = ticketsScanned
        };
    }
}
