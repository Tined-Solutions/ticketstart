using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Payment controller for Mercado Pago checkout preferences and webhooks.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a Mercado Pago checkout preference for a reservation.
    /// Requires authentication.
    /// Validates: Requirements 5.1, 16.2, 16.3
    /// </summary>
    [HttpPost("create-preference")]
    [Authorize]
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

            if (!result.Success)
            {
                _logger.LogWarning("Webhook processing failed for payment {PaymentId}: {Error}", result.PaymentId, result.Error);
                return Unauthorized(new { error = result.Error });
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
}
