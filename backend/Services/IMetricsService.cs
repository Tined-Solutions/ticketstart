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

    /// <summary>
    /// Calculates admin-wide monetary metrics across ALL events: charged money,
    /// recorded refunds and their net (APR-017 money-based semantics), optionally
    /// windowed by charge/refund/ticket date and filtered by event lifecycle.
    /// </summary>
    /// <param name="filter">Optional date range and event lifecycle filter</param>
    /// <returns>Totals plus one monetary row per event</returns>
    Task<AdminMonetaryMetrics> GetAdminMonetaryMetricsAsync(AdminMetricsFilter filter);
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

/// <summary>
/// Filters for the admin monetary metrics. Every field is optional; null means
/// "no bound" / "all lifecycle states".
/// </summary>
public class AdminMetricsFilter
{
    /// <summary>Inclusive lower bound on charge, refund and ticket creation dates.</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive upper bound on charge, refund and ticket creation dates.</summary>
    public DateTime? To { get; set; }

    /// <summary>"upcoming" (not started yet), "past" (already ended) or null for both.</summary>
    public string? EventState { get; set; }
}

/// <summary>
/// Admin monetary metrics: totals plus a per-event breakdown. Net is
/// money-based (APR-017 parity with the organizer revenue and the admin
/// purchases "Neto"): charged − refunded, never derived from list prices.
/// </summary>
public class AdminMonetaryMetrics
{
    public decimal Charged { get; set; }
    public decimal Refunded { get; set; }
    public decimal Net { get; set; }
    public int TicketsSold { get; set; }
    public int RefundOperations { get; set; }
    public IReadOnlyList<AdminEventMonetaryMetrics> Events { get; set; } = Array.Empty<AdminEventMonetaryMetrics>();
}

/// <summary>One event's monetary row in the admin metrics tab.</summary>
public class AdminEventMonetaryMetrics
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }

    /// <summary>True when the event already started (Event.Date &lt; now) — display lifecycle only.</summary>
    public bool IsPast { get; set; }

    public int TicketsSold { get; set; }
    public decimal Charged { get; set; }
    public decimal Refunded { get; set; }
    public decimal Net { get; set; }
}
