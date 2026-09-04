using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Minimal Cloudflare R2 storage client using raw HTTP + AWS Signature V4.
///
/// The AWS SDK cannot negotiate TLS with R2 from Linux containers (Render):
/// "sslv3 alert handshake failure" via its HttpWebRequest transport, and the
/// SDK's HttpClientFactory override does not change that. HttpClient itself
/// works fine from the container (Turnstile/Brevo prove it), so this client
/// signs SigV4 requests by hand and sends them over a plain HttpClient.
/// </summary>
public interface IR2StorageClient
{
    /// <summary>Uploads an object; throws InvalidOperationException on failure.</summary>
    Task PutObjectAsync(string bucketName, string key, Stream input, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Deletes an object; throws InvalidOperationException on failure.</summary>
    Task DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default);
}

public class R2StorageClient : IR2StorageClient
{
    private const string Region = "auto";
    private const string Service = "s3";
    private const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    private readonly HttpClient _httpClient;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _serviceUrl;

    public R2StorageClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _accessKey = configuration["CloudflareR2:AccessKey"]
            ?? throw new InvalidOperationException("CloudflareR2:AccessKey is not configured");
        _secretKey = configuration["CloudflareR2:SecretKey"]
            ?? throw new InvalidOperationException("CloudflareR2:SecretKey is not configured");
        _serviceUrl = (configuration["CloudflareR2:ServiceUrl"]
            ?? throw new InvalidOperationException("CloudflareR2:ServiceUrl is not configured")).TrimEnd('/');
    }

    public async Task PutObjectAsync(string bucketName, string key, Stream input, string contentType, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, bucketName, key, input, contentType, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to upload to R2 ({(int)response.StatusCode}): {body}");
        }
    }

    public async Task DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Delete, bucketName, key, null, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to delete from R2 ({(int)response.StatusCode}): {body}");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string bucketName, string key, Stream? body, string? contentType, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var host = new Uri(_serviceUrl).Host;
        var canonicalUri = BuildCanonicalUri(bucketName, key);

        var headers = new List<string>();
        if (!string.IsNullOrEmpty(contentType))
        {
            headers.Add($"content-type:{contentType}");
        }
        headers.Add($"host:{host}");
        headers.Add($"x-amz-content-sha256:{UnsignedPayload}");
        headers.Add($"x-amz-date:{amzDate}");
        headers.Sort(StringComparer.Ordinal);
        var signedHeaders = string.Join(";", headers.Select(h => h[..h.IndexOf(':')]));

        var canonicalRequest = $"{method.Method}\n{canonicalUri}\n\n{string.Join("\n", headers)}\n\n{signedHeaders}\n{UnsignedPayload}";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{dateStamp}/{Region}/{Service}/aws4_request\n{Hex(Sha256(canonicalRequest))}";
        var signingKey = GetSigningKey(_secretKey, dateStamp, Region, Service);
        var signature = Hex(Hmac(signingKey, stringToSign));
        var authorization =
            $"AWS4-HMAC-SHA256 Credential={_accessKey}/{dateStamp}/{Region}/{Service}/aws4_request, " +
            $"SignedHeaders={signedHeaders}, Signature={signature}";

        using var request = new HttpRequestMessage(method, $"{_serviceUrl}{canonicalUri}");
        if (body != null)
        {
            request.Content = new StreamContent(body);
            if (!string.IsNullOrEmpty(contentType))
            {
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
        }
        request.Headers.Host = host;
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", UnsignedPayload);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Path-style canonical URI with every path segment URL-encoded, keeping
    /// the "/" separators raw (SigV4 requirement).
    /// </summary>
    private static string BuildCanonicalUri(string bucketName, string key)
    {
        var segments = new[] { bucketName }.Concat(key.Split('/')).Select(Uri.EscapeDataString);
        return "/" + string.Join("/", segments);
    }

    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static byte[] Hmac(byte[] key, string value)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static byte[] GetSigningKey(string secret, string dateStamp, string region, string service)
    {
        var kDate = Hmac(Encoding.UTF8.GetBytes($"AWS4{secret}"), dateStamp);
        var kRegion = Hmac(kDate, region);
        var kService = Hmac(kRegion, service);
        return Hmac(kService, "aws4_request");
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}