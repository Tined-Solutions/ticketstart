using TicketeraOnline.Api.Models;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Tests for EventNotification entity — validates defaults, NotificationType
/// enum values, and IRetryableEmailRow contract conformance.
/// </summary>
public class EventNotificationTests
{
    [Fact]
    public void EventNotification_ImplementsIRetryableEmailRow()
    {
        var notification = new EventNotification();

        Assert.IsAssignableFrom<IRetryableEmailRow>(notification);
    }

    [Fact]
    public void EventNotification_Defaults_StatusIsPending()
    {
        var notification = new EventNotification();

        Assert.Equal("pending", notification.Status);
    }

    [Fact]
    public void EventNotification_Defaults_AttemptsIsZero()
    {
        var notification = new EventNotification();

        Assert.Equal(0, notification.Attempts);
    }

    [Fact]
    public void EventNotification_Defaults_MaxAttemptsIsFive()
    {
        var notification = new EventNotification();

        Assert.Equal(5, notification.MaxAttempts);
    }

    [Fact]
    public void EventNotification_Defaults_NotificationTypeIsDateChange()
    {
        var notification = new EventNotification();

        Assert.Equal("DateChange", notification.NotificationType);
    }

    [Fact]
    public void EventNotification_SetsAllProperties()
    {
        var now = DateTime.UtcNow;
        var eventId = Guid.NewGuid();
        var newDate = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc);

        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            NotificationType = "DateChange",
            NewDate = newDate,
            RecipientEmail = "buyer@test.com",
            Status = "sent",
            Attempts = 1,
            MaxAttempts = 5,
            LastError = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(eventId, notification.EventId);
        Assert.Equal("DateChange", notification.NotificationType);
        Assert.Equal(newDate, notification.NewDate);
        Assert.Equal("buyer@test.com", notification.RecipientEmail);
        Assert.Equal("sent", notification.Status);
        Assert.Equal(1, notification.Attempts);
        Assert.Equal(5, notification.MaxAttempts);
        Assert.Null(notification.LastError);
        Assert.Equal(now, notification.CreatedAt);
        Assert.Equal(now, notification.UpdatedAt);
    }

    [Fact]
    public void IRetryableEmailRow_Properties_AreAccessible()
    {
        IRetryableEmailRow row = new EventNotification
        {
            Attempts = 2,
            MaxAttempts = 3,
            Status = "failed",
            LastError = "Test error",
            RecipientEmail = "test@example.com"
        };

        Assert.Equal(2, row.Attempts);
        Assert.Equal(3, row.MaxAttempts);
        Assert.Equal("failed", row.Status);
        Assert.Equal("Test error", row.LastError);
        Assert.Equal("test@example.com", row.RecipientEmail);
    }

    /// <summary>
    /// TRIANGULATION: EventNotification created with different NotificationType discriminator
    /// proves the entity is extensible (OCP per EDC-007).
    /// </summary>
    [Fact]
    public void EventNotification_SupportsLocationChangeType()
    {
        var notification = new EventNotification
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            NotificationType = "LocationChange",
            RecipientEmail = "buyer@test.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal("LocationChange", notification.NotificationType);
    }

    /// <summary>
    /// TRIANGULATION: Ensure EventNotification works with all Status values.
    /// </summary>
    [Fact]
    public void EventNotification_SupportsAllStatusValues()
    {
        Assert.Equal("pending", new EventNotification().Status);
        Assert.Equal("sent", new EventNotification { Status = "sent" }.Status);
        Assert.Equal("failed", new EventNotification { Status = "failed" }.Status);
    }
}
