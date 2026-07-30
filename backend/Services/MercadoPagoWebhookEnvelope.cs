using System.Text.Json.Serialization;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Mercado Pago webhook notification envelope.
/// MP sends: { "action": "payment.updated", "type": "payment", "data": { "id": "123456789" } }
/// Uses [JsonPropertyName] for snake_case mapping to avoid global policy regressions.
/// </summary>
public class MercadoPagoWebhookEnvelope
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("data")]
    public MercadoPagoWebhookData? Data { get; set; }
}

/// <summary>
/// Data node inside a Mercado Pago webhook notification.
/// Contains the canonical payment ID used to look up the full payment details via GET /v1/payments/{id}.
/// </summary>
public class MercadoPagoWebhookData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}
