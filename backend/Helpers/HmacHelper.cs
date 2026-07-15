using System.Security.Cryptography;
using System.Text;

namespace TicketeraOnline.Api.Helpers;

/// <summary>
/// Shared HMAC-SHA256 helper used for QR code signing, webhook signature validation,
/// and reservation token generation.
/// </summary>
public static class HmacHelper
{
    /// <summary>
    /// Computes an HMAC-SHA256 hex-encoded (lowercase) signature for the given data.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <param name="key">Secret key</param>
    /// <returns>Lowercase hex HMAC-SHA256 signature</returns>
    /// <exception cref="ArgumentException">Thrown when the key is null or empty</exception>
    public static string ComputeHmacSha256(string data, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("HMAC key cannot be null or empty", nameof(key));
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Validates an HMAC-SHA256 signature using constant-time comparison.
    /// </summary>
    /// <param name="data">Data that was signed</param>
    /// <param name="key">Secret key</param>
    /// <param name="signature">Signature to validate</param>
    /// <returns>True if the signature is valid, false otherwise</returns>
    public static bool ValidateHmacSha256(string data, string key, string signature)
    {
        if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var expected = ComputeHmacSha256(data, key);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    /// <summary>
    /// Validates an HMAC-SHA256 signature from raw bytes using constant-time comparison.
    /// Use this overload when the payload was received as raw bytes (e.g., webhook body)
    /// to avoid encoding mismatches between the sender and receiver.
    /// </summary>
    /// <param name="data">Raw data bytes that were signed</param>
    /// <param name="key">Secret key</param>
    /// <param name="signature">Signature to validate</param>
    /// <returns>True if the signature is valid, false otherwise</returns>
    public static bool ValidateHmacSha256(byte[] data, string key, string signature)
    {
        if (data == null || data.Length == 0 || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(signature))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(data);
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    /// <summary>
    /// Extracts the Unix timestamp from a QR code payload.
    /// Format: {ticketId}:{timestamp}:{signature}
    /// </summary>
    /// <param name="qrPayload">The QR code payload string</param>
    /// <returns>The Unix timestamp (seconds)</returns>
    /// <exception cref="FormatException">Thrown when the payload format is invalid</exception>
    public static long ExtractTimestamp(string qrPayload)
    {
        if (string.IsNullOrWhiteSpace(qrPayload))
        {
            throw new FormatException("QR payload is null or empty");
        }

        var parts = qrPayload.Split(':');
        if (parts.Length < 2)
        {
            throw new FormatException($"Invalid QR payload format: expected at least 2 colon-separated parts, got {parts.Length}");
        }

        if (!long.TryParse(parts[1], out var timestamp))
        {
            throw new FormatException($"Invalid timestamp format in QR payload: '{parts[1]}'");
        }

        return timestamp;
    }
}
