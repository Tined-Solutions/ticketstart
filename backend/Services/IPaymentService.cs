namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service interface for Mercado Pago payment integration and webhook processing.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Creates a Mercado Pago payment preference for a reservation.
    /// Validates the HMAC-SHA256 reservation token before creating the preference.
    /// Validates: Requirements 5.1, 5.2, 5.3, IDOR protection for guest checkout
    /// </summary>
    /// <param name="reservationId">Active reservation identifier</param>
    /// <param name="token">HMAC-SHA256 reservation token</param>
    /// <returns>Checkout URL and preference identifier</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the reservation token is missing or invalid</exception>
    Task<PaymentPreference> CreatePaymentPreferenceAsync(Guid reservationId, string token);

    /// <summary>
    /// Processes a Mercado Pago webhook notification.
    /// Validates signature, confirms reservation and creates tickets on success,
    /// or releases reservation and initiates refund on failure.
    /// Validates: Requirements 5.5, 5.6, 5.7, 5.8, 16.5
    /// </summary>
    /// <param name="payload">Webhook payload</param>
    /// <param name="signature">HMAC-SHA256 signature header</param>
    /// <param name="rawBody">Raw request body bytes for signature validation. When null, falls back to string-based validation.</param>
    /// <returns>Webhook processing result</returns>
    Task<WebhookResult> ProcessWebhookAsync(WebhookPayload payload, string signature, byte[]? rawBody = null);

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
    public WebhookFailureType FailureType { get; set; } = WebhookFailureType.None;
}

/// <summary>
/// Classifies a webhook failure so the controller can choose the right HTTP status.
/// </summary>
public enum WebhookFailureType
{
    None,
    Authentication,
    Processing
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
    public string Token { get; set; } = string.Empty;
}
