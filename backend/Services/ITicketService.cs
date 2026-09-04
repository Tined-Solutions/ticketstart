using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for ticket generation, QR code creation, and validation.
/// Handles cryptographically signed QR codes using HMAC-SHA256.
/// </summary>
public interface ITicketService
{
    /// <summary>
    /// Creates tickets from a confirmed reservation.
    /// Generates unique QR codes with HMAC-SHA256 signatures for each ticket.
    /// Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5
    /// </summary>
    /// <param name="reservationId">The confirmed reservation identifier</param>
    /// <param name="purchaserEmail">Email of the purchaser</param>
    /// <param name="purchaserDNI">DNI of the purchaser</param>
    /// <returns>List of created tickets with QR codes</returns>
    /// <exception cref="KeyNotFoundException">Thrown when reservation not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when reservation is not confirmed</exception>
    Task<IEnumerable<Ticket>> CreateTicketsAsync(Guid reservationId, string purchaserEmail, string purchaserDNI);

    /// <summary>
    /// Generates a QR code string for a ticket with HMAC-SHA256 signature.
    /// Format: {ticketId}:{timestamp}:{signature}
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    /// <param name="ticketId">The ticket identifier</param>
    /// <returns>QR code data string with signature</returns>
    string GenerateQRCode(Guid ticketId);

    /// <summary>
    /// Generates a visual QR code image as a base64-encoded PNG.
    /// Uses QRCoder library to create the image.
    /// Validates: Requirement 6.5
    /// </summary>
    /// <param name="qrCodeData">The QR code data string</param>
    /// <returns>Base64-encoded PNG image</returns>
    string GenerateQRCodeImage(string qrCodeData);

    /// <summary>
    /// Verifies the HMAC-SHA256 signature of a QR code.
    /// Validates: Requirements 6.6, 6.7
    /// </summary>
    /// <param name="qrCodeData">The QR code data string to verify</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    bool VerifyQRCodeSignature(string qrCodeData);

    /// <summary>
    /// Validates a QR code and marks the ticket as used.
    /// Checks signature, usage status, and event association.
    /// Uses database transaction to prevent double-scanning.
    /// Validates: Requirements 6.6, 6.7, 9.3, 9.4, 9.5, 9.6
    /// </summary>
    /// <param name="qrCodeData">The QR code data string to validate</param>
    /// <param name="eventId">The event ID where the ticket is being scanned</param>
    /// <returns>Validation result with ticket information</returns>
    Task<QRCodeValidationResult> ValidateQRCodeAsync(string qrCodeData, Guid eventId);

    /// <summary>
    /// Looks up tickets by email and DNI.
    /// Returns all matching tickets with QR codes.
    /// Validates: Requirements 8.2, 8.3, 8.5
    /// </summary>
    /// <param name="email">Purchaser email</param>
    /// <param name="dni">Purchaser DNI</param>
    /// <returns>List of matching tickets</returns>
    Task<IEnumerable<Ticket>> LookupTicketsAsync(string email, string dni);

    /// <summary>
    /// Looks up tickets by email only and returns info-only response (no QR fields).
    /// Used for public ticket lookup without authentication.
    /// Validates: Batch 5 — B5.1
    /// </summary>
    /// <param name="email">Purchaser email</param>
    /// <returns>List of ticket info responses without QR data</returns>
    Task<IEnumerable<TicketLookupInfoResponse>> LookupTicketsByEmailAsync(string email);

    /// <summary>
    /// Looks up active (unused) tickets by email and DNI and returns info-only response
    /// (no QR fields). DNI is matched by digits only; email is matched case-insensitively
    /// after trimming. Used for public ticket lookup without authentication.
    /// Validates: Batch 5 — B5.1
    /// </summary>
    /// <param name="email">Purchaser email</param>
    /// <param name="dni">Purchaser DNI</param>
    /// <returns>List of active ticket info responses without QR data</returns>
    Task<IEnumerable<TicketLookupInfoResponse>> LookupActiveTicketsByEmailAndDniAsync(string email, string dni);

    /// <summary>
    /// Resends tickets by email grouped by event. Returns generic success
    /// regardless of whether tickets exist for the given email (no info leak).
    /// Validates: Batch 5 — B5.2
    /// </summary>
    /// <param name="email">Purchaser email</param>
    /// <returns>True (always returns success to prevent info leak)</returns>
    Task<bool> ResendTicketsByEmailAsync(string email);

    /// <summary>
    /// Returns the QR payload for a ticket, or null when the ticket does not
    /// exist. Used by the public QR image endpoint — the PNG is rendered on
    /// demand from this immutable payload.
    /// </summary>
    Task<string?> GetTicketQrCodeDataAsync(Guid ticketId);
}

/// <summary>
/// Result of QR code validation.
/// </summary>
public class QRCodeValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public Ticket? Ticket { get; set; }
}

/// <summary>
/// Response model for ticket lookup.
/// </summary>
public class TicketLookupResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string EventLocation { get; set; } = string.Empty;
    public string TicketTypeName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string QRCodeData { get; set; } = string.Empty;
    public string QRCodeImage { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response model for email-only ticket lookup (no QR fields — info only).
/// Used by the public ticket lookup endpoint (B5.1).
/// </summary>
public class TicketLookupInfoResponse
{
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string EventLocation { get; set; } = string.Empty;
    public string TicketType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string PurchaserEmail { get; set; } = string.Empty;
}

/// <summary>
/// Request model for ticket resend endpoint.
/// </summary>
public class ResendTicketsRequest
{
    public string Email { get; set; } = string.Empty;
    public string TurnstileToken { get; set; } = string.Empty;
}

/// <summary>
/// Request model for QR code validation.
/// </summary>
public class ValidateQRCodeRequest
{
    public string QRCodeData { get; set; } = string.Empty;
    public Guid EventId { get; set; }
}

/// <summary>
/// Response model for QR code validation.
/// </summary>
public class ValidateQRCodeResponse
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public TicketValidationDetails? Ticket { get; set; }
}

/// <summary>
/// Ticket details returned in validation response.
/// </summary>
public class TicketValidationDetails
{
    public Guid Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string TicketTypeName { get; set; } = string.Empty;
    public string PurchaserEmail { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public bool IsRefunded { get; set; }
    public DateTime? RefundedAt { get; set; }
}
