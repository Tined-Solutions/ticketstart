using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// HTTP client implementation for Mercado Pago API.
/// </summary>
public class MercadoPagoClient : IMercadoPagoClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MercadoPagoClient> _logger;

    public MercadoPagoClient(HttpClient httpClient, IOptions<MercadoPagoOptions> options, ILogger<MercadoPagoClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var accessToken = options.Value.AccessToken;
        if (string.IsNullOrEmpty(accessToken) || accessToken.StartsWith("YOUR_"))
        {
            _logger.LogWarning("Mercado Pago access token is not configured");
        }

        _httpClient.BaseAddress = new Uri("https://api.mercadopago.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <inheritdoc />
    public async Task<MercadoPagoPreferenceResponse> CreatePreferenceAsync(
        MercadoPagoPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            items = request.Items.Select(i => new
            {
                title = i.Title,
                quantity = i.Quantity,
                unit_price = i.UnitPrice,
                currency_id = "ARS"
            }),
            external_reference = request.ExternalReference
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Creating Mercado Pago preference for external reference {ExternalReference}", request.ExternalReference);

        var response = await _httpClient.PostAsync("checkout/preferences", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = JsonDocument.Parse(responseJson);

        return new MercadoPagoPreferenceResponse
        {
            Id = document.RootElement.GetProperty("id").GetString() ?? string.Empty,
            InitPoint = document.RootElement.GetProperty("init_point").GetString() ?? string.Empty
        };
    }

    /// <inheritdoc />
    public async Task<MercadoPagoRefundResponse> RefundPaymentAsync(
        string paymentId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var body = new { amount };
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Refunding Mercado Pago payment {PaymentId} for amount {Amount}", paymentId, amount);

        var response = await _httpClient.PostAsync($"v1/payments/{paymentId}/refunds", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = JsonDocument.Parse(responseJson);

        return new MercadoPagoRefundResponse
        {
            Id = document.RootElement.GetProperty("id").GetString() ?? string.Empty,
            PaymentId = paymentId,
            Amount = amount,
            Status = document.RootElement.TryGetProperty("status", out var status) ? status.GetString() ?? "approved" : "approved"
        };
    }
}
