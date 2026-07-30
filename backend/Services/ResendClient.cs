using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Resend API client implementation using HttpClient.
/// </summary>
public class ResendClient : IResendClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResendClient> _logger;

    public ResendClient(HttpClient httpClient, IOptions<ResendOptions> options, ILogger<ResendClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.resend.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
    }

    /// <inheritdoc />
    public async Task<ResendEmailResponse> SendEmailAsync(ResendEmailRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending email to {Recipient} via Resend", request.To);

        var response = await _httpClient.PostAsJsonAsync("emails", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Resend API returned {StatusCode} for {Recipient}. Body: {ErrorBody}",
                (int)response.StatusCode, request.To, errorBody);
            throw new HttpRequestException(
                $"Resend API error {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<ResendEmailResponse>(cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Resend returned an empty response body");
        }

        _logger.LogDebug("Resend accepted email with id {EmailId}", result.Id);
        return result;
    }
}
