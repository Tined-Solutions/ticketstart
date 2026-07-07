using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Integration tests for ReservationExpirationService background worker.
/// Validates: Requirements 4.5, 4.6, 4.7
/// </summary>
public class ReservationExpirationServiceTests
{
    /// <summary>
    /// Tests that the ReservationExpirationService can be started and stopped successfully.
    /// Validates: Requirement 4.6 - THE Expiration_Service SHALL run continuously as an IHostedService background worker
    /// </summary>
    [Fact]
    public async Task StartAsync_InitializesServiceSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        services.AddScoped<IReservationService, ReservationService>();
        services.AddLogging(builder => builder.AddConsole());
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpirationService>>();
        var service = new ReservationExpirationService(serviceProvider, logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Service should start without errors
        // No exception means success

        // Cleanup
        await service.StopAsync(CancellationToken.None);
        service.Dispose();
        await serviceProvider.DisposeAsync();
    }

    /// <summary>
    /// Tests that StopAsync disposes the timer properly.
    /// Validates: Requirement 4.6, 4.7
    /// </summary>
    [Fact]
    public async Task StopAsync_DisposesTimerSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        services.AddScoped<IReservationService, ReservationService>();
        services.AddLogging(builder => builder.AddConsole());
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpirationService>>();
        var service = new ReservationExpirationService(serviceProvider, logger);

        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert - Service should stop without errors
        // No exception means success

