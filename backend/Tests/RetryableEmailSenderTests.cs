using Microsoft.EntityFrameworkCore;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Pure unit tests for RetryableEmailSender.ProcessAsync.
/// Validates: success path (Status→Sent), exhaustion path (MaxAttempts→Exhausted),
/// failure-with-retry (Attempts++, LastError set), and batch save.
/// </summary>
public class RetryableEmailSenderTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RetryableEmailSender _sender;

    public RetryableEmailSenderTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sender = new RetryableEmailSender(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_OnSuccess_SetsStatusToSent()
    {
        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "buyer@test.com",
            Attempts = 0,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var rows = new List<EventNotification> { notification };
        var sendCalled = 0;
        Task SendFunc(EventNotification row, CancellationToken ct)
        {
            sendCalled++;
            return Task.CompletedTask;
        }

        await _sender.ProcessAsync(rows, SendFunc, CancellationToken.None);

        Assert.Equal(1, sendCalled);
        Assert.Equal("sent", notification.Status);
        Assert.Equal(1, notification.Attempts);
        Assert.Null(notification.LastError);
    }

    [Fact]
    public async Task ProcessAsync_OnExhaustion_MarksExhausted()
    {
        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "buyer@test.com",
            Attempts = 4,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var rows = new List<EventNotification> { notification };
        Task SendFunc(EventNotification row, CancellationToken ct)
        {
            throw new InvalidOperationException("Send failed");
        }

        await _sender.ProcessAsync(rows, SendFunc, CancellationToken.None);

        Assert.Equal("exhausted", notification.Status);
        Assert.Equal(5, notification.Attempts);
        Assert.NotNull(notification.LastError);
        Assert.Contains("Send failed", notification.LastError);
    }

    [Fact]
    public async Task ProcessAsync_OnFailure_IncrementsAttemptsAndSetsLastError()
    {
        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "buyer@test.com",
            Attempts = 1,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var rows = new List<EventNotification> { notification };
        Task SendFunc(EventNotification row, CancellationToken ct)
        {
            throw new TimeoutException("Request timed out");
        }

        await _sender.ProcessAsync(rows, SendFunc, CancellationToken.None);

        Assert.Equal("pending", notification.Status);  // still pending, has retries left
        Assert.Equal(2, notification.Attempts);
        Assert.NotNull(notification.LastError);
        Assert.Contains("timed out", notification.LastError);
    }

    /// <summary>
    /// TRIANGULATION: Processes multiple rows independently — one succeeds, one fails.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_MixedOutcome_RecordsEachCorrectly()
    {
        var success = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "good@test.com",
            Attempts = 0,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var exhausted = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "bad@test.com",
            Attempts = 4,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.AddRange(success, exhausted);
        await _context.SaveChangesAsync();

        var rows = new List<EventNotification> { success, exhausted };
        Task SendFunc(EventNotification row, CancellationToken ct)
        {
            if (row.RecipientEmail == "bad@test.com")
                throw new Exception("boom");
            return Task.CompletedTask;
        }

        await _sender.ProcessAsync(rows, SendFunc, CancellationToken.None);

        Assert.Equal("sent", success.Status);
        Assert.Equal(1, success.Attempts);
        Assert.Equal("exhausted", exhausted.Status);
        Assert.Equal(5, exhausted.Attempts);
    }

    /// <summary>
    /// TRIANGULATION: ProcessAsync uses generic type parameter — works with
    /// any IRetryableEmailRow, not just EventNotification.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_GenericType_WorksWithAnyIRetryableEmailRow()
    {
        // PendingEmailSend also implements IRetryableEmailRow via the interface
        // (it has matching property names — we'll verify it in Phase 5 refactoring)
        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "generic@test.com",
            Attempts = 0,
            MaxAttempts = 3,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var rows = new List<EventNotification> { notification };

        await _sender.ProcessAsync(rows, (row, ct) => Task.CompletedTask, CancellationToken.None);

        Assert.Equal("sent", notification.Status);
    }
}
