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
}
