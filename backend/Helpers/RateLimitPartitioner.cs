using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TicketeraOnline.Api.Helpers;

/// <summary>
/// Computes per-client partition keys for the application's rate limiters (JD-C2).
///
/// Previously the limiters were GLOBAL (no PartitionBy), so a single client could exhaust
/// the shared bucket and lock out every other user (self-DoS) while providing almost no
/// per-attacker protection. Partitioning gives each client its own bucket. For endpoints
/// that may be authenticated, requests are keyed by user id (so logged-in users behind a
/// NAT/shared IP don't trip each other's limit); anonymous requests are keyed by the
/// client IP (resolved from X-Forwarded-For by the ForwardedHeaders middleware when a
/// trusted proxy forwards it, otherwise the connection's remote address).
/// </summary>
public static class RateLimitPartitioner
{
    /// <summary>
    /// Preferred for endpoints that may be authenticated (e.g. reservations): key by the
    /// authenticated user id when present, otherwise by the client IP.
    /// </summary>
    public static string AuthenticatedOrIp(HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
