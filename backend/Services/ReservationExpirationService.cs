using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Background service that cleans up expired reservations.
/// Passive worker: its ONLY purpose is to flip the state of reservations whose
/// ExpiresAt is in the past from Active to Expired. It does NOT control stock —
/// availability is computed mathematically from active, unexpired reservations.
/// Validates: Requirements 4.5, 4.6, 4.7 and Batch 3 REQ-9, REQ-10.
/// </summary>
public class ReservationExpirationService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationExpirationService> _logger;
    private Task? _executeTask;
    private CancellationTokenSource? _cts;

    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromMinutes(1);

    public ReservationExpirationService(
        IServiceProvider serviceProvider,
        ILogger<ReservationExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts the background service. Only the PeriodicTimer-based ExecuteAsync loop runs.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reservation Expiration Service starting. Interval: {Interval}", PeriodicInterval);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executeTask = ExecuteAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// PeriodicTimer-based execution loop for passive reservation expiry cleanup.
    /// Marks expired active reservations as Expired. Runs every minute and stops
    /// gracefully on cancellation.
    /// NOTE (pending): a real-time "reservation expired" notification channel does not
    /// exist yet. When one is introduced, this loop should notify for records with
    /// ExpiresAt &lt; NOW() — currently it only performs the state cleanup.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Periodic expiry-cleanup loop started (interval: {Interval})", PeriodicInterval);

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
            _logger.LogInformation("Periodic expiry-cleanup loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in periodic expiry-cleanup loop");
        }
    }

    /// <summary>
    /// Marks expired active reservations as Expired. No stock is touched — expired
    /// reservations automatically stop counting toward the mathematical availability.
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

            _logger.LogInformation("Processing {Count} expired reservations for state cleanup", expiredReservations.Count);

            foreach (var reservation in expiredReservations)
            {
                // Passive cleanup: only the visual state changes. No stock counter exists.
                reservation.Status = ReservationStatus.Expired;

                _logger.LogInformation("Marked reservation {ReservationId} as expired ({Quantity} tickets of type {TicketTypeId})",
                    reservation.Id, reservation.Quantity, reservation.TicketTypeId);
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Expired state cleanup completed for {Count} reservations", expiredReservations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expired reservations — continuing to next cycle");
        }
    }

    /// <summary>
    /// Stops the PeriodicTimer loop.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reservation Expiration Service stopping...");

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
        _cts?.Dispose();
    }
}
