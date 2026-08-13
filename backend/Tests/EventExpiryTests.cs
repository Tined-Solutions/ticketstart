using TicketeraOnline.Api.Models;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Domain tests for EHE-001: Event.IsExpired(asOf) uses strict less-than
/// (Date &lt; asOf), so an event at the exact start instant is NOT expired.
/// Pure predicate tests — no DB, no mocks (dotnet-testing: pure helper → xUnit unit test).
/// </summary>
public class EventExpiryTests
{
    [Fact]
    public void Event_IsExpired_Future_False()
    {
        // GIVEN an event starting in the future relative to asOf
        var evt = new Event { Date = new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc) };

        // WHEN IsExpired is called with an earlier instant
        var result = evt.IsExpired(new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc));

        // THEN the event is not expired
        Assert.False(result);
    }

    [Fact]
    public void Event_IsExpired_Past_True()
    {
        // GIVEN an event whose start has already passed relative to asOf
        var evt = new Event { Date = new DateTime(2026, 8, 10, 14, 0, 0, DateTimeKind.Utc) };

        // WHEN IsExpired is called with a later instant
        var result = evt.IsExpired(new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc));

        // THEN the event is expired
        Assert.True(result);
    }

    [Fact]
    public void Event_IsExpired_ExactInstant_False()
    {
        // GIVEN an event and asOf at the exact same start instant
        var asOf = new DateTime(2026, 8, 12, 14, 0, 0, DateTimeKind.Utc);
        var evt = new Event { Date = asOf };

        // WHEN IsExpired is called with that exact instant
        var result = evt.IsExpired(asOf);

        // THEN the event is NOT expired (strict `<`, not `<=`)
        Assert.False(result);
    }

    [Fact]
    public void EventExpiredException_Message_EventHasAlreadyStarted()
    {
        // ADR-5: the exception message is the ProblemDetails title used by both
        // the controller catch and the GlobalExceptionHandler fallback.
        var ex = new EventExpiredException();
        Assert.Equal("Event has already started", ex.Message);
    }
}
