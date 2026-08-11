using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for EventNotificationQueue.EnqueueAsync.
/// Validates: row is persisted to DbContext.EventNotifications,
/// SaveChangesAsync is called, and the queue returns immediately.
/// </summary>
public class EventNotificationQueueTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EventNotificationQueue _queue;

    public EventNotificationQueueTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _queue = new EventNotificationQueue(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task EnqueueAsync_PersistsRow()
    {
        var notification = new EventNotification
        {
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            NewDate = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc),
            RecipientEmail = "buyer@test.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _queue.EnqueueAsync(notification);

        var rows = await _context.EventNotifications.ToListAsync();
        Assert.Single(rows);
        Assert.Equal("buyer@test.com", rows[0].RecipientEmail);
        Assert.Equal("DateChange", rows[0].NotificationType);
    }

    [Fact]
    public async Task EnqueueAsync_ReturnsImmediately()
    {
        var notification = new EventNotification
        {
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "buyer@test.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _queue.EnqueueAsync(notification);
        sw.Stop();

        // Enqueue should take well under 1 second (no email send, just DB insert)
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Enqueue took {sw.ElapsedMilliseconds}ms, expected <1000ms");
    }

    /// <summary>
    /// TRIANGULATION: Multiple enqueues for different buyers.
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_MultipleBuyers_CreatesSeparateRows()
    {
        var eventId = Guid.NewGuid();
        var newDate = new DateTime(2026, 12, 25);

        await _queue.EnqueueAsync(new EventNotification
        {
            EventId = eventId,
            NotificationType = "DateChange",
            NewDate = newDate,
            RecipientEmail = "alice@test.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _queue.EnqueueAsync(new EventNotification
        {
            EventId = eventId,
            NotificationType = "DateChange",
            NewDate = newDate,
            RecipientEmail = "bob@test.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var rows = await _context.EventNotifications.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.RecipientEmail == "alice@test.com");
        Assert.Contains(rows, r => r.RecipientEmail == "bob@test.com");
    }
}
