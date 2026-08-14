using FsCheck;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;
using GenStatic = FsCheck.Fluent.Gen;
using PropStatic = FsCheck.Fluent.Prop;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for admin capabilities.
/// Validates Requirements 14.1, 14.2, 14.3, 14.6
/// </summary>
public class AdminPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IAdminService _adminService;
    private readonly IAuditLogService _auditLogService;

    public AdminPropertyTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<AdminService>();

        _adminService = new AdminService(_context, logger);
        _auditLogService = new AuditLogService(_context, LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AuditLogService>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Property 42: Admin Access to All Events

    /// <summary>
    /// Property 42: Admin Access to All Events
    /// For any admin user, they SHALL have access to view all events regardless of ownership.
    /// **Validates: Requirements 14.1, 14.2, 14.3**
    /// </summary>
    [Fact]
    public void Property42_AdminAccess_ReturnsAllEventsRegardlessOfOwnership()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<AdminEventScenario>(AdminEventScenarioGen()),
            scenario =>
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                    .Options;

                using var context = new ApplicationDbContext(options);
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var adminService = new AdminService(context, loggerFactory.CreateLogger<AdminService>());

                context.Users.Add(scenario.Admin);
                context.Users.AddRange(scenario.Organizers);
                context.Events.AddRange(scenario.Events);
                context.SaveChanges();

                var result = Task.Run(() => adminService.GetAllEventsAsync(1, 200)).Result;
                var expectedIds = scenario.Events.Select(e => e.Id).ToHashSet();
                var actualIds = result.Items.Select(e => e.Id).ToHashSet();

                return result.Total == scenario.Events.Count && expectedIds.SetEquals(actualIds);
            });

        Check.QuickThrowOnFailure(prop);
    }

    /// <summary>
    /// Property 42 (Edge Case): No events returns empty collection.
    /// </summary>
    [Fact]
    public async Task GetAllEvents_NoEvents_ReturnsEmptyCollection()
    {
        // Act
        var result = await _adminService.GetAllEventsAsync(1, 50);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    /// <summary>
    /// Property 42 (Ownership): Events returned include organizer information.
    /// </summary>
    [Fact]
    public async Task GetAllEvents_ReturnsEventsWithOrganizerId()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Single Event");
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetAllEventsAsync(1, 50);

        // Assert
        var eventList = result.Items.ToList();
        Assert.Single(eventList);
        Assert.Equal(eventEntity.Id, eventList[0].Id);
        Assert.Equal(eventEntity.Name, eventList[0].Name);
        Assert.Equal(organizerId, eventList[0].OrganizerId);
    }

    /// <summary>
    /// Property 42 (Pagination cap): A requested page size above the 200-row hard cap is clamped to 200.
    /// </summary>
    [Fact]
    public async Task GetAllEvents_PageSizeOver200_IsCappedTo200()
    {
        // Act
        var result = await _adminService.GetAllEventsAsync(1, 500);

        // Assert
        Assert.Equal(200, result.PageSize);
    }

    #endregion

    #region Property 43: Admin Action Audit Logging

    /// <summary>
    /// Property 43: Admin Action Audit Logging
    /// For any admin action (view, modify, delete), the system SHALL log the action with timestamp, admin user ID, and action details.
    /// **Validates: Requirements 14.6**
    /// </summary>
    [Fact]
    public void Property43_AdminAction_AuditLogPersistsExactValuesAndIsRetrievable()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<AuditActionScenario>(AuditActionScenarioGen()),
            scenario =>
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                    .Options;

                using var context = new ApplicationDbContext(options);
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var auditLogService = new AuditLogService(context, loggerFactory.CreateLogger<AuditLogService>());

                var auditContext = new AuditLogContext(scenario.AdminId, scenario.Action, scenario.Resource, scenario.ResourceId, scenario.Details);
                Task.Run(() => auditLogService.LogActionAsync(auditContext)).Wait();

                var logs = context.AuditLogs.ToList();
                if (logs.Count != 1)
                {
                    return false;
                }

                var log = logs[0];
                var ok = log.UserId == scenario.AdminId
                         && log.ActionType == scenario.Action
                         && log.ResourceType == scenario.Resource
                         && log.ResourceId == scenario.ResourceId
                         && log.Details == scenario.Details
                         && log.Timestamp <= DateTime.UtcNow;
                return ok;
            });

        Check.QuickThrowOnFailure(prop);
    }

    /// <summary>
    /// Property 43 (Multiple Actions): Each admin action creates a distinct audit log entry.
    /// </summary>
    [Fact]
    public async Task LogAction_MultipleAdminActions_PersistsAllLogs()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        // Act
        await _auditLogService.LogActionAsync(new AuditLogContext(adminId, AuditActionType.ViewUsers, AuditResourceType.User, Guid.Empty));
        await _auditLogService.LogActionAsync(new AuditLogContext(adminId, AuditActionType.ViewEvents, AuditResourceType.Event, eventId));
        await _auditLogService.LogActionAsync(new AuditLogContext(adminId, AuditActionType.UpdateEvent, AuditResourceType.Event, eventId, "Updated event details"));

        // Assert
        var logs = _context.AuditLogs.OrderByDescending(l => l.Timestamp).ThenByDescending(l => l.Id).ToList();
        Assert.Equal(3, logs.Count);
        Assert.All(logs, log => Assert.Equal(adminId, log.UserId));
        Assert.Contains(logs, log => log.ActionType == AuditActionType.ViewUsers);
        Assert.Contains(logs, log => log.ActionType == AuditActionType.ViewEvents);
        Assert.Contains(logs, log => log.ActionType == AuditActionType.UpdateEvent);
    }

    /// <summary>
    /// Property 43 (Different Admins): Audit logs distinguish between different admin users.
    /// </summary>
    [Fact]
    public async Task LogAction_DifferentAdmins_PersistsUserSpecificLogs()
    {
        // Arrange
        var admin1Id = Guid.NewGuid();
        var admin2Id = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        // Act
        await _auditLogService.LogActionAsync(new AuditLogContext(admin1Id, AuditActionType.DeleteEvent, AuditResourceType.Event, eventId));
        await _auditLogService.LogActionAsync(new AuditLogContext(admin2Id, AuditActionType.ViewEvents, AuditResourceType.Event, eventId));

        // Assert
        var logs = _context.AuditLogs.ToList();
        Assert.Equal(2, logs.Count);
        Assert.Single(logs, log => log.UserId == admin1Id && log.ActionType == AuditActionType.DeleteEvent);
        Assert.Single(logs, log => log.UserId == admin2Id && log.ActionType == AuditActionType.ViewEvents);
    }

    /// <summary>
    /// Property 43 (Null ResourceId): Audit log round-trips a null resource ID.
    /// </summary>
    [Fact]
    public async Task LogAction_NullResourceId_RoundTrips()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        // Act
        await _auditLogService.LogActionAsync(new AuditLogContext(adminId, AuditActionType.ViewUsers, AuditResourceType.User, null));

        // Assert
        var log = _context.AuditLogs.Single();
        Assert.Null(log.ResourceId);
    }

    /// <summary>
    /// Multiple actions written in the same timestamp tick are ordered deterministically by Id tie-break.
    /// </summary>
    [Fact]
    public async Task GetAllLogs_SameTimestamp_OrdersByIdDescending()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000001");

        _context.AuditLogs.AddRange(
            new AuditLog { Id = id2, UserId = adminId, ActionType = AuditActionType.ViewUsers, ResourceType = AuditResourceType.User, Timestamp = timestamp },
            new AuditLog { Id = id1, UserId = adminId, ActionType = AuditActionType.ViewEvents, ResourceType = AuditResourceType.Event, Timestamp = timestamp }
        );
        await _context.SaveChangesAsync();

        // Act
        var logs = (await _auditLogService.GetAllLogsAsync()).ToList();

        // Assert
        Assert.Equal(2, logs.Count);
        Assert.Equal(id1, logs[0].Id);
        Assert.Equal(id2, logs[1].Id);
    }

    #endregion

    #region Admin User Access

    /// <summary>
    /// Admin can view all user accounts.
    /// Validates Requirement 14.4.
    /// </summary>
    [Fact]
    public async Task GetAllUsers_AdminAccess_ReturnsAllUsers()
    {
        // Arrange
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Email = "user1@example.com",
            PasswordHash = "hash1",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "user2@example.com",
            PasswordHash = "hash2",
            Role = UserRole.Staff,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            PasswordHash = "hash3",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _context.Users.AddRange(user1, user2, admin);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetAllUsersAsync(1, 50);

        // Assert
        Assert.NotNull(result);
        var userList = result.Items.ToList();
        Assert.Equal(3, result.Total);
        Assert.Equal(3, userList.Count);
        Assert.Contains(userList, u => u.Email == "user1@example.com" && u.Role == UserRole.Organizador);
        Assert.Contains(userList, u => u.Email == "user2@example.com" && u.Role == UserRole.Staff);
        Assert.Contains(userList, u => u.Email == "admin@example.com" && u.Role == UserRole.Admin);
    }

    /// <summary>
    /// Empty users table returns an empty paged result.
    /// </summary>
    [Fact]
    public async Task GetAllUsers_NoUsers_ReturnsEmptyPagedResult()
    {
        // Act
        var result = await _adminService.GetAllUsersAsync(1, 50);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    /// <summary>
    /// User summaries must not expose password hashes.
    /// </summary>
    [Fact]
    public async Task GetAllUsers_UserSummary_DoesNotExposePasswordHash()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "super-secret-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetAllUsersAsync(1, 50);

        // Assert
        var userList = result.Items.ToList();
        Assert.Single(userList);
        Assert.Equal(user.Email, userList[0].Email);
        Assert.Null(userList[0].GetType().GetProperty("PasswordHash"));
    }

    /// <summary>
    /// Admin user access (Pagination cap): A requested page size above the 200-row hard cap is clamped to 200.
    /// </summary>
    [Fact]
    public async Task GetAllUsers_PageSizeOver200_IsCappedTo200()
    {
        // Act
        var result = await _adminService.GetAllUsersAsync(1, 500);

        // Assert
        Assert.Equal(200, result.PageSize);
    }

    /// <summary>
    /// Default UserRole value is Organizador (enum value 0).
    /// </summary>
    [Fact]
    public void UserRole_DefaultValue_IsOrganizador()
    {
        Assert.Equal(UserRole.Organizador, default(UserRole));
    }

    #endregion

    #region Model Validation

    /// <summary>
    /// AuditLog entity configuration enforces MaxLength constraints for action/resource types and details.
    /// </summary>
    [Fact]
    public void AuditLogConfiguration_EnforcesMaxLength()
    {
        var entity = _context.Model.FindEntityType(typeof(AuditLog));
        Assert.NotNull(entity);

        var actionType = entity.FindProperty(nameof(AuditLog.ActionType));
        var resourceType = entity.FindProperty(nameof(AuditLog.ResourceType));
        var details = entity.FindProperty(nameof(AuditLog.Details));

        Assert.Equal(100, actionType!.GetMaxLength());
        Assert.Equal(100, resourceType!.GetMaxLength());
        Assert.Equal(1000, details!.GetMaxLength());
    }

    #endregion

    #region EA-005/EA-003 — Approve/Reject status transitions + pending list

    /// <summary>
    /// EA-005: for ANY starting status, an admin approve or reject call succeeds —
    /// no state machine blocks any transition (only a missing event fails).
    /// </summary>
    [Fact]
    public void Property_ApproveReject_AnyStatus_TransitionsSucceed()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<EventStatus>(GenStatic.Elements(Enum.GetValues<EventStatus>())),
            startingStatus =>
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                    .Options;

                using var context = new ApplicationDbContext(options);
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var adminService = new AdminService(context, loggerFactory.CreateLogger<AdminService>());

                var eventEntity = CreateEvent(Guid.NewGuid(), "Transitions");
                eventEntity.Status = startingStatus;
                context.Events.Add(eventEntity);
                context.SaveChanges();

                var approved = Task.Run(() => adminService.ApproveEventAsync(eventEntity.Id)).Result;
                var rejected = Task.Run(() => adminService.RejectEventAsync(eventEntity.Id, "reason")).Result;

                return approved.Status == EventStatus.Approved
                       && rejected.Status == EventStatus.Rejected;
            });

        Check.QuickThrowOnFailure(prop);
    }

    /// <summary>
    /// EA-003: GetPendingEventsAsync returns ONLY Pending events (mixed statuses),
    /// oldest first, and never approved/rejected rows.
    /// </summary>
    [Fact]
    public async Task GetPendingEvents_ReturnsOnlyPending_OldestFirst()
    {
        // Arrange — one event per status; Pending ones created at different times
        var organizerId = Guid.NewGuid();
        var early = CreateEvent(organizerId, "Early Pending");
        early.Status = EventStatus.Pending;
        early.CreatedAt = DateTime.UtcNow.AddDays(-5);
        var late = CreateEvent(organizerId, "Late Pending");
        late.Status = EventStatus.Pending;
        late.CreatedAt = DateTime.UtcNow.AddDays(-1);
        var approved = CreateEvent(organizerId, "Approved");
        approved.Status = EventStatus.Approved;
        var rejected = CreateEvent(organizerId, "Rejected");
        rejected.Status = EventStatus.Rejected;
        _context.Events.AddRange(early, late, approved, rejected);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetPendingEventsAsync(1, 50);

        // Assert — only the two Pending events, oldest (CreatedAt) first
        var ids = result.Items.Select(e => e.Id).ToList();
        Assert.Equal(2, result.Total);
        Assert.Equal(2, ids.Count);
        Assert.Contains(early.Id, ids);
        Assert.Contains(late.Id, ids);
        Assert.DoesNotContain(approved.Id, ids);
        Assert.DoesNotContain(rejected.Id, ids);
        Assert.Equal(early.Id, ids[0]); // OrderBy(CreatedAt): early before late
        Assert.Equal(EventStatus.Pending, result.Items[0].Status);
        Assert.Equal(EventStatus.Pending, result.Items[1].Status);
    }

    #endregion

    private static Event CreateEvent(Guid organizerId, string name)
    {
        var now = DateTime.UtcNow;
        return new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test event for admin tests",
            Date = now.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static Gen<string> SafeStringGen()
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
        return GenStatic.Where(
            GenStatic.ArrayOf(GenStatic.Elements(chars)),
            arr => arr.Length > 0)
            .Select(arr => new string(arr));
    }

    private static Gen<Guid> GuidGen()
    {
        return GenStatic.ArrayOf(GenStatic.Choose(0, 255).Select(i => (byte)i), 16)
            .Select(bytes => new Guid(bytes));
    }

    private static Gen<AdminEventScenario> AdminEventScenarioGen()
    {
        return
            from organizerCount in GenStatic.Choose(0, 5)
            from eventsPerOrganizer in GenStatic.Choose(0, 5)
            from adminEmail in SafeStringGen()
            from organizerEmails in GenStatic.ArrayOf(SafeStringGen(), organizerCount)
            from eventNames in GenStatic.ArrayOf(SafeStringGen(), organizerCount * eventsPerOrganizer)
            select BuildScenario(adminEmail, organizerEmails, eventNames, eventsPerOrganizer);
    }

    private static Gen<AuditActionScenario> AuditActionScenarioGen()
    {
        var guidGen = GuidGen();
        return
            from adminId in guidGen
            from action in GenStatic.Elements(Enum.GetValues<AuditActionType>())
            from resource in GenStatic.Elements(Enum.GetValues<AuditResourceType>())
            from resourceId in GenStatic.Frequency(
                (1, GenStatic.Constant((Guid?)null)),
                (1, guidGen.Select(g => (Guid?)g)))
            from details in GenStatic.Frequency(
                (1, GenStatic.Constant((string?)null)),
                (1, SafeStringGen().Select(s => (string?)$"Details: {s}")))
            select new AuditActionScenario(adminId, action, resource, resourceId, details);
    }

    private static AdminEventScenario BuildScenario(string adminEmail, string[] organizerEmails, string[] eventNames, int eventsPerOrganizer)
    {
        var now = DateTime.UtcNow;
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = $"admin-{adminEmail}-{Guid.NewGuid()}@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Admin,
            CreatedAt = now
        };

        var organizers = organizerEmails
            .Select((email, i) => new User
            {
                Id = Guid.NewGuid(),
                Email = $"org-{i}-{email}-{Guid.NewGuid()}@example.com",
                PasswordHash = "dummy-hash",
                Role = UserRole.Organizador,
                CreatedAt = now
            })
            .ToList();

        var events = new List<Event>();
        int nameIndex = 0;
        foreach (var organizer in organizers)
        {
            for (int i = 0; i < eventsPerOrganizer; i++)
            {
                var name = eventNames[nameIndex++];
                events.Add(new Event
                {
                    Id = Guid.NewGuid(),
                    Name = $"{organizer.Id}-{name}",
                    Description = "Generated event",
                    Date = now.AddDays(30 + i),
                    Location = "Generated Location",
                    ImageUrl = "https://example.com/generated.jpg",
                    OrganizerId = organizer.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        return new AdminEventScenario(admin, organizers, events);
    }

    private class CustomArbitrary<T> : Arbitrary<T>
    {
        public CustomArbitrary(Gen<T> generator)
        {
            Generator = generator;
        }

        public override Gen<T> Generator { get; }

        public override IEnumerable<T> Shrinker(T value) => Enumerable.Empty<T>();
    }
}

public record AdminEventScenario(User Admin, List<User> Organizers, List<Event> Events);

public record AuditActionScenario(Guid AdminId, AuditActionType Action, AuditResourceType Resource, Guid? ResourceId, string? Details);

/// <summary>
/// LINQ query syntax support for FsCheck fluent generators.
/// </summary>
public static class GenLinq
{
    public static Gen<B> Select<A, B>(this Gen<A> gen, Func<A, B> f) =>
        GenStatic.Select(gen, f);

    public static Gen<B> SelectMany<A, B>(this Gen<A> gen, Func<A, Gen<B>> f) =>
        GenStatic.SelectMany(gen, f);

    public static Gen<C> SelectMany<A, B, C>(this Gen<A> gen, Func<A, Gen<B>> f, Func<A, B, C> g) =>
        GenStatic.SelectMany(gen, f, g);

    public static Gen<A> Where<A>(this Gen<A> gen, Func<A, bool> predicate) =>
        GenStatic.Where(gen, predicate);
}