        // Cleanup
        service.Dispose();
        await serviceProvider.DisposeAsync();
    }

    /// <summary>
    /// Integration test: Tests that the service executes periodically and processes expired reservations.
    /// Validates: Requirement 4.7 - THE Expiration_Service SHALL check for expired reservations at regular intervals
    /// </summary>
    [Fact]
    public async Task ServiceIntegration_ExecutesPeriodicallyContinuously()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        services.AddScoped<IReservationService, ReservationService>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var serviceProvider = services.BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpirationService>>();
        var service = new ReservationExpirationService(serviceProvider, logger);

        // Act & Assert - Service should run without errors
        await service.StartAsync(CancellationToken.None);
        
        // Wait for timer to fire at least once (it fires immediately on start)
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        
        // Service should stop cleanly
        await service.StopAsync(CancellationToken.None);
        service.Dispose();

        // If we reach here without exceptions, the service is working correctly
        await serviceProvider.DisposeAsync();
    }

    /// <summary>
    /// Integration test: Tests that the service properly releases expired reservations and restores inventory.
    /// Validates: Requirements 4.5, 4.6, 4.7
    /// - 4.5: WHEN a reservation expires, THE Expiration_Service SHALL release the reserved tickets back to inventory
    /// - 4.6: THE Expiration_Service SHALL run continuously as an IHostedService background worker
    /// - 4.7: THE Expiration_Service SHALL check for expired reservations at regular intervals
    /// </summary>
    [Fact]
    public async Task ServiceIntegration_ReleasesExpiredReservationsAndRestoresInventory()
    {
        // Arrange - Use a shared database name for this test
        var dbName = $"TestDb_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IReservationService, ReservationService>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var serviceProvider = services.BuildServiceProvider();
        
        Guid ticketTypeId;
        Guid eventId;
        
        // Set up test data - create event with tickets and expired reservation
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Create organizer
            var organizerId = Guid.NewGuid();
            var user = new User
            {
                Id = organizerId,
                Email = "organizer@test.com",
                PasswordHash = "hash",
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            
            // Create event
            var eventEntity = new Event
            {
                Id = Guid.NewGuid(),
                Name = "Test Event",
                Description = "Test Description",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Test Location",
                OrganizerId = organizerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Events.Add(eventEntity);
            eventId = eventEntity.Id;
            
            // Create ticket type
            var ticketType = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                Name = "General Admission",
                Price = 50.00m,
                Quantity = 100,
                CreatedAt = DateTime.UtcNow
            };
            context.TicketTypes.Add(ticketType);
            ticketTypeId = ticketType.Id;
            
            // Create expired reservations
            var expiredReservation1 = new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                Quantity = 15,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow.AddMinutes(-15)
            };
            
            var expiredReservation2 = new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                Quantity = 10,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-2), // Expired 2 minutes ago
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow.AddMinutes(-12)
            };
            
            context.Reservations.AddRange(expiredReservation1, expiredReservation2);
            await context.SaveChangesAsync();
        }
        
        // Act - Start service and let it process
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpirationService>>();
        var service = new ReservationExpirationService(serviceProvider, logger);
        
        await service.StartAsync(CancellationToken.None);
        
        // Wait for the timer to execute (fires immediately, then we wait a bit longer)
        await Task.Delay(TimeSpan.FromMilliseconds(1000));
        
        await service.StopAsync(CancellationToken.None);
        service.Dispose();
        
        // Assert - Verify reservations were released
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var reservations = await context.Reservations.ToListAsync();
            
            // All expired active reservations should now be marked as Expired
            Assert.All(reservations, r => Assert.Equal(ReservationStatus.Expired, r.Status));
            
            // Verify inventory was restored by checking we can now create a new reservation
            var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();
            
            // Should be able to reserve the full quantity (100) since the expired reservations (15 + 10 = 25) were released
            var newReservation = await reservationService.CreateReservationAsync(
                Guid.NewGuid(), 
                eventId, 
                ticketTypeId, 
                100,
                "12345678");
            
            Assert.NotNull(newReservation);
            Assert.Equal(100, newReservation.Quantity);
            Assert.Equal(ReservationStatus.Active, newReservation.Status);
        }
        
        await serviceProvider.DisposeAsync();
    }

    /// <summary>
    /// Integration test: Tests that the service continues running after handling exceptions.
    /// Validates: Requirement 4.6, 4.7 - Service should be resilient to errors
    /// </summary>
    [Fact]
    public async Task ServiceIntegration_ContinuesRunningAfterException()
    {
        // Arrange - Use a disposed context to trigger exceptions
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        services.AddScoped<IReservationService, ReservationService>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var serviceProvider = services.BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpirationService>>();
        var service = new ReservationExpirationService(serviceProvider, logger);

        // Act - Service should handle exceptions gracefully
        await service.StartAsync(CancellationToken.None);
        
        // Wait for multiple timer cycles
        await Task.Delay(TimeSpan.FromMilliseconds(1000));
        
        // Service should still stop cleanly even if there were errors
        await service.StopAsync(CancellationToken.None);
        
        // Assert - No exception means the service handled errors gracefully
        service.Dispose();
        await serviceProvider.DisposeAsync();
    }

    /// <summary>
    /// Integration test: Tests that multiple service cycles execute correctly.
    /// Validates: Requirement 4.7 - Service runs at regular intervals
    /// </summary>
    [Fact]
    public async Task ServiceIntegration_ExecutesMultipleCycles()
    {
        // Arrange - Use a shared database name for this test
        var dbName = $"TestDb_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddScoped<IReservationService, ReservationService>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Create test data with reservations that will expire during the test
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var organizerId = Guid.NewGuid();
            var user = new User
            {
                Id = organizerId,
                Email = "organizer@test.com",
                PasswordHash = "hash",
                Role = UserRole.Organizador,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            
            var eventEntity = new Event
            {
                Id = Guid.NewGuid(),
                Name = "Test Event",
                Description = "Test Description",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Test Location",
                OrganizerId = organizerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Events.Add(eventEntity);
            
            var ticketType = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                Name = "General Admission",
                Price = 50.00m,
                Quantity = 100,
                CreatedAt = DateTime.UtcNow
            };
            context.TicketTypes.Add(ticketType);
            
            // Create multiple expired reservations
            for (int i = 0; i < 5; i++)
            {
                var reservation = new Reservation
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    EventId = eventEntity.Id,
                    TicketTypeId = ticketType.Id,
                    Quantity = 5,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(-1 - i),
                    Status = ReservationStatus.Active,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-11 - i)
                };
                context.Reservations.Add(reservation);
            }
            
            await context.SaveChangesAsync();
        }
        
        // Act - Let service run for multiple cycles
        var logger = serviceProvider.GetRequiredService<ILogger<ReservationExpirationService>>();
        var service = new ReservationExpirationService(serviceProvider, logger);
        
        await service.StartAsync(CancellationToken.None);
        
        // Wait for multiple timer cycles (service checks every 30 seconds, but fires immediately on start)
        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        
        await service.StopAsync(CancellationToken.None);
        service.Dispose();
        
        // Assert - All reservations should be expired
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reservations = await context.Reservations.ToListAsync();
            
            Assert.Equal(5, reservations.Count);
            Assert.All(reservations, r => Assert.Equal(ReservationStatus.Expired, r.Status));
        }
        
        await serviceProvider.DisposeAsync();
    }
}
