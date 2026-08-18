using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TicketeraOnline.Api.Helpers;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// TDD tests for the JD-C2 fix: rate limiters must be partitioned per client instead of
/// global, so one client cannot exhaust the shared bucket and lock out everyone.
/// <see cref="RateLimitPartitioner.AuthenticatedOrIp"/> keys authenticated requests by
/// user id (avoids NAT/shared-IP collateral between logged-in users) and anonymous ones
/// by the client IP.
/// </summary>
public class RateLimitPartitionerTests
{
    private static DefaultHttpContext ContextWithIp(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    private static DefaultHttpContext ContextWithIpAndUser(string ip, string userId)
    {
        var context = ContextWithIp(ip);
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test"));
        return context;
    }

    [Fact]
    public void AnonymousRequest_KeysByRemoteIp()
    {
        var key = RateLimitPartitioner.AuthenticatedOrIp(ContextWithIp("1.2.3.4"));
        Assert.Equal("ip:1.2.3.4", key);
    }

    [Fact]
    public void AnonymousRequest_NoIp_KeysByUnknown()
    {
        var context = new DefaultHttpContext(); // RemoteIpAddress is null
        var key = RateLimitPartitioner.AuthenticatedOrIp(context);
        Assert.Equal("ip:unknown", key);
    }

    [Fact]
    public void AuthenticatedRequest_KeysByUserId()
    {
        var key = RateLimitPartitioner.AuthenticatedOrIp(ContextWithIpAndUser("9.9.9.9", "user-123"));
        Assert.Equal("user:user-123", key);
    }

    [Fact]
    public void AuthenticatedRequest_IgnoresSharedIp()
    {
        // Two different authenticated users behind the same IP get separate buckets.
        var keyA = RateLimitPartitioner.AuthenticatedOrIp(ContextWithIpAndUser("10.0.0.1", "aaa"));
        var keyB = RateLimitPartitioner.AuthenticatedOrIp(ContextWithIpAndUser("10.0.0.1", "bbb"));
        Assert.Equal("user:aaa", keyA);
        Assert.Equal("user:bbb", keyB);
        Assert.NotEqual(keyA, keyB);
    }
}
