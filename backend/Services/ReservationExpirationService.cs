using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Background service that monitors and releases expired reservations.
/// Uses PeriodicTimer for reliable scheduling and ExecuteUpdateAsync for atomic stock restoration.
/// Validates: Requirements 4.5, 4.6, 4.7 and Batch 3 REQ-9, REQ-10.
/// </summary>
public class ReservationExpirationService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationExpirationService> _logger;
    private Timer? _timer;
    private Task? _executeTask;
    private CancellationTokenSource? _cts;

    private const int CheckIntervalSeconds = 30;
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromMinutes(1);

    public ReservationExpirationService(
        IServiceProvider serviceProvider,
        ILogger<ReservationExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts the background service using the legacy Timer-based approach.
    /// Also starts the PeriodicTimer-based ExecuteAsync loop for atomic stock restoration.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reservation Expiration Service starting. Check interval: {Interval}s", CheckIntervalSeconds);

        _timer = new Timer(CheckExpiredReservations, null, TimeSpan.Zero, TimeSpan.FromSeconds(CheckIntervalSeconds));

        // Start the PeriodicTimer-based loop for atomic stock restoration
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executeTask = ExecuteAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// PeriodicTimer-based execution loop for atomic stock restoration.
    /// Decrements CurrentlyReserved on TicketType for expired reservations using ExecuteUpdateAsync.
    /// Runs every minute and stops gracefully on cancellation.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Periodic stock-restoration loop started (interval: {Interval})", PeriodicInterval);

        using var timer = new PeriodicTimer(PeriodicInterval);

        try
        {
            // Process immediately on startup, then every interval
            await ProcessExpiredReservationsAsync();

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessExpiredReservationsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Periodic stock-restoration loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in periodic stock-restoration loop");
        }
    }

    /// <summary>
    /// Processes expired reservations by marking them as Expired and atomically
    /// decrementing CurrentlyReserved on the corresponding TicketType.
    /// Uses ExecuteUpdateAsync to avoid race conditions.
    /// </summary>
    private async Task ProcessExpiredReservationsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            // Find expired active reservations
            var expiredReservations = await context.Reservations
                .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now)
                .ToListAsync();

            if (expiredReservations.Count == 0)
            {
                _logger.LogDebug("No expired reservations to process");
                return;
            }

            _logger.LogInformation("Processing {Count} expired reservations for stock restoration", expiredReservations.Count);

            foreach (var reservation in expiredReservations)
            {
                // Atomically decrement CurrentlyReserved, clamped to zero
                await context.TicketTypes
                    .Where(tt => tt.Id == reservation.TicketTypeId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(tt => tt.CurrentlyReserved,
                            tt => Math.Max(0, tt.CurrentlyReserved - reservation.Quantity)));

                // Mark the reservation as expired
                reservation.Status = ReservationStatus.Expired;

                _logger.LogInformation("Released {Quantity} tickets of type {TicketTypeId} from expired reservation {ReservationId}",
                    reservation.Quantity, reservation.TicketTypeId, reservation.Id);
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Stock restored for {Count} expired reservations", expiredReservations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expired reservations — continuing to next cycle");
        }
    }

    /// <summary>
    /// Legacy Timer callback that marks expired reservations (compatibility with InMemory tests).
    /// </summary>
    private async void CheckExpiredReservations(object? state)
    {
        _logger.LogDebug("Checking for expired reservations...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();
            var releasedCount = await reservationService.ReleaseExpiredReservationsAsync();

            if (releasedCount > 0)
            {
                _logger.LogInformation("Released {Count} expired reservations (legacy timer)", releasedCount);
            }
            else
            {
                _logger.LogDebug("No expired reservations to release");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in legacy timer callback — continuing");
        }
    }

    /// <summary>
    /// Stops both the legacy timer and the PeriodicTimer loop.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reservation Expiration Service stopping...");

        _timer?.Change(Timeout.Infinite, 0);

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

        _logger.LogInformation("Reservation Expiration Service stopped");
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }
}
