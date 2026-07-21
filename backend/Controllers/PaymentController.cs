using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Payment controller for Mercado Pago checkout preferences and webhooks.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentController : TicketeraControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        ILogger<PaymentController> logger,
        IAuditLogService auditLogService)
    {
        _paymentService = paymentService;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Creates a Mercado Pago checkout preference for a reservation.
    /// Public endpoint; buyers purchase as guests.
    /// Validates: Requirements 5.1, 16.2, 16.3
    /// </summary>
    [HttpPost("create-preference")]
    [AllowAnonymous]
    public async Task<IActionResult> CreatePreference([FromBody] CreatePaymentPreferenceRequest request)
    {
        if (request == null || request.ReservationId == Guid.Empty)
        {
            _logger.LogWarning("Invalid create preference request");
            return BadRequest(new { error = "ReservationId is required" });
        }

        if (string.IsNullOrEmpty(request.Token))
        {
            _logger.LogWarning("Missing reservation token for reservation {ReservationId}", request.ReservationId);
            return Unauthorized(new { error = "Invalid reservation token" });
        }

        try
        {
            var preference = await _paymentService.CreatePaymentPreferenceAsync(request.ReservationId, request.Token);

            _logger.LogInformation(
                "Created preference {PreferenceId} for reservation {ReservationId}",
                preference.PreferenceId, request.ReservationId);

            return Ok(new
            {
                checkoutUrl = preference.CheckoutUrl,
                preferenceId = preference.PreferenceId
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Invalid reservation token for reservation {ReservationId}", request.ReservationId);
            return Unauthorized(new { error = "Invalid reservation token" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Reservation {ReservationId} not found", request.ReservationId);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Reservation {ReservationId} cannot be used for payment", request.ReservationId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating preference for reservation {ReservationId}", request.ReservationId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while creating the payment preference" });
        }
    }

    /// <summary>
    /// Receives Mercado Pago webhook notifications.
    /// Public endpoint; validates signature when present (dashboard webhooks).
    /// notification_url webhooks from preferences may not include a signature.
    /// ALWAYS returns 200 OK to MP — non-200 causes MP to retry indefinitely.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(
        [FromHeader(Name = "x-signature")] string? signature = null)
    {
        MercadoPagoWebhookEnvelope envelope;
        byte[] rawBody;

        try
        {
            // Read raw bytes for HMAC validation
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var bodyString = await reader.ReadToEndAsync();
            rawBody = Encoding.UTF8.GetBytes(bodyString);
            Request.Body.Position = 0;

            envelope = JsonSerializer.Deserialize<MercadoPagoWebhookEnvelope>(bodyString)
                ?? new MercadoPagoWebhookEnvelope();
        }
        catch (Exception ex)
        {
            // Cannot even parse the body — ACK to stop MP retries
            _logger.LogWarning(ex, "Failed to parse webhook body — returning 200 ACK");
            return Ok(new { status = "acknowledged" });
        }

        try
        {
            var result = await _paymentService.ProcessWebhookAsync(envelope, signature ?? string.Empty, rawBody);

            var dataId = envelope.Data?.Id ?? result.PaymentId;

            await TryLogAuditAsync(new AuditLogContext(
                UserId: null,
                Action: AuditActionType.ProcessWebhook,
                Resource: AuditResourceType.Payment,
                ResourceId: null,
                Details: $"Webhook processed for payment {dataId} with action {envelope.Action}; success={result.Success}",
                UserIdentifier: "System"));

            if (!result.Success)
            {
                if (result.FailureType == WebhookFailureType.Authentication)
                {
                    // Return 200 ACK even on auth failure — non-200 makes MP retry indefinitely.
                    // The warning is logged above; MP considers the notification delivered.
                    _logger.LogWarning("Webhook authentication failed for payment {PaymentId} — returning 200 ACK", result.PaymentId);
                    return Ok(new { paymentId = result.PaymentId, status = "acknowledged", reason = "signature_validation_failed" });
                }

                _logger.LogWarning("Webhook processing failed for payment {PaymentId}", result.PaymentId);
                return Ok(new { paymentId = result.PaymentId, status = "failed", error = "PROCESSING_FAILED" });
            }

            _logger.LogInformation("Webhook processed successfully for payment {PaymentId}", result.PaymentId);
            return Ok(new { paymentId = result.PaymentId });
        }
        catch (Exception ex)
        {
            // Catch-all: log the error but ALWAYS return 200 to MP
            _logger.LogError(ex, "Unexpected error processing webhook for payment {PaymentId}", envelope.Data?.Id);
            return Ok(new { status = "acknowledged" });
        }
    }

    /// <summary>
    /// Retries sending emails for any pending email send rows.
    /// Admin-gated: requires RequireAdminRole policy.
    /// The X-CSRF-PROTECT header is required (this endpoint is NOT in the CSRF exemption list —
    /// admin frontend must send it).
    /// </summary>
    [HttpPost("emails/retry-pending")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> RetryPendingEmails()
    {
        try
        {
            var result = await _paymentService.RetryPendingEmailsAsync();
            _logger.LogInformation(
                "Admin retried pending emails: {Attempted} attempted, {Sent} sent, {Failed} failed, {Exhausted} exhausted",
                result.Attempted, result.Sent, result.Failed, result.Exhausted);
            return Ok(new
            {
                attempted = result.Attempted,
                sent = result.Sent,
                failed = result.Failed,
                exhausted = result.Exhausted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying pending emails");
            return StatusCode(500, new { error = "An error occurred while retrying pending emails" });
        }
    }

    /// <summary>
    /// Confirms a payment after the user returns from the Mercado Pago checkout flow.
    /// Public endpoint; called by the frontend with the preference_id from the URL.
    /// </summary>
    [HttpPost("confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.PreferenceId))
        {
            return BadRequest(new { error = "PreferenceId is required" });
        }

        try
        {
            var result = await _paymentService.ConfirmPaymentAsync(request.PreferenceId);

            if (!result.Success)
            {
                return Ok(new { status = "pending", error = result.Error });
            }

            return Ok(new { status = "confirmed", paymentId = result.PaymentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment for preference {PreferenceId}", request.PreferenceId);
            return StatusCode(500, new { error = "An error occurred while confirming the payment" });
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

public class ConfirmPaymentRequest
{
    public string PreferenceId { get; set; } = string.Empty;
}
