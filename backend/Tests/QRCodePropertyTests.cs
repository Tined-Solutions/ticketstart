using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for QR code functionality.
/// Validates Requirements 6.1, 6.2, 6.3, 6.6, 6.7, 9.4, 9.5, 9.6
/// </summary>
public class QRCodePropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<TicketService>> _mockLogger;
    private readonly TicketService _ticketService;
    private const string TestHmacKey = "test-hmac-secret-key-minimum-32-characters-long-for-security";

    public QRCodePropertyTests()
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

    #region Property 18: QR Code Uniqueness

    /// <summary>
    /// Property 18: QR Code Uniqueness
    /// For any set of generated tickets, all QR codes SHALL be unique.
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public async Task Property18_QRCodeUniqueness_AllGeneratedQRCodesAreUnique()
    {
        // Arrange - Create test data
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
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 50, // Create 50 tickets
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act - Create multiple tickets
        var tickets = await _ticketService.CreateTicketsAsync(
            reservation.Id,
            "buyer@test.com",
            "12345678");

        // Assert - All QR codes must be unique
        var qrCodes = tickets.Select(t => t.QRCodeData).ToList();
        var distinctQRCodes = qrCodes.Distinct().ToList();

        Assert.Equal(qrCodes.Count, distinctQRCodes.Count);
        Assert.Equal(50, qrCodes.Count);
        
        // Additional verification: Each ticket ID in QR code should be unique
        var ticketIds = new HashSet<string>();
        foreach (var qrCode in qrCodes)
        {
            var parts = qrCode.Split(':');
            Assert.Equal(3, parts.Length);
            var ticketIdFromQR = parts[0];
            Assert.True(ticketIds.Add(ticketIdFromQR), 
                $"Duplicate ticket ID found in QR codes: {ticketIdFromQR}");
        }
    }

    /// <summary>
    /// Property 18 (Multiple reservations): QR codes across different reservations are unique
    /// </summary>
    [Fact]
    public async Task Property18_QRCodeUniqueness_AcrossMultipleReservations()
    {
        // Arrange - Create test data for multiple reservations
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
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var allQRCodes = new List<string>();

        // Act - Create multiple reservations and tickets
        for (int i = 0; i < 5; i++)
        {
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                Quantity = 5,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var tickets = await _ticketService.CreateTicketsAsync(
                reservation.Id,
                $"buyer{i}@test.com",
                $"1234567{i}");

            allQRCodes.AddRange(tickets.Select(t => t.QRCodeData));
        }

        // Assert - All QR codes across all reservations must be unique
        var distinctQRCodes = allQRCodes.Distinct().ToList();
        Assert.Equal(allQRCodes.Count, distinctQRCodes.Count);
        Assert.Equal(25, allQRCodes.Count); // 5 reservations * 5 tickets each
    }

    #endregion

    #region Property 19: QR Code Signature Validity

    /// <summary>
    /// Property 19: QR Code Signature Validity
    /// For any generated QR code, the HMAC-SHA256 signature SHALL be valid 
    /// when verified with the secret key.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void Property19_QRCodeSignatureValidity_AllGeneratedSignaturesAreValid()
    {
        // Arrange & Act - Generate multiple QR codes with different ticket IDs
        var testTicketIds = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };

        foreach (var ticketId in testTicketIds)
        {
            // Act - Generate QR code
            var qrCodeData = _ticketService.GenerateQRCode(ticketId);

            // Assert - Signature must be valid
            var isValid = _ticketService.VerifyQRCodeSignature(qrCodeData);
            Assert.True(isValid, $"Generated QR code for ticket {ticketId} should have valid signature");
            
            // Additional verification: QR code should contain the ticket ID
            Assert.Contains(ticketId.ToString(), qrCodeData);
        }
    }

    /// <summary>
    /// Property 19 (Time-based): QR codes generated at different times have valid signatures
    /// </summary>
    [Fact]
    public async Task Property19_QRCodeSignatureValidity_ValidAcrossDifferentTimestamps()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var qrCodes = new List<string>();

        // Act - Generate QR codes with delays to ensure different timestamps
        for (int i = 0; i < 5; i++)
        {
            var qrCode = _ticketService.GenerateQRCode(ticketId);
            qrCodes.Add(qrCode);
            
            // Delay to ensure different timestamps (Unix timestamps are in seconds)
            await Task.Delay(1100);
        }

        // Assert - All QR codes should have valid signatures
        foreach (var qrCode in qrCodes)
        {
            var isValid = _ticketService.VerifyQRCodeSignature(qrCode);
            Assert.True(isValid, $"QR code should have valid signature: {qrCode}");
        }
        
        // Additional check: Each QR code should have different timestamp
        var timestamps = qrCodes.Select(qr => qr.Split(':')[1]).ToList();
        var distinctTimestamps = timestamps.Distinct().ToList();
        Assert.True(distinctTimestamps.Count > 1, 
            "QR codes generated at different times should have different timestamps");
    }

    #endregion

    #region Property 20: QR Code Format Correctness

    /// <summary>
    /// Property 20: QR Code Format Correctness
    /// For any generated QR code, it SHALL encode the ticket identifier, timestamp, 
    /// and HMAC signature in the format {ticketId}:{timestamp}:{signature}.
    /// Validates: Requirements 6.3
    /// </summary>
    [Fact]
    public void Property20_QRCodeFormatCorrectness_MatchesExpectedFormat()
    {
        // Arrange - Generate QR codes for various ticket IDs
        var testTicketIds = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid()
        };

        foreach (var ticketId in testTicketIds)
        {
            // Act - Generate QR code
            var qrCodeData = _ticketService.GenerateQRCode(ticketId);

            // Assert - Verify format {ticketId}:{timestamp}:{signature}
            Assert.NotNull(qrCodeData);
            Assert.NotEmpty(qrCodeData);

            var parts = qrCodeData.Split(':');
            Assert.Equal(3, parts.Length);

            // Part 1: Ticket ID (must be valid GUID)
            Assert.True(Guid.TryParse(parts[0], out var parsedTicketId), 
                $"First part should be valid GUID: {parts[0]}");
            Assert.Equal(ticketId, parsedTicketId);

            // Part 2: Timestamp (must be valid Unix timestamp)
            Assert.True(long.TryParse(parts[1], out var timestamp), 
                $"Second part should be valid timestamp: {parts[1]}");
            Assert.True(timestamp > 0, "Timestamp should be positive");
            
            // Verify timestamp is reasonable (within last minute and not in future)
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.True(timestamp <= now + 5, "Timestamp should not be in future");
            Assert.True(timestamp >= now - 60, "Timestamp should be recent");

            // Part 3: Signature (must be non-empty hexadecimal string)
            Assert.NotEmpty(parts[2]);
            Assert.True(parts[2].All(c => "0123456789abcdef".Contains(c)), 
                $"Signature should be hexadecimal: {parts[2]}");
            
            // HMAC-SHA256 produces 256-bit (64 hex characters) signature
            Assert.Equal(64, parts[2].Length);
        }
    }

    /// <summary>
    /// Property 20 (Edge case): Format is consistent across all tickets in a batch
    /// </summary>
    [Fact]
    public async Task Property20_QRCodeFormatCorrectness_ConsistentFormatAcrossBatch()
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
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 20,
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

        // Assert - All tickets should have correct format
        foreach (var ticket in tickets)
        {
            var parts = ticket.QRCodeData.Split(':');
            Assert.Equal(3, parts.Length);
            Assert.True(Guid.TryParse(parts[0], out _));
            Assert.True(long.TryParse(parts[1], out _));
            Assert.Equal(64, parts[2].Length); // HMAC-SHA256 hex length
        }
    }

    #endregion

    #region Property 21: QR Code Signature Verification

    /// <summary>
    /// Property 21: QR Code Signature Verification
    /// For any QR code presented for validation, the system SHALL verify 
    /// the HMAC-SHA256 signature and reject codes with invalid signatures.
    /// Validates: Requirements 6.6, 6.7
    /// </summary>
    [Fact]
    public void Property21_SignatureVerification_RejectsInvalidSignatures()
    {
        // Test cases with various invalid signatures
        var testCases = new[]
        {
            // Case 1: Completely invalid signature
            new { 
                TicketId = Guid.NewGuid(), 
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 
                Signature = "invalidsignature123",
                Description = "random invalid signature"
            },
            // Case 2: Empty signature
            new { 
                TicketId = Guid.NewGuid(), 
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 
                Signature = "",
                Description = "empty signature"
            },
            // Case 3: Wrong length signature
            new { 
                TicketId = Guid.NewGuid(), 
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 
                Signature = "abc123",
                Description = "too short signature"
            },
            // Case 4: Signature for different data
            new { 
                TicketId = Guid.NewGuid(), 
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 
                Signature = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Description = "wrong signature"
            }
        };

        foreach (var testCase in testCases)
        {
            // Arrange - Create QR code with invalid signature
            var invalidQRCode = $"{testCase.TicketId}:{testCase.Timestamp}:{testCase.Signature}";

            // Act
            var isValid = _ticketService.VerifyQRCodeSignature(invalidQRCode);

            // Assert
            Assert.False(isValid, 
                $"QR code with {testCase.Description} should be rejected");
        }
    }

    /// <summary>
    /// Property 21 (Tamper detection): Modified QR code data is rejected
    /// </summary>
    [Fact]
    public void Property21_SignatureVerification_RejectsTamperedData()
    {
        // Arrange - Generate valid QR code
        var originalTicketId = Guid.NewGuid();
        var validQRCode = _ticketService.GenerateQRCode(originalTicketId);
        
        // Verify it's valid first
        Assert.True(_ticketService.VerifyQRCodeSignature(validQRCode));

        var parts = validQRCode.Split(':');
        var timestamp = parts[1];
        var signature = parts[2];

        // Test various tampering attempts
        var tamperedCases = new[]
        {
            // Case 1: Change ticket ID but keep signature
            new { 
                QRCode = $"{Guid.NewGuid()}:{timestamp}:{signature}",
                Description = "changed ticket ID"
            },
            // Case 2: Change timestamp but keep signature
            new { 
                QRCode = $"{originalTicketId}:{long.Parse(timestamp) + 1000}:{signature}",
                Description = "changed timestamp"
            },
            // Case 3: Single character change in signature
            new { 
                QRCode = $"{originalTicketId}:{timestamp}:{signature[0..^1]}a",
                Description = "modified signature"
            }
        };

        foreach (var testCase in tamperedCases)
        {
            // Act
            var isValid = _ticketService.VerifyQRCodeSignature(testCase.QRCode);

            // Assert
            Assert.False(isValid, 
                $"Tampered QR code ({testCase.Description}) should be rejected");
        }
    }

    /// <summary>
    /// Property 21 (Format validation): Malformed QR codes are rejected
    /// </summary>
    [Fact]
    public void Property21_SignatureVerification_RejectsMalformedQRCodes()
    {
        var malformedCases = new[]
        {
            "",
            "single-part",
            "two:parts",
            "four:parts:are:invalid",
            "invalid-guid:12345:signature",
            $"{Guid.NewGuid()}:notanumber:signature",
            null!
        };

        foreach (var malformedQRCode in malformedCases)
        {
            // Act
            var isValid = _ticketService.VerifyQRCodeSignature(malformedQRCode);

            // Assert
            Assert.False(isValid, 
                $"Malformed QR code should be rejected: {malformedQRCode ?? "null"}");
        }
    }

    #endregion

    #region Property 27: Double-Scan Prevention

    /// <summary>
    /// Property 27: Double-Scan Prevention
    /// For any ticket that has already been scanned and marked as used, 
    /// subsequent scan attempts SHALL be rejected with an "already used" error.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public async Task Property27_DoubleScanPrevention_RejectsAlreadyUsedTickets()
    {
        // Arrange - Create test data
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

        // Act - First scan (should succeed)
        var firstScanResult = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert - First scan should succeed
        Assert.True(firstScanResult.IsValid);
        Assert.Null(firstScanResult.Error);
        Assert.NotNull(firstScanResult.Ticket);
        Assert.True(firstScanResult.Ticket.IsUsed);
        Assert.NotNull(firstScanResult.Ticket.UsedAt);

        var firstUsedAt = firstScanResult.Ticket.UsedAt;

        // Act - Second scan attempt (should fail)
        var secondScanResult = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert - Second scan should be rejected
        Assert.False(secondScanResult.IsValid);
        Assert.NotNull(secondScanResult.Error);
        Assert.Contains("already used", secondScanResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(secondScanResult.Ticket);
        Assert.True(secondScanResult.Ticket.IsUsed);

        // Act - Multiple subsequent scans (all should fail)
        for (int i = 0; i < 5; i++)
        {
            var additionalScanResult = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);
            
            Assert.False(additionalScanResult.IsValid, 
                $"Scan attempt #{i + 3} should be rejected");
            Assert.Contains("already used", additionalScanResult.Error, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Property 27 (Edge case): UsedAt timestamp remains unchanged after double-scan attempts
    /// </summary>
    [Fact]
    public async Task Property27_DoubleScanPrevention_PreservesOriginalUsedAtTimestamp()
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

        // Act - First scan
        var firstScanResult = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);
        Assert.True(firstScanResult.IsValid);
        var originalUsedAt = firstScanResult.Ticket!.UsedAt;
        Assert.NotNull(originalUsedAt);

        // Small delay
        await Task.Delay(100);

        // Act - Second scan
        var secondScanResult = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);
        
        // Assert - UsedAt should not change
        Assert.False(secondScanResult.IsValid);
        Assert.Equal(originalUsedAt, secondScanResult.Ticket!.UsedAt);
    }

    #endregion

    #region Property 28: Event-Specific Ticket Validation

    /// <summary>
    /// Property 28: Event-Specific Ticket Validation
    /// For any ticket, validation SHALL succeed only when scanned at the event 
    /// for which the ticket was purchased.
    /// Validates: Requirements 9.5
    /// </summary>
    [Fact]
    public async Task Property28_EventSpecificValidation_RejectsTicketsForDifferentEvent()
    {
        // Arrange - Create two different events
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Concert A",
            Description = "First Event",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Venue A",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Concert B",
            Description = "Second Event",
            Date = DateTime.UtcNow.AddDays(31),
            Location = "Venue B",
            OrganizerId = organizer.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType1 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = event1.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var ticketType2 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = event2.Id,
            Name = "General Admission",
            Price = 100m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var ticketId = Guid.NewGuid();
        var qrCodeData = _ticketService.GenerateQRCode(ticketId);

        // Ticket is for Event 1
        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = event1.Id,
            TicketTypeId = ticketType1.Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(event1);
        _context.Events.Add(event2);
        _context.TicketTypes.Add(ticketType1);
        _context.TicketTypes.Add(ticketType2);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act - Try to validate at the correct event (Event 1)
        var correctEventResult = await _ticketService.ValidateQRCodeAsync(qrCodeData, event1.Id);

        // Assert - Should succeed at correct event
        Assert.True(correctEventResult.IsValid);
        Assert.Null(correctEventResult.Error);
        Assert.NotNull(correctEventResult.Ticket);

        // Act - Try to validate the same ticket at wrong event (Event 2)
        // Note: We need to reset the ticket for this test
        ticket.IsUsed = false;
        ticket.UsedAt = null;
        await _context.SaveChangesAsync();

        var wrongEventResult = await _ticketService.ValidateQRCodeAsync(qrCodeData, event2.Id);

        // Assert - Should fail at wrong event
        Assert.False(wrongEventResult.IsValid);
        Assert.NotNull(wrongEventResult.Error);
        Assert.Contains("not this event", wrongEventResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(wrongEventResult.Ticket);
        Assert.False(wrongEventResult.Ticket.IsUsed); // Should NOT be marked as used
    }

    /// <summary>
    /// Property 28 (Multiple events): Ticket cannot be used at any other event
    /// </summary>
    [Fact]
    public async Task Property28_EventSpecificValidation_TicketOnlyValidForOneEvent()
    {
        // Arrange - Create multiple events
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var events = new List<Event>();
        var ticketTypes = new List<TicketType>();

        for (int i = 0; i < 5; i++)
        {
            var ev = new Event
            {
                Id = Guid.NewGuid(),
                Name = $"Event {i}",
                Description = "Test Description",
                Date = DateTime.UtcNow.AddDays(30 + i),
                Location = $"Venue {i}",
                OrganizerId = organizer.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var tt = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                Name = "General Admission",
                Price = 100m,
                Quantity = 10,
                CreatedAt = DateTime.UtcNow
            };

            events.Add(ev);
            ticketTypes.Add(tt);
            _context.Events.Add(ev);
            _context.TicketTypes.Add(tt);
        }

        var ticketId = Guid.NewGuid();
        var qrCodeData = _ticketService.GenerateQRCode(ticketId);

        // Ticket is specifically for Event 0
        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = events[0].Id,
            TicketTypeId = ticketTypes[0].Id,
            PurchaserEmail = "buyer@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act & Assert - Try to validate at all wrong events (1-4)
        for (int i = 1; i < events.Count; i++)
        {
            var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, events[i].Id);
            
            Assert.False(result.IsValid, 
                $"Ticket should be rejected at Event {i}");
            Assert.Contains("not this event", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        // Act & Assert - Should only succeed at the correct event (Event 0)
        var correctResult = await _ticketService.ValidateQRCodeAsync(qrCodeData, events[0].Id);
        Assert.True(correctResult.IsValid);
    }

    #endregion

    #region Property 29: Valid Ticket Marked as Used

    /// <summary>
    /// Property 29: Valid Ticket Marked as Used
    /// For any valid, unused ticket scanned at the correct event, 
    /// the system SHALL mark the ticket as used and return success.
    /// Validates: Requirements 9.6
    /// </summary>
    [Fact]
    public async Task Property29_ValidTicketMarkedAsUsed_SuccessfulScanMarksTicketUsed()
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
            UsedAt = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Verify ticket is initially unused
        Assert.False(ticket.IsUsed);
        Assert.Null(ticket.UsedAt);

        // Act - Validate QR code at correct event
        var result = await _ticketService.ValidateQRCodeAsync(qrCodeData, eventEntity.Id);

        // Assert - Validation should succeed
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        Assert.NotNull(result.Ticket);

        // Assert - Ticket should be marked as used
        Assert.True(result.Ticket.IsUsed);
        Assert.NotNull(result.Ticket.UsedAt);

        // Verify UsedAt timestamp is recent
        var timeSinceUse = DateTime.UtcNow - result.Ticket.UsedAt.Value;
        Assert.True(timeSinceUse.TotalSeconds < 5, 
            "UsedAt timestamp should be set to current time");

        // Verify database was updated
        var ticketFromDb = await _context.Tickets.FindAsync(ticketId);
        Assert.NotNull(ticketFromDb);
        Assert.True(ticketFromDb.IsUsed);
        Assert.NotNull(ticketFromDb.UsedAt);
        Assert.Equal(result.Ticket.UsedAt, ticketFromDb.UsedAt);
    }

    /// <summary>
    /// Property 29 (Batch validation): Multiple valid tickets can be marked as used
    /// </summary>
    [Fact]
    public async Task Property29_ValidTicketMarkedAsUsed_MultipleTicketsCanBeValidated()
    {
        // Arrange - Create multiple tickets for the same event
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
            Quantity = 20,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(organizer);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var tickets = new List<Ticket>();
        for (int i = 0; i < 10; i++)
        {
            var ticketId = Guid.NewGuid();
            var qrCodeData = _ticketService.GenerateQRCode(ticketId);

            var ticket = new Ticket
            {
                Id = ticketId,
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = $"buyer{i}@test.com",
                PurchaserDNI = $"1234567{i}",
                QRCodeData = qrCodeData,
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            tickets.Add(ticket);
            _context.Tickets.Add(ticket);
        }
        await _context.SaveChangesAsync();

        // Act - Validate all tickets
        foreach (var ticket in tickets)
        {
            var result = await _ticketService.ValidateQRCodeAsync(ticket.QRCodeData, eventEntity.Id);

            // Assert - Each validation should succeed
            Assert.True(result.IsValid, 
                $"Ticket {ticket.Id} should validate successfully");
            Assert.Null(result.Error);
            Assert.NotNull(result.Ticket);
            Assert.True(result.Ticket.IsUsed);
            Assert.NotNull(result.Ticket.UsedAt);
        }

        // Verify all tickets in database are marked as used
        var ticketsFromDb = await _context.Tickets
            .Where(t => t.EventId == eventEntity.Id)
            .ToListAsync();

        Assert.Equal(10, ticketsFromDb.Count);
        Assert.All(ticketsFromDb, t => Assert.True(t.IsUsed));
        Assert.All(ticketsFromDb, t => Assert.NotNull(t.UsedAt));
    }

    #endregion
}
