using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// ADR-3 regression: EventService reads "now" exclusively through the injected
/// TimeProvider. With a frozen FakeTimeProvider, CreateEventAsync is fully
/// deterministic — past dates rejected, future dates accepted — with no
/// real-clock bleed-through.
///
/// The frozen instant (2030) is deliberately far from real time so that, against
/// the pre-migration real clock, at least one of the two tests always fails:
/// past-date (2030-12-31 vs real now → no exception) or future-date (2030-01-01
/// vs real now → spurious rejection). Post-migration both are green always.
/// </summary>
public class EventClockTests : IDisposable
{
    private static readonly DateTime FrozenNow = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public EventClockTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var configurationData = new Dictionary<string, string?>
        {
            { "CloudflareR2:BucketName", "test-bucket" },
            { "CloudflareR2:PublicUrl", "https://test.r2.dev" }
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationData).Build();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private EventService CreateService(TimeProvider clock) => new(
        _context,
        new TestLogger<EventService>(),
        _configuration,
        new Mock<IAmazonS3>().Object,
        new Mock<IEventNotificationQueue>().Object,
        clock,
        Options.Create(new HideExpiredEventsOptions()));

    private static CreateEventRequest CreateRequest(DateTime date) => new()
    {
        Name = "Frozen Clock Event",
        Description = "ADR-3 deterministic creation",
        Date = date,
        Location = "Venue",
        ImageUrl = "",
        TicketTypes = new List<CreateTicketTypeRequest>
        {
            new() { Name = "General", Price = 100m, Quantity = 50 }
        }
    };

    [Fact]
    public async Task CreateEvent_PastDate_FrozenClock_AnyException()
    {
        // Arrange: clock frozen at 2030-01-01T00:00:00Z
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(FrozenNow);
        var service = CreateService(fake);

        var request = CreateRequest(FrozenNow.AddSeconds(-1));

        // Act & Assert: 1s before "now" → rejected regardless of real wall time
        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateEventAsync(request, Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateEvent_FutureDate_FrozenClock_Succeeds()
    {
        // Arrange: clock frozen at 2030-01-01T00:00:00Z
        var fake = new FakeTimeProvider();
        fake.SetUtcNow(FrozenNow);
        var service = CreateService(fake);

        var request = CreateRequest(FrozenNow.AddSeconds(1));

        // Act: 1s after "now" → accepted
        var result = await service.CreateEventAsync(request, Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Date, result.Date);
        Assert.Equal(DateTimeKind.Utc, result.CreatedAt.Kind);
    }
}
