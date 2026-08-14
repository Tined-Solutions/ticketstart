using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Tests for EventNotificationDispatchService (IHostedService BackgroundService).
/// Validates: polls EventNotification with Status=Pending, dispatches via
/// IRetryableEmailSender, respects batch size 50, handles empty result set.
/// </summary>
public class EventNotificationDispatchServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IRetryableEmailSender> _mockSender;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<EventNotificationDispatchService>> _mockLogger;
    private readonly ServiceProvider _serviceProvider;

    public EventNotificationDispatchServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _context = new ApplicationDbContext(options);
        _mockSender = new Mock<IRetryableEmailSender>();
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<EventNotificationDispatchService>>();

        // Default: email delivery succeeds. Tests that exercise the failure path
        // re-setup this mock with Success = false.
        _mockEmailService
            .Setup(e => e.SendEventDateChangeNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(new EmailResult { Success = true });

        var services = new ServiceCollection();
        // Create fresh contexts sharing the same InMemory database name.
        services.AddTransient<ApplicationDbContext>(_ =>
            new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options));
        services.AddSingleton(_mockSender.Object);
        // Register IRetryableEmailSender as singleton (resolved from scope at runtime)
        services.AddSingleton<IRetryableEmailSender>(_mockSender.Object);
        services.AddSingleton(_mockEmailService.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task ProcessPendingAsync_DispatchesPendingNotifications()
    {
        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "buyer@test.com",
            NewDate = new DateTime(2026, 11, 1),
            Attempts = 0,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var service = new EventNotificationDispatchService(
            _serviceProvider, _mockLogger.Object);

        await service.ProcessPendingAsync(CancellationToken.None);

        _mockSender.Verify(
            s => s.ProcessAsync(
                It.Is<IEnumerable<EventNotification>>(rows =>
                    rows.Any(r => r.Id == notification.Id)),
                It.IsAny<Func<EventNotification, CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_SkipsNonPendingRows()
    {
        var sent = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "sent@test.com",
            Status = "sent",
            Attempts = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var exhausted = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "DateChange",
            RecipientEmail = "exhausted@test.com",
            Status = "exhausted",
            Attempts = 5,
            MaxAttempts = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.AddRange(sent, exhausted);
        await _context.SaveChangesAsync();

        var service = new EventNotificationDispatchService(
            _serviceProvider, _mockLogger.Object);

        await service.ProcessPendingAsync(CancellationToken.None);

        // No rows dispatched — all are non-Pending
        _mockSender.Verify(
            s => s.ProcessAsync(
                It.IsAny<IEnumerable<EventNotification>>(),
                It.IsAny<Func<EventNotification, CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// TRIANGULATION: Multiple pending rows are dispatched in a single batch.
    /// </summary>
    [Fact]
    public async Task ProcessPendingAsync_MultiplePendingRows_DispatchesAsBatch()
    {
        for (int i = 0; i < 3; i++)
        {
            _context.EventNotifications.Add(new EventNotification
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                NotificationType = "DateChange",
                RecipientEmail = $"buyer{i}@test.com",
                NewDate = DateTime.UtcNow.AddDays(30),
                Status = "pending",
                Attempts = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        var service = new EventNotificationDispatchService(
            _serviceProvider, _mockLogger.Object);

        await service.ProcessPendingAsync(CancellationToken.None);

        _mockSender.Verify(
            s => s.ProcessAsync(
                It.Is<IEnumerable<EventNotification>>(rows => rows.Count() == 3),
                It.IsAny<Func<EventNotification, CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// RED/GREEN — Verifies the dispatch service passes the notification's
    /// EventName to the email service instead of a hardcoded value.
    /// EDC-003: Emails must contain the real event name.
    /// </summary>
    [Fact]
    public async Task ProcessPendingAsync_UsesEventNameFromNotification()
    {
        // Arrange: notification with a real event name
        var eventId = Guid.NewGuid();
        var oldDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        var newDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventName = "Rock Fest 2026",
            NotificationType = "DateChange",
            RecipientEmail = "buyer@test.com",
            OldDate = oldDate,
            NewDate = newDate,
            Attempts = 0,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.Add(notification);
        await _context.SaveChangesAsync();

        // Capture the sendFunc so we can invoke it and assert on the email call
        Func<EventNotification, CancellationToken, Task>? capturedSendFunc = null;

        _mockSender
            .Setup(s => s.ProcessAsync(
                It.IsAny<IEnumerable<EventNotification>>(),
                It.IsAny<Func<EventNotification, CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EventNotification>, Func<EventNotification, CancellationToken, Task>, CancellationToken>(
                (rows, sendFunc, ct) => capturedSendFunc = sendFunc);

        var service = new EventNotificationDispatchService(
            _serviceProvider, _mockLogger.Object);

        // Act
        await service.ProcessPendingAsync(CancellationToken.None);

        // Invoke the captured sendFunc to trigger the email call
        Assert.NotNull(capturedSendFunc);
        await capturedSendFunc!(notification, CancellationToken.None);

        // Assert: EmailService was called with the notification's EventName
        _mockEmailService.Verify(
            e => e.SendEventDateChangeNotificationAsync(
                notification.RecipientEmail,
                "Rock Fest 2026",
                oldDate,
                newDate,
                It.IsAny<string?>()),
            Times.Once);
    }

    /// <summary>
    /// EDC-004: an email delivery failure must surface as an exception so the
    /// IRetryableEmailSender state machine records attempts/LastError and retries
    /// (or exhausts) the row — instead of silently marking it "sent".
    /// </summary>
    [Fact]
    public async Task ProcessPendingAsync_EmailFailure_ThrowsToRetryStateMachine()
    {
        _mockEmailService
            .Setup(e => e.SendEventDateChangeNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(new EmailResult { Success = false, Error = "Resend rejected recipient" });

        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventName = "Rock Fest 2026",
            NotificationType = "DateChange",
            RecipientEmail = "buyer@test.com",
            NewDate = DateTime.UtcNow.AddDays(1),
            Attempts = 0,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EventNotifications.Add(notification);
        await _context.SaveChangesAsync();

        Func<EventNotification, CancellationToken, Task>? capturedSendFunc = null;
        _mockSender
            .Setup(s => s.ProcessAsync(
                It.IsAny<IEnumerable<EventNotification>>(),
                It.IsAny<Func<EventNotification, CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EventNotification>, Func<EventNotification, CancellationToken, Task>, CancellationToken>(
                (rows, sendFunc, ct) => capturedSendFunc = sendFunc);

        var service = new EventNotificationDispatchService(
            _serviceProvider, _mockLogger.Object);

        await service.ProcessPendingAsync(CancellationToken.None);

        Assert.NotNull(capturedSendFunc);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => capturedSendFunc!(notification, CancellationToken.None));
    }
}
