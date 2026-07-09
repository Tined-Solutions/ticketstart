using Microsoft.AspNetCore.Http.Extensions;

namespace TicketeraOnline.Api.Helpers;

/// <summary>
/// Redacts sensitive values from strings before they are written to logs or responses.
/// Uses a defensive whitelist-by-key approach for query strings and a conservative
/// pattern-based redaction for free-form messages.
/// </summary>
public static class LogRedactor
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "pwd",
        "pass",
        "token",
        "jwt",
        "refresh_token",
        "refreshtoken",
        "refresh_token",
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
        "cookie"
    };

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
            var value = SensitiveKeys.Contains(pair.Key) ? "[REDACTED]" : pair.Value.ToString();
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
        foreach (var key in SensitiveKeys)
        {
            result = RedactKeyValuePair(result, key);
        }

        return result;
    }

    private static string RedactKeyValuePair(string input, string key)
    {
        // Matches key followed by optional whitespace, then : or =, then a non-whitespace value.
        var pattern = $"(?i)({key})\\s*[:=]\\s*\\S+";
        return System.Text.RegularExpressions.Regex.Replace(input, pattern, "$1=[REDACTED]");
    }
}
