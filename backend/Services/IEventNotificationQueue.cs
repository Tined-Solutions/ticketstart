using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Seam between the event-update path and the async notification dispatcher.
/// EventService calls EnqueueAsync to record intent; the BackgroundService
/// processes it later. This keeps EventService decoupled from email concerns
/// (DIP — EventService never references IEmailService).
/// </summary>
public interface IEventNotificationQueue
{
    /// <summary>
    /// Persists a notification row to the EventNotification table and returns
    /// immediately. The actual email delivery happens asynchronously through
    /// EventNotificationDispatchService.
    /// </summary>
    Task EnqueueAsync(EventNotification notification);
}
