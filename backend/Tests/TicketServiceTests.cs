using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for TicketService.
/// Tests QR code generation, signature verification, ticket creation, and validation.
/// </summary>
public class TicketServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<TicketService>> _mockLogger;
    private readonly TicketService _ticketService;
    private const string TestHmacKey = "test-hmac-secret-key-minimum-32-characters-long-for-security";

    public TicketServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup configuration
        var configDict = new Dictionary<string, string?>
        {
            ["QRCode:HmacSecretKey"] = TestHmacKey
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Setup logger
        _mockLogger = new Mock<ILogger<TicketService>>();

        // Create service
        _ticketService = new TicketService(_context, _configuration, _mockLogger.Object,
            new ServiceCollection().BuildServiceProvider());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public void GenerateQRCode_ReturnsCorrectFormat()
    {
        // Arrange
        var ticketId = Guid.NewGuid();

        // Act
        var qrCodeData = _ticketService.GenerateQRCode(ticketId);

        // Assert
        Assert.NotNull(qrCodeData);
        var parts = qrCodeData.Split(':');
        Assert.Equal(3, parts.Length); // ticketId:timestamp:signature
        Assert.Equal(ticketId.ToString(), parts[0]);
        Assert.True(long.TryParse(parts[1], out _)); // Valid timestamp
        Assert.NotEmpty(parts[2]); // Signature exists
    }

    [Fact]
    public void VerifyQRCodeSignature_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var qrCodeData = _ticketService.GenerateQRCode(ticketId);

        // Act
        var isValid = _ticketService.VerifyQRCodeSignature(qrCodeData);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyQRCodeSignature_InvalidSignature_ReturnsFalse()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var invalidQRCode = $"{ticketId}:{timestamp}:invalidsignature123";

        // Act
        var isValid = _ticketService.VerifyQRCodeSignature(invalidQRCode);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyQRCodeSignature_InvalidFormat_ReturnsFalse()
    {
        // Arrange - QR code with only 2 parts instead of 3
        var invalidQRCode = "ticketId:timestamp";

        // Act
        var isValid = _ticketService.VerifyQRCodeSignature(invalidQRCode);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void GenerateQRCodeImage_ValidData_ReturnsBase64Image()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var qrCodeData = _ticketService.GenerateQRCode(ticketId);

        // Act
        var imageBase64 = _ticketService.GenerateQRCodeImage(qrCodeData);

        // Assert
        Assert.NotNull(imageBase64);
        Assert.NotEmpty(imageBase64);
        
        // Verify it's valid base64
        var bytes = Convert.FromBase64String(imageBase64);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task CreateTicketsAsync_ValidReservation_CreatesTickets()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 3,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act
        var tickets = await _ticketService.CreateTicketsAsync(
            reservation.Id,
            "buyer@test.com",
            "12345678");

        // Assert
        Assert.NotNull(tickets);
        Assert.Equal(3, tickets.Count());
        
        foreach (var ticket in tickets)
        {
            Assert.NotEqual(Guid.Empty, ticket.Id);
            Assert.Equal(eventEntity.Id, ticket.EventId);
            Assert.Equal(ticketType.Id, ticket.TicketTypeId);
            Assert.Equal("buyer@test.com", ticket.PurchaserEmail);
            Assert.Equal("12345678", ticket.PurchaserDNI);
            Assert.NotEmpty(ticket.QRCodeData);
            Assert.False(ticket.IsUsed);
            Assert.Null(ticket.UsedAt);
            
            // Verify QR code is valid
            Assert.True(_ticketService.VerifyQRCodeSignature(ticket.QRCodeData));
        }
    }

    [Fact]
    public async Task CreateTicketsAsync_NonConfirmedReservation_ThrowsException()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 3,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active, // Not confirmed
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _ticketService.CreateTicketsAsync(
                reservation.Id,
                "buyer@test.com",
                "12345678"));
    }

    [Fact]
    public async Task ValidateQRCodeAsync_ValidUnusedTicket_MarksAsUsed()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var ticketId = Guid.NewGuid();
        var qrCodeData = _ticketService.GenerateQRCode(ticketId);

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        Assert.NotNull(result.Ticket);
        Assert.True(result.Ticket.IsUsed);
        Assert.NotNull(result.Ticket.UsedAt);
    }

    [Fact]
    public async Task ValidateQRCodeAsync_AlreadyUsedTicket_ReturnsError()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var ticketId = Guid.NewGuid();
        var qrCodeData = _ticketService.GenerateQRCode(ticketId);

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = true, // Already used
            UsedAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("already used", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Ticket);
    }

    [Fact]
    public async Task ValidateQRCodeAsync_WrongEvent_ReturnsError()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity1 = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event 1",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var eventEntity2 = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event 2",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(31),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity1.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var ticketId = Guid.NewGuid();
        var qrCodeData = _ticketService.GenerateQRCode(ticketId);

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventEntity1.Id, // Ticket is for event 1
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity1);
        _context.Events.Add(eventEntity2);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act - Validate at event 2
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity2.Id);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not this event", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Ticket);
    }

    [Fact]
    public async Task ValidateQRCodeAsync_InvalidSignature_ReturnsError()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var invalidQRCode = $"{ticketId}:{timestamp}:invalidsignature";

        // Act
        var result = await _ticketService.ValidateQRCodeAsync(invalidQRCode, Guid.NewGuid());

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("signature", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupTicketsAsync_MatchingEmailAndDNI_ReturnsTickets()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var ticket1 = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        var ticket2 = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket1);
        _context.Tickets.Add(ticket2);
        await _context.SaveChangesAsync();

        // Act
        var tickets = await _ticketService.LookupTicketsAsync("buyer@test.com", "12345678");

        // Assert
        Assert.NotNull(tickets);
        Assert.Equal(2, tickets.Count());
    }

    [Fact]
    public async Task LookupTicketsAsync_NoMatch_ReturnsEmptyList()
    {
        // Act
        var tickets = await _ticketService.LookupTicketsAsync("nonexistent@test.com", "99999999");

        // Assert
        Assert.NotNull(tickets);
        Assert.Empty(tickets);
    }

    [Fact]
    public async Task LookupTicketsAsync_RequiresBothEmailAndDNI_ReturnsOnlyExactMatches()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        // Ticket with matching email but different DNI
        var ticket1 = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "11111111",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        // Ticket with matching DNI but different email
        var ticket2 = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "other@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        // Ticket with both matching
        var ticket3 = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket1);
        _context.Tickets.Add(ticket2);
        _context.Tickets.Add(ticket3);
        await _context.SaveChangesAsync();

        // Act
        var tickets = await _ticketService.LookupTicketsAsync("buyer@test.com", "12345678");

        // Assert
        Assert.NotNull(tickets);
        Assert.Single(tickets); // Only ticket3 should be returned
        Assert.Equal(ticket3.Id, tickets.First().Id);
    }

    #region QR Timestamp Window Validation (B5.4)

    [Fact]
    public async Task ValidateQRCodeAsync_TimestampBeforePurchaseDate_ReturnsError()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var purchaseDate = DateTime.UtcNow.AddDays(-10); // ticket purchased 10 days ago
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(10),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = purchaseDate,
            UpdatedAt = purchaseDate
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = purchaseDate
        };

        var ticketId = Guid.NewGuid();
        // Create QR code with timestamp BEFORE purchase date
        var badTimestamp = new DateTimeOffset(purchaseDate.AddDays(-5)).ToUnixTimeSeconds();
        var dataToSign = $"{ticketId}:{badTimestamp}";
        var signature = HmacHelper.ComputeHmacSha256(dataToSign, TestHmacKey);
        var qrCodeData = $"{dataToSign}:{signature}";

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            CreatedAt = purchaseDate
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("timestamp", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateQRCodeAsync_TimestampAfterEventEndPlus24h_ReturnsError()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var purchaseDate = DateTime.UtcNow.AddDays(-5);
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(-3), // Event ended 3 days ago
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = purchaseDate,
            UpdatedAt = purchaseDate
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = purchaseDate
        };

        var ticketId = Guid.NewGuid();
        // Create QR code with timestamp AFTER event end + 24h
        var badTimestamp = new DateTimeOffset(eventEntity.Date.AddHours(48)).ToUnixTimeSeconds();
        var dataToSign = $"{ticketId}:{badTimestamp}";
        var signature = HmacHelper.ComputeHmacSha256(dataToSign, TestHmacKey);
        var qrCodeData = $"{dataToSign}:{signature}";

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            CreatedAt = purchaseDate
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("timestamp", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateQRCodeAsync_TimestampInFuture_ReturnsError()
    {
        // Arrange
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var purchaseDate = DateTime.UtcNow.AddDays(-1);
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = purchaseDate,
            UpdatedAt = purchaseDate
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = purchaseDate
        };

        var ticketId = Guid.NewGuid();
        // Create QR code with timestamp IN THE FUTURE (5 days from now)
        var futureTimestamp = new DateTimeOffset(DateTime.UtcNow.AddDays(5)).ToUnixTimeSeconds();
        var dataToSign = $"{ticketId}:{futureTimestamp}";
        var signature = HmacHelper.ComputeHmacSha256(dataToSign, TestHmacKey);
        var qrCodeData = $"{dataToSign}:{signature}";

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            CreatedAt = purchaseDate
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("timestamp", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateQRCodeAsync_TimestampWithinWindow_ReturnsValid()
    {
        // Arrange — timestamp within purchase-to-eventEnd+24h window should be valid
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var purchaseDate = DateTime.UtcNow.AddDays(-1);
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(7),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = purchaseDate,
            UpdatedAt = purchaseDate
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = purchaseDate
        };

        var ticketId = Guid.NewGuid();
        // Use a valid timestamp within window (now)
        var validTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataToSign = $"{ticketId}:{validTimestamp}";
        var signature = HmacHelper.ComputeHmacSha256(dataToSign, TestHmacKey);
        var qrCodeData = $"{dataToSign}:{signature}";

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            CreatedAt = purchaseDate
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void HmacHelper_ExtractTimestamp_ReturnsCorrectUnixTimestamp()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataToSign = $"{ticketId}:{timestamp}";
        var signature = HmacHelper.ComputeHmacSha256(dataToSign, TestHmacKey);
        var qrCodeData = $"{dataToSign}:{signature}";

        // Act
        var extracted = HmacHelper.ExtractTimestamp(qrCodeData);

        // Assert
        Assert.Equal(timestamp, extracted);
    }

    [Fact]
    public void HmacHelper_ExtractTimestamp_InvalidFormat_ThrowsFormatException()
    {
        // Arrange
        var invalidQrData = "not-a-valid-qr-format";

        // Act & Assert
        Assert.Throws<FormatException>(() => HmacHelper.ExtractTimestamp(invalidQrData));
    }

    #endregion

    #region Refunded tickets (APR-005/006/009)

    [Fact]
    public async Task CreateTicketsAsync_SetsReservationId_OnEveryTicket()
    {
        // Arrange — APR-009: tickets created after this change carry their ReservationId
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 2,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act
        var tickets = await _ticketService.CreateTicketsAsync(reservation.Id, "buyer@test.com", "12345678");

        // Assert — every ticket is precisely linked to its reservation
        var ticketList = tickets.ToList();
        Assert.Equal(2, ticketList.Count);
        Assert.All(ticketList, t => Assert.Equal(reservation.Id, t.ReservationId));
    }

    [Fact]
    public async Task ValidateQRCodeAsync_RefundedTicket_ReturnsInvalidWithEntradaReembolsada()
    {
        // Arrange — APR-006: a refunded ticket must be rejected with "Entrada reembolsada"
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        var purchaseDate = DateTime.UtcNow.AddDays(-1);
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(7),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = purchaseDate,
            UpdatedAt = purchaseDate
        };
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = purchaseDate
        };
        var ticketId = Guid.NewGuid();
        var validTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataToSign = $"{ticketId}:{validTimestamp}";
        var signature = HmacHelper.ComputeHmacSha256(dataToSign, TestHmacKey);
        var qrCodeData = $"{dataToSign}:{signature}";

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            IsRefunded = true,
            RefundedAt = DateTime.UtcNow.AddDays(-2),
            CreatedAt = purchaseDate
        };
        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert — invalid, the exact Spanish message, and the ticket attached
        Assert.False(result.IsValid);
        Assert.Equal("Entrada reembolsada", result.Error);
        Assert.NotNull(result.Ticket);
        Assert.Equal(ticketId, result.Ticket!.Id);
    }

    [Fact]
    public async Task LookupTicketsByEmailAsync_ExcludesRefundedTickets()
    {
        // Arrange — one valid + one refunded ticket for the same email (APR-005)
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            IsRefunded = true,
            RefundedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _context.SaveChangesAsync();

        // Act
        var responses = await _ticketService.LookupTicketsByEmailAsync("buyer@test.com");

        // Assert — only the non-refunded ticket is summarized
        var response = Assert.Single(responses);
        Assert.Equal(1, response.Quantity);
    }

    [Fact]
    public async Task LookupActiveTicketsByEmailAndDniAsync_ExcludesRefundedTickets()
    {
        // Arrange — one active + one refunded ticket for the same email/DNI (APR-005)
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            IsRefunded = true,
            RefundedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _context.SaveChangesAsync();

        // Act
        var responses = await _ticketService.LookupActiveTicketsByEmailAndDniAsync("buyer@test.com", "12345678");

        // Assert — only the non-refunded ticket is summarized
        var response = Assert.Single(responses);
        Assert.Equal(1, response.Quantity);
    }

    [Fact]
    public async Task ResendTicketsByEmailAsync_ExcludesRefundedTickets()
    {
        // Arrange — APR-005: refunded tickets are not re-sent
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock
            .Setup(s => s.SendResendEmailAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Ticket>>(), It.IsAny<Event>()))
            .ReturnsAsync(new EmailResult { Success = true });
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IEmailService>(emailServiceMock.Object);
        var ticketService = new TicketService(
            _context, _configuration, _mockLogger.Object, serviceCollection.BuildServiceProvider());

        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = _ticketService.GenerateQRCode(Guid.NewGuid()),
            IsUsed = false,
            IsRefunded = true,
            RefundedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await ticketService.ResendTicketsByEmailAsync("buyer@test.com");

        // Assert — success returned and the email got only the non-refunded ticket
        Assert.True(result);
        emailServiceMock.Verify(s => s.SendResendEmailAsync(
            "buyer@test.com",
            It.Is<IEnumerable<Ticket>>(tickets => tickets.Count() == 1 && tickets.All(t => !t.IsRefunded)),
            It.IsAny<Event>()), Times.Once);
    }

    #endregion
}
