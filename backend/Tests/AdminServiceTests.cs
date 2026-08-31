using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// RED tests for the past-event immutability guard at the ADMIN SERVICE layer
/// (EA-003/EA-004 MODIFIED, D-2/ADR-7): Approve/Reject MUST throw
/// <see cref="EventFinalizedException"/> on a past-dated event BEFORE any status
/// flip, and future-dated events MUST still flip. AdminService receives the
/// TimeProvider through its constructor (D-8).
/// </summary>
public class AdminServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly FakeTimeProvider _clock;

    public AdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private AdminService CreateService() => new(
        _context,
        new TestLogger<AdminService>(),
        _clock);

    private async Task<Event> SeedEvent(DateTime date, EventStatus status = EventStatus.Pending)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Description",
            Date = date,
            Location = "Venue",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
            UpdatedAt = _clock.GetUtcNow().UtcDateTime,
            Status = status
        };
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        return evt;
    }

    private async Task<User> SeedUser(UserRole role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = $"user-{Guid.NewGuid()}@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = role,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    #region EA-003 MODIFIED — ApproveEventAsync

    [Fact]
    public async Task ApproveEventAsync_PastEvent_ThrowsEventFinalized_NoStatusFlip()
    {
        // GIVEN a pending event whose Date (T-2d) has already passed
        var evt = await SeedEvent(_clock.GetUtcNow().UtcDateTime.AddDays(-2));
        var service = CreateService();

        // WHEN an admin approves it
        // THEN it throws EventFinalizedException and Status stays Pending (no audit-worthy mutation)
        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            service.ApproveEventAsync(evt.Id));

        var persisted = await _context.Events.AsNoTracking().SingleAsync(e => e.Id == evt.Id);
        Assert.Equal(EventStatus.Pending, persisted.Status);
    }

    [Fact]
    public async Task ApproveEventAsync_FutureEvent_StillFlipsToApproved()
    {
        // EA-003 (admin-approves): future events keep flipping as before.
        var evt = await SeedEvent(_clock.GetUtcNow().UtcDateTime.AddDays(10));
        var service = CreateService();

        var summary = await service.ApproveEventAsync(evt.Id);

        Assert.Equal(EventStatus.Approved, summary.Status);
        var persisted = await _context.Events.AsNoTracking().SingleAsync(e => e.Id == evt.Id);
        Assert.Equal(EventStatus.Approved, persisted.Status);
    }

    #endregion

    #region EA-004 MODIFIED — RejectEventAsync

    [Fact]
    public async Task RejectEventAsync_PastEvent_ThrowsEventFinalized_NoStatusFlip()
    {
        // GIVEN a pending event whose Date (T-2d) has already passed
        var evt = await SeedEvent(_clock.GetUtcNow().UtcDateTime.AddDays(-2));
        var service = CreateService();

        // WHEN an admin rejects it (with an optional reason)
        // THEN it throws EventFinalizedException and Status stays Pending
        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            service.RejectEventAsync(evt.Id, "too late"));

        var persisted = await _context.Events.AsNoTracking().SingleAsync(e => e.Id == evt.Id);
        Assert.Equal(EventStatus.Pending, persisted.Status);
    }

    [Fact]
    public async Task RejectEventAsync_FutureEvent_StillFlipsToRejected()
    {
        // EA-004 (reject-without-reason): future events keep flipping as before.
        var evt = await SeedEvent(_clock.GetUtcNow().UtcDateTime.AddDays(10));
        var service = CreateService();

        var summary = await service.RejectEventAsync(evt.Id, null);

        Assert.Equal(EventStatus.Rejected, summary.Status);
        var persisted = await _context.Events.AsNoTracking().SingleAsync(e => e.Id == evt.Id);
        Assert.Equal(EventStatus.Rejected, persisted.Status);
    }

    #endregion

    #region AUM-001 — UpdateUserRoleAsync (D7)

    [Fact]
    public async Task UpdateUserRoleAsync_ExistingUser_PersistsRoleAndReturnsSummary()
    {
        // GIVEN an existing user with role Staff
        var user = await SeedUser(UserRole.Staff);
        var service = CreateService();

        // WHEN the admin updates the role to Organizador
        var summary = await service.UpdateUserRoleAsync(user.Id, UserRole.Organizador);

        // THEN the returned summary reflects the new role and the row is persisted
        Assert.Equal(user.Id, summary.Id);
        Assert.Equal(user.Email, summary.Email);
        Assert.Equal(UserRole.Organizador, summary.Role);

        var persisted = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(UserRole.Organizador, persisted.Role);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_UnknownUser_ThrowsKeyNotFoundException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateUserRoleAsync(Guid.NewGuid(), UserRole.Admin));
    }

    #endregion
}