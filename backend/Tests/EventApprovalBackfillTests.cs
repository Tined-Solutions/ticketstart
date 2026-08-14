using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// RED tests for the EA-006 legacy event backfill.
/// The migration's Up() MUST set ALL pre-existing events (expired included) to
/// Status = Approved, best-effort: a failure logs and continues (never aborts
/// the schema migration). The backfill is InMemory-testable.
/// </summary>
public class EventApprovalBackfillTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public EventApprovalBackfillTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Event SeedEvent(DateTime date, string name = "Legacy Event", EventStatus status = EventStatus.Pending)
    {
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Legacy description",
            Date = date,
            Location = "Venue",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = date,
            UpdatedAt = date,
            Status = status
        };
        _context.Events.Add(eventEntity);
        _context.SaveChanges();
        return eventEntity;
    }

    [Fact]
    public async Task RunAsync_AllExistingEvents_BecomeApproved()
    {
        // Arrange — a mix of future, past (expired) and already-approved events.
        // EA-006 backfill scope = ALL rows, expired included.
        var future = SeedEvent(DateTime.UtcNow.AddDays(30), "Future");
        var past = SeedEvent(DateTime.UtcNow.AddDays(-5), "Expired");
        var alreadyApproved = SeedEvent(DateTime.UtcNow.AddDays(2), "Approved", EventStatus.Approved);

        // Act
        await EventApprovalBackfill.RunAsync(_context);

        // Assert — every pre-existing event is Approved
        var statuses = _context.Events.ToDictionary(e => e.Id, e => e.Status);
        Assert.Equal(EventStatus.Approved, statuses[future.Id]);
        Assert.Equal(EventStatus.Approved, statuses[past.Id]);
        Assert.Equal(EventStatus.Approved, statuses[alreadyApproved.Id]);
    }

    [Fact]
    public async Task RunAsync_EmptyDatabase_NoOp()
    {
        // Act — must not throw on an empty table
        await EventApprovalBackfill.RunAsync(_context);

        // Assert
        Assert.Empty(_context.Events);
    }
}
