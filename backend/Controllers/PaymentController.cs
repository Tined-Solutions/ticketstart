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

        try
        {
            var preference = await _paymentService.CreatePaymentPreferenceAsync(request.ReservationId);

            _logger.LogInformation(
                "Created preference {PreferenceId} for reservation {ReservationId}",
                preference.PreferenceId, request.ReservationId);

            return Ok(new
            {
                checkoutUrl = preference.CheckoutUrl,
                preferenceId = preference.PreferenceId
            });
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
    /// Public endpoint; validates signature.
    /// Validates: Requirements 5.5, 5.8, 16.5
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(
        [FromBody] WebhookPayload payload,
        [FromHeader(Name = "x-signature")] string? signature = null)
    {
        if (payload == null || string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Webhook received without payload or signature");
            return Unauthorized(new { error = "Missing webhook payload or signature" });
        }

        try
        {
            var result = await _paymentService.ProcessWebhookAsync(payload, signature);

            await TryLogAuditAsync(new AuditLogContext(
                UserId: Guid.Empty,
                Action: AuditActionType.ProcessWebhook,
                Resource: AuditResourceType.Payment,
                ResourceId: null,
                Details: $"Webhook processed for payment {result.PaymentId} with status {payload.Status}; success={result.Success}"));

            if (!result.Success)
            {
                if (result.FailureType == WebhookFailureType.Authentication)
                {
                    _logger.LogWarning("Webhook authentication failed for payment {PaymentId}", result.PaymentId);
                    return Unauthorized(new { error = "Invalid webhook signature" });
                }

                _logger.LogWarning("Webhook processing failed for payment {PaymentId}", result.PaymentId);
                return Ok(new { paymentId = result.PaymentId, status = "failed", error = "PROCESSING_FAILED" });
            }

            _logger.LogInformation("Webhook processed successfully for payment {PaymentId}", result.PaymentId);
            return Ok(new { paymentId = result.PaymentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing webhook for payment {PaymentId}", payload.PaymentId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while processing the webhook" });
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
