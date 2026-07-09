using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Controller for ticket operations including lookup and validation.
/// Handles QR code validation for staff and ticket retrieval for users.
/// </summary>
[ApiController]
[Route("api/tickets")]
public class TicketController : TicketeraControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<TicketController> _logger;

    public TicketController(
        ITicketService ticketService,
        ILogger<TicketController> logger,
        IAuditLogService auditLogService)
    {
        _ticketService = ticketService;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Looks up tickets by email and DNI.
    /// Validates: Requirements 8.1, 8.2, 8.3, 8.5
    /// </summary>
    /// <param name="email">Purchaser email address</param>
    /// <param name="dni">Purchaser DNI number</param>
    /// <returns>List of matching tickets with QR codes</returns>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(IEnumerable<TicketLookupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<TicketLookupResponse>>> LookupTickets(
        [FromQuery] string email,
        [FromQuery] string dni)
    {
        _logger.LogInformation("Ticket lookup request for email {Email} and DNI {DniHash}", email, LogRedactor.HashIdentifier(dni));

        // Validate input
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Ticket lookup failed: Email is required");
            return BadRequest(new { error = "Email is required" });
        }

        if (string.IsNullOrWhiteSpace(dni))
        {
            _logger.LogWarning("Ticket lookup failed: DNI is required");
            return BadRequest(new { error = "DNI is required" });
        }

        try
        {
            // Look up tickets
            var tickets = await _ticketService.LookupTicketsAsync(email, dni);

            // Map to response DTOs with QR code images
            var response = tickets.Select(ticket => new TicketLookupResponse
            {
                Id = ticket.Id,
                EventId = ticket.EventId,
                EventName = ticket.Event.Name,
                EventDate = ticket.Event.Date,
                EventLocation = ticket.Event.Location,
                TicketTypeName = ticket.TicketType.Name,
                Price = ticket.TicketType.Price,
                QRCodeData = ticket.QRCodeData,
                QRCodeImage = _ticketService.GenerateQRCodeImage(ticket.QRCodeData),
                IsUsed = ticket.IsUsed,
                UsedAt = ticket.UsedAt,
                CreatedAt = ticket.CreatedAt
            }).ToList();

            _logger.LogInformation("Ticket lookup successful: Found {Count} tickets", response.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ticket lookup for email {Email} and DNI {DniHash}", email, LogRedactor.HashIdentifier(dni));
            return StatusCode(500, new { error = "An error occurred while looking up tickets" });
        }
    }

    /// <summary>
    /// Validates a QR code and marks the ticket as used.
    /// Staff/Admin only endpoint.
    /// Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7
    /// </summary>
    /// <param name="request">QR code validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate")]
    [Authorize(Policy = "RequireStaffRole")]
    [ProducesResponseType(typeof(ValidateQRCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ValidateQRCodeResponse>> ValidateQRCode(
        [FromBody] ValidateQRCodeRequest request)
    {
        _logger.LogInformation("QR code validation request for event {EventId}", request.EventId);

        // Validate input
        if (string.IsNullOrWhiteSpace(request.QRCodeData))
        {
            _logger.LogWarning("QR code validation failed: QRCodeData is required");
            return BadRequest(new { error = "QRCodeData is required" });
        }

        if (request.EventId == Guid.Empty)
        {
            _logger.LogWarning("QR code validation failed: EventId is required");
            return BadRequest(new { error = "EventId is required" });
        }

        try
        {
            // Validate QR code
            var result = await _ticketService.ValidateQRCodeAsync(request.QRCodeData, request.EventId);

            // Map to response DTO
            var response = new ValidateQRCodeResponse
            {
                IsValid = result.IsValid,
                Error = result.Error
            };

            if (result.Ticket != null)
            {
                response.Ticket = new TicketValidationDetails
                {
                    Id = result.Ticket.Id,
                    EventName = result.Ticket.Event.Name,
                    TicketTypeName = result.Ticket.TicketType.Name,
                    PurchaserEmail = result.Ticket.PurchaserEmail,
                    IsUsed = result.Ticket.IsUsed,
                    UsedAt = result.Ticket.UsedAt
                };
            }

            if (result.IsValid)
            {
                _logger.LogInformation("QR code validation successful for ticket {TicketId}",
                    result.Ticket?.Id);
            }
            else
            {
                _logger.LogWarning("QR code validation failed: {Error}", result.Error);
            }

            _ = TryGetUserId(out var userId);
            await TryLogAuditAsync(new AuditLogContext(
                UserId: userId,
                Action: AuditActionType.ValidateQr,
                Resource: AuditResourceType.Ticket,
                ResourceId: result.Ticket?.Id,
                Details: $"QR validation for event {request.EventId}; valid={result.IsValid}"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during QR code validation for event {EventId}", request.EventId);
            return StatusCode(500, new { error = "An error occurred while validating the QR code" });
        }
    }

    private async Task TryLogAuditAsync(AuditLogContext context)
    {
        try
        {
            await _auditLogService.LogActionAsync(context);
        }
        catch (Exception ex)
        {
            try
            {
                _logger.LogError(ex,
                    "Audit logging failed for action {ActionType} resource {ResourceType} id {ResourceId}; continuing with response",
                    context.Action, context.Resource, context.ResourceId);
            }
            catch
            {
                // Logger failure must not break the request.
            }
        }
    }
}
