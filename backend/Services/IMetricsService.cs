using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for calculating organizer dashboard metrics.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Calculates metrics for a single event.
    /// </summary>
    /// <param name="eventId">ID of the event to calculate metrics for</param>
    /// <returns>Event metrics, or null if the event does not exist</returns>
    Task<EventMetrics?> GetEventMetricsAsync(Guid eventId);

    /// <summary>
    /// Calculates metrics for all events owned by the specified organizer.
    /// </summary>
    /// <param name="organizerId">ID of the organizer</param>
    /// <returns>Collection of event metrics for the organizer's events</returns>
    Task<IEnumerable<EventMetrics>> GetOrganizerMetricsAsync(Guid organizerId);
}

/// <summary>
/// Data transfer object representing event metrics for the organizer dashboard.
/// </summary>
public class EventMetrics
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int TicketsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public int RemainingInventory { get; set; }
    public int TicketsScanned { get; set; }

    /// <summary>
    /// EA-007: approval status, serialized as "Pending"/"Approved"/"Rejected"
    /// (per-enum <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>)
    /// so the organizer dashboard renders the moderation badge.
    /// </summary>
    public EventStatus Status { get; set; }
}
