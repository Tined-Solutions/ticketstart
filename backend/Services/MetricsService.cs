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
    /// </summary>
    public async Task<IEnumerable<EventMetrics>> GetOrganizerMetricsAsync(Guid organizerId)
    {
        _logger.LogInformation("Calculating metrics for organizer {OrganizerId}", organizerId);

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == organizerId)
            .OrderBy(e => e.Date)
            .ToListAsync();

        var metrics = new List<EventMetrics>();
        foreach (var eventEntity in events)
        {
            metrics.Add(await CalculateMetricsAsync(eventEntity));
        }

        _logger.LogInformation("Retrieved metrics for {EventCount} events owned by organizer {OrganizerId}", metrics.Count, organizerId);

        return metrics;
    }

    /// <summary>
    /// Calculates metrics for a single event entity.
    /// </summary>
    private async Task<EventMetrics> CalculateMetricsAsync(Event eventEntity)
    {
        var eventId = eventEntity.Id;

        // Total tickets sold: confirmed tickets in the database for this event
        var ticketsSold = await _context.Tickets
            .AsNoTracking()
            .CountAsync(t => t.EventId == eventId);

        // Total revenue: sum of ticket type prices for each sold ticket
        var totalRevenue = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
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
