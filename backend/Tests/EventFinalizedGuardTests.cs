using Microsoft.Extensions.Time.Testing;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services.Guards;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for the past-event immutability guard (PEM-001 / D-3): pure helper,
/// no DI, no DB — FakeTimeProvider drives the clock (dotnet-testing: pure helper → xUnit unit test).
/// The guard MUST evaluate Event.IsExpired on a MATERIALIZED entity with the injected
/// clock; an expired event throws EventFinalizedException before any mutation proceeds.
/// </summary>
public class EventFinalizedGuardTests
{
    private static readonly DateTime FrozenNow = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Event CreateEvent(DateTime date) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Event",
        Date = date,
        Location = "Venue",
        OrganizerId = Guid.NewGuid()
    };

    private static FakeTimeProvider FrozenClock() => new(new DateTimeOffset(FrozenNow));

    [Fact]
    public void EnsureMutable_ExpiredEvent_ThrowsEventFinalizedException()
    {
        // GIVEN a materialized event whose Date is BEFORE the frozen clock instant
        var pastEvent = CreateEvent(FrozenNow.AddDays(-2));

        // WHEN the guard is evaluated (PEM-001 expired-throws)
        // THEN it throws EventFinalizedException and no mutation proceeds
        var ex = Assert.Throws<EventFinalizedException>(() =>
            EventFinalizedGuard.EnsureMutable(pastEvent, FrozenClock()));

        // D-1: the message doubles as the ProblemDetails title
        Assert.Equal("Event has already finished", ex.Message);
    }

    [Fact]
    public void EnsureMutable_ActiveEvent_DoesNotThrow()
    {
        // GIVEN a materialized event whose Date is AFTER the frozen clock instant
        var futureEvent = CreateEvent(FrozenNow.AddDays(1));

        // WHEN the guard is evaluated (PEM-001 active-passes)
        // THEN it returns without throwing
        EventFinalizedGuard.EnsureMutable(futureEvent, FrozenClock());
    }

    [Fact]
    public void EnsureMutable_ExactInstant_DoesNotThrow()
    {
        // GIVEN an event at the exact same instant as the frozen clock (strict <,
        // not <= — mirrors EventExpiryTests.Event_IsExpired_ExactInstant_False)
        var exactEvent = CreateEvent(FrozenNow);

        // WHEN the guard is evaluated (PEM-001 exact-instant-mutable)
        // THEN it returns without throwing
        EventFinalizedGuard.EnsureMutable(exactEvent, FrozenClock());
    }
}