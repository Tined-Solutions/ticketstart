using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// EF-backed enqueue implementation. Records a notification row to the
/// EventNotification table and returns immediately — no email is sent here.
/// The BackgroundService (EventNotificationDispatchService) picks it up later.
///
/// This is the ENQUEUE-side seam. The DISPATCH side is owned by the
/// BackgroundService + IRetryableEmailSender.
/// </summary>
public class EventNotificationQueue : IEventNotificationQueue
{
    private readonly ApplicationDbContext _context;

    public EventNotificationQueue(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(EventNotification notification)
    {
        _context.EventNotifications.Add(notification);
        await _context.SaveChangesAsync();
    }
}
