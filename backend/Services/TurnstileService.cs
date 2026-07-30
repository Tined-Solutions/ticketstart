using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

public class TurnstileService : ITurnstileService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TurnstileService> _logger;
    private readonly TurnstileOptions _options;

    public TurnstileService(
        HttpClient httpClient,
        IOptions<TurnstileOptions> options,
        ILogger<TurnstileService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<bool> VerifyTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying Turnstile token");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("secret", _options.SecretKey),
            new KeyValuePair<string, string>("response", token)
        });

        try
        {
            var response = await _httpClient.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                content,
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TurnstileVerifyResponse>(json);

            if (result?.Success == true)
            {
                _logger.LogInformation("Turnstile verification succeeded");
                return true;
            }

            _logger.LogWarning(
                "Turnstile verification failed. Error codes: {ErrorCodes}",
                result?.ErrorCodes != null ? string.Join(", ", result.ErrorCodes) : "none");

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Turnstile verification request failed");
            return false;
        }
    }

    private class TurnstileVerifyResponse
    {
        public bool Success { get; set; }
        public string[]? ErrorCodes { get; set; }
    }
}
