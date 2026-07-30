using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Text;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for ticket generation, QR code creation, and validation.
/// Uses HMAC-SHA256 for cryptographic signing of QR codes.
/// Uses QRCoder library for visual QR code image generation.
/// </summary>
public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TicketService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _hmacSecretKey;

    public TicketService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<TicketService> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hmacSecretKey = _configuration["QRCode:HmacSecretKey"]
            ?? throw new InvalidOperationException("QRCode:HmacSecretKey is not configured");
    }

    /// <summary>
    /// Creates tickets from a confirmed reservation.
    /// Generates unique QR codes with HMAC-SHA256 signatures for each ticket.
    /// Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5
    /// </summary>
    public async Task<IEnumerable<Ticket>> CreateTicketsAsync(
        Guid reservationId,
        string purchaserEmail,
        string purchaserDNI)
    {
        _logger.LogInformation("Creating tickets for reservation {ReservationId}", reservationId);

        // Load reservation with related data
        var reservation = await _context.Reservations
            .Include(r => r.Event)
            .Include(r => r.TicketType)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found", reservationId);
            throw new KeyNotFoundException($"Reservation {reservationId} not found");
        }

        // Validate reservation is confirmed
        if (reservation.Status != ReservationStatus.Confirmed)
        {
            _logger.LogWarning("Reservation {ReservationId} is not confirmed. Status: {Status}",
                reservationId, reservation.Status);
            throw new InvalidOperationException(
                $"Cannot create tickets for reservation with status: {reservation.Status}");
        }

        // Create tickets
        var tickets = new List<Ticket>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < reservation.Quantity; i++)
        {
            var ticketId = Guid.NewGuid();

            // Generate QR code with HMAC-SHA256 signature
            var qrCodeData = GenerateQRCode(ticketId);

            var ticket = new Ticket
            {
                Id = ticketId,
                EventId = reservation.EventId,
                TicketTypeId = reservation.TicketTypeId,
                PurchaserEmail = purchaserEmail,
                PurchaserDNI = purchaserDNI,
                QRCodeData = qrCodeData,
                IsUsed = false,
                UsedAt = null,
                CreatedAt = now
            };

            tickets.Add(ticket);
            _context.Tickets.Add(ticket);

            _logger.LogInformation("Created ticket {TicketId} with QR code for reservation {ReservationId}",
                ticketId, reservationId);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Successfully created {Count} tickets for reservation {ReservationId}",
            tickets.Count, reservationId);

        return tickets;
    }

    /// <summary>
    /// Generates a QR code string for a ticket with HMAC-SHA256 signature.
    /// Format: {ticketId}:{timestamp}:{signature}
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    public string GenerateQRCode(Guid ticketId)
    {
        // Generate timestamp
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Create data to sign: ticketId + timestamp
        var dataToSign = $"{ticketId}:{timestamp}";

        // Generate HMAC-SHA256 signature
        var signature = HmacHelper.ComputeHmacSha256(dataToSign, _hmacSecretKey);

        // Format: {ticketId}:{timestamp}:{signature}
        var qrCodeData = $"{dataToSign}:{signature}";

        _logger.LogDebug("Generated QR code for ticket {TicketId}: {QRCodeData}", ticketId, qrCodeData);

        return qrCodeData;
    }

    /// <summary>
    /// Generates a visual QR code image as a base64-encoded PNG.
    /// Uses QRCoder library to create the image.
    /// Validates: Requirement 6.5
    /// </summary>
    public string GenerateQRCodeImage(string qrCodeData)
    {
        _logger.LogDebug("Generating QR code image for data: {QRCodeData}", qrCodeData);

        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeDataObj = qrGenerator.CreateQrCode(qrCodeData, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeDataObj);
            
            // Generate PNG with 10 pixels per module
            var qrCodeBytes = qrCode.GetGraphic(10);
            
            // Convert to base64
            var base64Image = Convert.ToBase64String(qrCodeBytes);
            
            _logger.LogDebug("Successfully generated QR code image");
            
            return base64Image;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR code image for data: {QRCodeData}", qrCodeData);
            throw;
        }
    }

    /// <summary>
    /// Verifies the HMAC-SHA256 signature of a QR code.
    /// Validates: Requirements 6.6, 6.7
    /// </summary>
    public bool VerifyQRCodeSignature(string qrCodeData)
    {
        _logger.LogDebug("Verifying QR code signature: {QRCodeData}", qrCodeData);

        try
        {
            // Parse QR code data: {ticketId}:{timestamp}:{signature}
            var parts = qrCodeData.Split(':');
            if (parts.Length != 3)
            {
                _logger.LogWarning("Invalid QR code format. Expected 3 parts, got {Count}", parts.Length);
                return false;
            }

            var ticketIdStr = parts[0];
            var timestampStr = parts[1];
            var providedSignature = parts[2];

            // Validate ticket ID format
            if (!Guid.TryParse(ticketIdStr, out _))
            {
                _logger.LogWarning("Invalid ticket ID format in QR code: {TicketId}", ticketIdStr);
                return false;
            }

            // Validate timestamp format
            if (!long.TryParse(timestampStr, out _))
            {
                _logger.LogWarning("Invalid timestamp format in QR code: {Timestamp}", timestampStr);
                return false;
            }

            // Reconstruct data that was signed
            var dataToVerify = $"{ticketIdStr}:{timestampStr}";

            // Compare signatures (constant-time comparison to prevent timing attacks)
            var isValid = HmacHelper.ValidateHmacSha256(dataToVerify, _hmacSecretKey, providedSignature);

            if (!isValid)
            {
                _logger.LogWarning("QR code signature verification failed for ticket ID: {TicketId}", ticketIdStr);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying QR code signature: {QRCodeData}", qrCodeData);
            return false;
        }
    }

    /// <summary>
    /// Validates a QR code and marks the ticket as used.
    /// Checks signature, usage status, and event association.
    /// Uses database transaction to prevent double-scanning.
    /// Validates: Requirements 6.6, 6.7, 9.3, 9.4, 9.5, 9.6
    /// </summary>
    public async Task<QRCodeValidationResult> ValidateQRCodeAsync(string qrCodeData, Guid eventId)
    {
        _logger.LogInformation("Validating QR code for event {EventId}", eventId);

        // Step 1: Verify HMAC-SHA256 signature
        if (!VerifyQRCodeSignature(qrCodeData))
        {
            _logger.LogWarning("QR code signature verification failed");
            return new QRCodeValidationResult
            {
                IsValid = false,
                Error = "Invalid QR code signature. This ticket may be fraudulent."
            };
        }

        // Step 2: Extract ticket ID from QR code
        var parts = qrCodeData.Split(':');
        if (!Guid.TryParse(parts[0], out var ticketId))
        {
            _logger.LogWarning("Failed to parse ticket ID from QR code");
            return new QRCodeValidationResult
            {
                IsValid = false,
                Error = "Invalid QR code format."
            };
        }

        // Step 3: Use transaction to atomically check and update ticket status
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Load ticket with related data
            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .Include(t => t.TicketType)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
            {
                _logger.LogWarning("Ticket {TicketId} not found", ticketId);
                await transaction.RollbackAsync();
                return new QRCodeValidationResult
                {
                    IsValid = false,
                    Error = "Ticket not found."
                };
            }

            // Step 4: Validate QR timestamp window
            // Extract timestamp from QR code payload
            long qrTimestamp;
            try
            {
                qrTimestamp = HmacHelper.ExtractTimestamp(qrCodeData);
            }
            catch (FormatException)
            {
                _logger.LogWarning("Failed to extract timestamp from QR code for ticket {TicketId}", ticketId);
                await transaction.RollbackAsync();
                return new QRCodeValidationResult
                {
                    IsValid = false,
                    Error = "Invalid QR code format: cannot extract timestamp."
                };
            }

            var qrDateTime = DateTimeOffset.FromUnixTimeSeconds(qrTimestamp).UtcDateTime;

            // Validate: timestamp >= purchaseDate (ticket.CreatedAt) with 5-min clock-skew tolerance
            if (qrDateTime < ticket.CreatedAt.AddMinutes(-5))
            {
                _logger.LogWarning("QR timestamp {QrTimestamp} is before ticket purchase date {PurchaseDate} for ticket {TicketId}",
                    qrDateTime, ticket.CreatedAt, ticketId);
                await transaction.RollbackAsync();
                return new QRCodeValidationResult
                {
                    IsValid = false,
                    Error = "QR code timestamp is outside valid window.",
                    Ticket = ticket
                };
            }

            // Validate: timestamp <= event.EndDate + 24h
            var maxValidDate = ticket.Event.Date.AddHours(24);
            if (qrDateTime > maxValidDate)
            {
                _logger.LogWarning("QR timestamp {QrTimestamp} is after event end + 24h {MaxDate} for ticket {TicketId}",
                    qrDateTime, maxValidDate, ticketId);
                await transaction.RollbackAsync();
                return new QRCodeValidationResult
                {
                    IsValid = false,
                    Error = "QR code timestamp is outside valid window.",
                    Ticket = ticket
                };
            }

            // Validate: timestamp <= now (not in the future)
            if (qrDateTime > DateTime.UtcNow)
            {
                _logger.LogWarning("QR timestamp {QrTimestamp} is in the future for ticket {TicketId}",
                    qrDateTime, ticketId);
                await transaction.RollbackAsync();
                return new QRCodeValidationResult
                {
                    IsValid = false,
                    Error = "QR code timestamp is outside valid window.",
                    Ticket = ticket
                };
            }

            // Step 5: Check if ticket has already been used (double-scan prevention)
            if (ticket.IsUsed)
            {
                _logger.LogWarning("Ticket {TicketId} has already been used at {UsedAt}",
                    ticketId, ticket.UsedAt);
                await transaction.RollbackAsync();
                return new QRCodeValidationResult
                {
                    IsValid = false,
                    Error = $"Ticket already used on {ticket.UsedAt:yyyy-MM-dd HH:mm:ss} UTC.",
                    Ticket = ticket
                };
            }

            // Step 6: Check event association
            if (ticket.EventId != eventId)
            {
                _logger.LogWarning("Ticket {TicketId} is for event {TicketEventId}, but scanned at event {ScannedEventId}",
                    ticketId, ticket.EventId, eventId);
                await transaction.RollbackAsync();
                return new QRCodeValidationResult
                {
                    IsValid = false,
                    Error = $"Ticket is for event '{ticket.Event.Name}', not this event.",
                    Ticket = ticket
                };
            }

            // Step 7: Mark ticket as used with timestamp
            ticket.IsUsed = true;
            ticket.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Ticket {TicketId} validated and marked as used for event {EventId}",
                ticketId, eventId);

            return new QRCodeValidationResult
            {
                IsValid = true,
                Ticket = ticket
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating QR code for ticket {TicketId}", ticketId);
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Looks up tickets by email and DNI.
    /// Returns all matching tickets with QR codes.
    /// Validates: Requirements 8.2, 8.3, 8.5
    /// </summary>
    public async Task<IEnumerable<Ticket>> LookupTicketsAsync(string email, string dni)
    {
        _logger.LogInformation("Looking up tickets for email hash {EmailHash} and DNI hash {DNIHash}",
            LogRedactor.HashIdentifier(email), LogRedactor.HashIdentifier(dni));

        // Query tickets matching both email AND DNI
        var tickets = await _context.Tickets
            .Include(t => t.Event)
            .Include(t => t.TicketType)
            .Where(t => t.PurchaserEmail == email && t.PurchaserDNI == dni)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("Found {Count} tickets for email hash {EmailHash} and DNI hash {DNIHash}",
            tickets.Count, LogRedactor.HashIdentifier(email), LogRedactor.HashIdentifier(dni));

        return tickets;
    }

    /// <summary>
    /// Looks up tickets by email only and returns info-only response (no QR fields).
    /// Groups tickets by event for a summary response.
    /// Validates: Batch 5 — B5.1
    /// </summary>
    public async Task<IEnumerable<TicketLookupInfoResponse>> LookupTicketsByEmailAsync(string email)
    {
        _logger.LogInformation("Looking up tickets by email hash {EmailHash}", LogRedactor.HashIdentifier(email));

        var tickets = await _context.Tickets
            .Include(t => t.Event)
            .Include(t => t.TicketType)
            .Where(t => t.PurchaserEmail == email)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        if (tickets.Count == 0)
        {
            _logger.LogInformation("No tickets found for email hash {EmailHash}", LogRedactor.HashIdentifier(email));
            return Enumerable.Empty<TicketLookupInfoResponse>();
        }

        // Group by event to return a summary without QR codes
        var responses = tickets
            .GroupBy(t => new { t.EventId, EventName = t.Event.Name, t.Event.Date, TicketTypeName = t.TicketType.Name })
            .Select(g => new TicketLookupInfoResponse
            {
                EventName = g.Key.EventName,
                EventDate = g.Key.Date,
                TicketType = g.Key.TicketTypeName,
                Quantity = g.Count(),
                PurchaserEmail = MaskEmail(email)
            })
            .ToList();

        _logger.LogInformation("Found {Count} ticket groups for email hash {EmailHash}",
            responses.Count, LogRedactor.HashIdentifier(email));

        return responses;
    }

    /// <summary>
    /// Resends tickets by email. Always returns success to prevent info leak.
    /// Validates: Batch 5 — B5.2
    /// </summary>
    public async Task<bool> ResendTicketsByEmailAsync(string email)
    {
        _logger.LogInformation("Resend tickets requested for email hash {EmailHash}", LogRedactor.HashIdentifier(email));

        try
        {
            var tickets = await _context.Tickets
                .Include(t => t.Event)
                .Include(t => t.TicketType)
                .Where(t => t.PurchaserEmail == email)
                .ToListAsync();

            if (tickets.Count == 0)
            {
                _logger.LogInformation("No tickets found for email hash {EmailHash} — still returning success", LogRedactor.HashIdentifier(email));
                return true;
            }

            _logger.LogInformation("Found {Count} tickets to resend for email hash {EmailHash}",
                tickets.Count, LogRedactor.HashIdentifier(email));

            var eventGroups = tickets.GroupBy(t => t.EventId);

            foreach (var eventGroup in eventGroups)
            {
                var eventTickets = eventGroup.ToList();
                var eventDetails = eventTickets.First().Event;

                try
                {
                    var emailService = _serviceProvider.GetRequiredService<IEmailService>();
                    var result = await emailService.SendResendEmailAsync(email, eventTickets, eventDetails);

                    if (!result.Success)
                    {
                        _logger.LogError(
                            "Failed to resend tickets for event {EventId} to {EmailHash}: {Error}",
                            eventDetails.Id, LogRedactor.HashIdentifier(email), result.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error resending tickets for event {EventId} to {EmailHash}",
                        eventDetails.Id, LogRedactor.HashIdentifier(email));
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ticket resend for email hash {EmailHash}", LogRedactor.HashIdentifier(email));
            return true;
        }
    }

    /// <summary>
    /// Masks an email address for display (e.g., "b***@test.com").
    /// </summary>
    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            return email;
        }

        var parts = email.Split('@');
        var localPart = parts[0];
        var domain = parts[1];

        if (localPart.Length <= 1)
        {
            return $"***@{domain}";
        }

        return $"{localPart[0]}***@{domain}";
    }

}
