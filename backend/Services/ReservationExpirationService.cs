using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Background service that continuously monitors and releases expired reservations.
/// Runs as an IHostedService to automatically restore ticket inventory from expired reservations.
/// Validates: Requirements 4.5, 4.6, 4.7
/// </summary>
public class ReservationExpirationService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationExpirationService> _logger;
    private Timer? _timer;

    // Check for expired reservations every 30 seconds
    private const int CheckIntervalSeconds = 30;

    public ReservationExpirationService(
        IServiceProvider serviceProvider,
        ILogger<ReservationExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts the background service and initializes the timer to check for expired reservations.
    /// Validates: Requirement 4.6
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reservation Expiration Service starting. Will check for expired reservations every {Interval} seconds.",
            CheckIntervalSeconds);

        // Initialize timer to run immediately and then every 30 seconds
        _timer = new Timer(
            CheckExpiredReservations,
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(CheckIntervalSeconds));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Timer callback that checks for and releases expired reservations.
    /// Creates a new scope to resolve scoped services (IReservationService) safely.
    /// Validates: Requirement 4.7
    /// </summary>
    private async void CheckExpiredReservations(object? state)
    {
        _logger.LogDebug("Checking for expired reservations...");

        try
        {
            // Create a new scope to resolve scoped services
            using var scope = _serviceProvider.CreateScope();
            var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();

            // Call the service to release expired reservations
            var releasedCount = await reservationService.ReleaseExpiredReservationsAsync();

            if (releasedCount > 0)
            {
                _logger.LogInformation("Released {Count} expired reservations and restored inventory", releasedCount);
            }
            else
            {
                _logger.LogDebug("No expired reservations to release");
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the service - it should continue checking
            _logger.LogError(ex, "Error occurred while checking for expired reservations");
        }
    }

    /// <summary>
    /// Stops the background service and disposes the timer.
    /// Validates: Requirement 4.7
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reservation Expiration Service stopping...");

        // Dispose the timer to stop checking for expired reservations
        _timer?.Change(Timeout.Infinite, 0);

        _logger.LogInformation("Reservation Expiration Service stopped");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes resources used by the service.
    /// </summary>
    public void Dispose()
    {
        _timer?.Dispose();
    }
}
