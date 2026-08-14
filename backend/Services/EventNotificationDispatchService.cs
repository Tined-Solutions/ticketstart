using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Background service that polls the event_notifications table every 30 seconds
/// for rows with Status=Pending, then dispatches them via IRetryableEmailSender.
/// Mirrors the ReservationExpirationService pattern (IHostedService, PeriodicTimer,
/// IServiceProvider.CreateScope, scoped DbContext, graceful shutdown).
///
/// The in-memory Resend retry (exponential backoff) is handled by
/// EmailService.SendWithRetryAsync. The DB row state machine is owned by
/// IRetryableEmailSender.ProcessAsync.
/// </summary>
public class EventNotificationDispatchService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventNotificationDispatchService> _logger;
    private Task? _executeTask;
    private CancellationTokenSource? _cts;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    public EventNotificationDispatchService(
        IServiceProvider serviceProvider,
        ILogger<EventNotificationDispatchService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Event Notification Dispatch Service starting. Poll interval: {Interval}, Batch size: {BatchSize}",
            PollInterval, BatchSize);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executeTask = ExecuteAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// PeriodicTimer-based execution loop. Processes immediately on startup,
    /// then every PollInterval.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Periodic notification-dispatch loop started (interval: {Interval})", PollInterval);

        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            await ProcessPendingAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessPendingAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Periodic notification-dispatch loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in periodic notification-dispatch loop");
        }
    }

    /// <summary>
    /// Queries pending EventNotification rows (up to BatchSize), then dispatches
    /// them through IRetryableEmailSender. Each row's sendFunc calls
    /// IEmailService.SendEventDateChangeNotificationAsync.
    /// </summary>
    internal async Task ProcessPendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var retryableSender = scope.ServiceProvider.GetRequiredService<IRetryableEmailSender>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var pending = await context.EventNotifications
                .Where(n => n.Status == "pending")
                .OrderBy(n => n.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(stoppingToken);

            if (pending.Count == 0)
            {
                _logger.LogDebug("No pending event notifications to process");
                return;
            }

            _logger.LogInformation(
                "Processing {Count} pending event notifications", pending.Count);

            await retryableSender.ProcessAsync(
                pending,
                async (notification, ct) =>
                {
                    var result = await emailService.SendEventDateChangeNotificationAsync(
                        notification.RecipientEmail,
                        notification.EventName,
                        notification.OldDate ?? notification.CreatedAt,
                        notification.NewDate ?? DateTime.UtcNow,
                        notification.RecipientName);

                    // Surface delivery failure as an exception so the shared
                    // IRetryableEmailSender state machine records attempts/LastError
                    // and retries (or exhausts) the row. EmailService returns a result
                    // object instead of throwing, so it must be translated here.
                    if (!result.Success)
                    {
                        throw new InvalidOperationException(
                            result.Error ?? "Event date change notification failed to send");
                    }
                },
                stoppingToken);

            _logger.LogInformation(
                "Dispatched {Count} pending event notifications", pending.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending event notifications — continuing to next cycle");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Event Notification Dispatch Service stopping...");

        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_executeTask != null)
        {
            try
            {
                await _executeTask;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping periodic loop");
            }
        }

        _logger.LogInformation("Event Notification Dispatch Service stopped");
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
