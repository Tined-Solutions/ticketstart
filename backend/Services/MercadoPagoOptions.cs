namespace TicketeraOnline.Api.Services;

/// <summary>
/// Configuration options for Mercado Pago integration.
/// </summary>
public class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public string AccessToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string FrontendUrl { get; set; } = string.Empty;
    public string WebhookBaseUrl { get; set; } = string.Empty;
}
