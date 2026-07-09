using Microsoft.AspNetCore.Http.Extensions;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TicketeraOnline.Api.Helpers;

/// <summary>
/// Redacts sensitive values from strings before they are written to logs or responses.
/// Uses a defensive key-based denylist for query strings and a conservative
/// pattern-based redaction for free-form messages.
/// </summary>
public static class LogRedactor
{
    private static readonly HashSet<string> _sensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "pwd",
        "pass",
        "token",
        "jwt",
        "refresh_token",
        "refreshtoken",
        "refresh-token",
        "api_key",
        "apikey",
        "secret",
        "secret_key",
        "secretkey",
        "private_key",
        "privatekey",
        "card_number",
        "cardnumber",
        "card",
        "cvv",
        "cvc",
        "authorization",
        "cookie",
        "signature",
        "x-signature",
        "x_signature",
        "bearer",
        "pan",
        "cardholder",
        "external_reference",
        "qr_code_data",
        "qrdata",
        "email",
        "dni",
        "phone",
        "document",
        "documentnumber",
        "document_number"
    };

    /// <summary>
    /// Exposes the sensitive-key denylist so tests and generators can enumerate it.
    /// </summary>
    public static IReadOnlyCollection<string> SensitiveKeys => _sensitiveKeys;

    /// <summary>
    /// Redacts sensitive values from a query string while preserving other parameters.
    /// </summary>
    public static string RedactQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            return string.Empty;

        if (!queryString.StartsWith('?'))
            queryString = "?" + queryString;

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString);
        var redacted = new Dictionary<string, string?>();

        foreach (var pair in query)
        {
            var value = _sensitiveKeys.Contains(pair.Key) ? "[REDACTED]" : pair.Value.ToString();
            redacted[pair.Key] = value;
        }

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(string.Empty, redacted);
    }

    /// <summary>
    /// Redacts likely sensitive tokens from a free-form message.
    /// </summary>
    public static string RedactMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var result = message;
        foreach (var key in _sensitiveKeys)
        {
            result = RedactKeyValuePair(result, key);
        }

        // Conservative regex failover for tokens that may not follow key=value patterns.
        result = RedactBearerTokens(result);
        result = RedactJwtPrefixes(result);
        result = RedactLongSecretLikeStrings(result);

        return result;
    }

    /// <summary>
    /// Hashes a government-issued identifier so the audit trail keeps correlation
    /// ability without exposing the raw value.
    /// </summary>
    public static string HashIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..12];
    }

    private static string RedactKeyValuePair(string input, string key)
    {
        // Matches key followed by optional whitespace, then : or =, then a non-whitespace value.
        var pattern = $"(?i)({Regex.Escape(key)})\\s*[:=]\\s*\\S+";
        return Regex.Replace(input, pattern, "$1=[REDACTED]");
    }

    private static string RedactBearerTokens(string input)
    {
        return Regex.Replace(input, @"(?i)bearer\s+\S+", "Bearer [REDACTED]");
    }

    private static string RedactJwtPrefixes(string input)
    {
        // eyJ is the Base64 encoding of a JSON object start, common to JWTs.
        return Regex.Replace(input, @"eyJ[a-zA-Z0-9_/+-]*=*", "[REDACTED]");
    }

    private static string RedactLongSecretLikeStrings(string input)
    {
        // Conservative match: hex or base64-like strings longer than 32 chars that look like secrets.
        // Avoids matching GUIDs (which contain dashes) and short tokens.
        return Regex.Replace(input, @"\b[a-f0-9]{32,}\b|\b[A-Za-z0-9+/]{33,}={0,2}\b", "[REDACTED]");
    }
}
