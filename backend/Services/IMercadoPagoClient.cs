namespace TicketeraOnline.Api.Services;

/// <summary>
/// Client for Mercado Pago API operations.
/// Abstracted for testability.
/// </summary>
public interface IMercadoPagoClient
{
    /// <summary>
    /// Creates a checkout preference.
    /// </summary>
    Task<MercadoPagoPreferenceResponse> CreatePreferenceAsync(
        MercadoPagoPreferenceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refunds a payment.
    /// </summary>
    Task<MercadoPagoRefundResponse> RefundPaymentAsync(
        string paymentId,
        decimal amount,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request body for creating a Mercado Pago preference.
/// </summary>
public class MercadoPagoPreferenceRequest
{
    public string ExternalReference { get; set; } = string.Empty;
    public List<MercadoPagoItemRequest> Items { get; set; } = new();
}

public class MercadoPagoItemRequest
{
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Response from creating a Mercado Pago preference.
/// </summary>
public class MercadoPagoPreferenceResponse
{
    public string Id { get; set; } = string.Empty;
    public string InitPoint { get; set; } = string.Empty;
}

/// <summary>
/// Response from refunding a Mercado Pago payment.
/// </summary>
public class MercadoPagoRefundResponse
{
    public string Id { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
