namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for Mercado Pago payment integration and webhook processing.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Creates a Mercado Pago payment preference for a reservation.
    /// Validates: Requirements 5.1, 5.2, 5.3
    /// </summary>
    /// <param name="reservationId">Active reservation identifier</param>
    /// <returns>Checkout URL and preference identifier</returns>
    Task<PaymentPreference> CreatePaymentPreferenceAsync(Guid reservationId);

    /// <summary>
    /// Processes a Mercado Pago webhook notification.
    /// Validates signature, confirms reservation and creates tickets on success,
    /// or releases reservation and initiates refund on failure.
    /// Validates: Requirements 5.5, 5.6, 5.7, 5.8, 16.5
    /// </summary>
    /// <param name="payload">Webhook payload</param>
    /// <param name="signature">HMAC-SHA256 signature header</param>
    /// <returns>Webhook processing result</returns>
    Task<WebhookResult> ProcessWebhookAsync(WebhookPayload payload, string signature);

    /// <summary>
    /// Initiates a refund for a payment when stock cannot be fulfilled.
    /// Logs the refund transaction.
    /// Validates: Requirements 12.2, 12.3
    /// </summary>
    /// <param name="mercadoPagoId">Mercado Pago payment identifier</param>
    /// <param name="amount">Amount to refund</param>
    /// <param name="reservationId">Reservation associated with the payment</param>
    /// <returns>Refund result</returns>
    Task<RefundResult> InitiateRefundAsync(string mercadoPagoId, decimal amount, Guid reservationId);
}

/// <summary>
/// Mercado Pago checkout preference result.
/// </summary>
public class PaymentPreference
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string PreferenceId { get; set; } = string.Empty;
}

/// <summary>
/// Webhook payload received from the payment gateway.
/// </summary>
public class WebhookPayload
{
    public string PaymentId { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Result of webhook processing.
/// </summary>
public class WebhookResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string PaymentId { get; set; } = string.Empty;
}

/// <summary>
/// Result of a refund request.
/// </summary>
public class RefundResult
{
    public bool Success { get; set; }
    public string? RefundId { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Request to create a payment preference.
/// </summary>
public class CreatePaymentPreferenceRequest
{
    public Guid ReservationId { get; set; }
}
