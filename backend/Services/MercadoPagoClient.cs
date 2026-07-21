using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static readonly JsonSerializerOptions _preferenceJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
            external_reference = request.ExternalReference,
            back_urls = request.BackUrls != null ? new
            {
                success = request.BackUrls.Success,
                failure = request.BackUrls.Failure,
                pending = request.BackUrls.Pending
            } : null,
            notification_url = request.NotificationUrl
            // auto_return omitted: requires publicly-accessible back_urls (localhost → rejected by MP)
        };

        var json = JsonSerializer.Serialize(body, _preferenceJsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Creating Mercado Pago preference for external reference {ExternalReference}", request.ExternalReference);
        _logger.LogDebug("Mercado Pago preference request body: {RequestBody}", json);

        var response = await _httpClient.PostAsync("checkout/preferences", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Mercado Pago preference creation failed with status {StatusCode}. Request: {RequestBody}. Response: {ErrorBody}",
                (int)response.StatusCode, json, errorBody);
        }

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

    /// <inheritdoc />
    public async Task<MercadoPagoPreferenceDetail?> GetPreferenceAsync(
        string preferenceId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"checkout/preferences/{preferenceId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        return new MercadoPagoPreferenceDetail
        {
            Id = doc.RootElement.GetProperty("id").GetString() ?? "",
            ExternalReference = doc.RootElement.TryGetProperty("external_reference", out var er) ? er.GetString() ?? "" : ""
        };
    }

    /// <inheritdoc />
    public async Task<List<MercadoPagoPaymentInfo>> SearchPaymentsByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        var url = $"v1/payments/search?external_reference={Uri.EscapeDataString(externalReference)}&sort=date_created&criteria=desc";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");

        var payments = new List<MercadoPagoPaymentInfo>();
        foreach (var r in results.EnumerateArray())
        {
            payments.Add(new MercadoPagoPaymentInfo
            {
                Id = r.GetProperty("id").GetString() ?? "",
                Status = r.GetProperty("status").GetString() ?? ""
            });
        }
        return payments;
    }

    /// <inheritdoc />
    public async Task<MercadoPagoPaymentDetail?> GetPaymentByIdAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        var url = $"v1/payments/{Uri.EscapeDataString(paymentId)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new MercadoPagoPaymentDetail
        {
            Id = root.GetProperty("id").ValueKind == System.Text.Json.JsonValueKind.Number
                ? root.GetProperty("id").GetInt64().ToString()
                : root.GetProperty("id").GetString() ?? string.Empty,
            Status = root.GetProperty("status").GetString() ?? string.Empty,
            ExternalReference = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null,
            TransactionAmount = root.TryGetProperty("transaction_amount", out var ta) ? ta.GetDecimal() : 0m
        };
    }
}
