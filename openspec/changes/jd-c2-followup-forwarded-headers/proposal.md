# Proposal: JD-C2 Follow-up — ForwardedHeaders Middleware Never Wired Into the Pipeline

## Summary

Review follow-up on `de45429` ("fix(backend): particionar rate limiters por cliente para evitar self-DoS (cierra JD-C2)"). The partitioning change is structurally correct but **does not take effect behind a proxy**: the `ForwardedHeaders` middleware that consumes `X-Forwarded-For` is configured but **never added to the request pipeline**, so `RemoteIpAddress` is never rewritten to the real client IP. As a result, the self-DoS the fix claims to close remains open for the anonymous endpoints in the target deployment (proxy/ngrok).

## Evidence

- `backend/Program.cs` line 202: `builder.Services.Configure<ForwardedHeadersOptions>(...)` sets `ForwardedHeaders.XForwardedFor | XForwardedProto`, and comments state the middleware "rewrite[s] RemoteIpAddress to the real client IP".
- `grep -rn "UseForwardedHeaders" backend` → **zero matches** outside comments. Configuring the options does NOT activate the middleware in ASP.NET Core; `app.UseForwardedHeaders()` must be called in the pipeline.
- `backend/Program.cs` pipeline order (lines ~300-330): `UseRateLimiter()` (line 321), then `UseAuthentication()`/`UseAuthorization()`. No `UseForwardedHeaders` anywhere before them.
- Rate limiter partition keys:
  - `Resend` and `Login` policies key on `context.Connection.RemoteIpAddress` (Program.cs lines 227, 238).
  - `Reservations` keys via `RateLimitPartitioner.AuthenticatedOrIp` (user id when authenticated, else `RemoteIpAddress`).
- `backend/Tests/RateLimitPartitionerTests.cs` sets `RemoteIpAddress` manually on a `DefaultHttpContext`, so unit tests pass without exercising the middleware wiring — the integration gap is invisible to the current suite.

## Impact

Behind any reverse proxy (including ngrok in dev, which the code itself documents), every client appears to the app as the proxy's IP:

- **`Resend` (3/hour) and `Login` (10/min)**: all clients collapse into ONE shared bucket → the original self-DoS (one client exhausting the shared bucket and locking out everyone) is NOT closed for these endpoints.
- **`Reservations`**: authenticated users partition by user id (works), but anonymous guests share the proxy-IP bucket → guests can lock each other out of reservation creation.
- **`X-Forwarded-Proto` is equally inert** without the middleware: `UseHttpsRedirection` (production only) can still loop when TLS is terminated at the proxy.

## Proposed Fix (when scheduled)

1. Add `app.UseForwardedHeaders();` as the **first** middleware in the pipeline (before `UseHttpsRedirection` and before `UseRateLimiter`) so both proto and IP rewriting happen before any consumer reads them.
2. Harden `ForwardedHeadersOptions` for production: keep `KnownProxies`/`KnownNetworks` explicit (trusted proxies only), never the dev "trust all" path, to avoid IP spoofing of the partition keys and audit logs.
3. Add an integration-style test that asserts `RemoteIpAddress` is rewritten from `X-Forwarded-For` through the real pipeline (WebApplicationFactory), not just a partitioner unit test.

## Success Criteria

- [ ] `app.UseForwardedHeaders()` present before `UseRateLimiter` in `backend/Program.cs`.
- [ ] Production `ForwardedHeadersOptions` restrict trust to explicit known proxies/networks.
- [ ] Integration test proves `X-Forwarded-For` reaches the partition key.
- [ ] `dotnet test` green.

## References

- Commit: `de45429` (JD-C2) — introduced `RateLimitPartitioner` and per-client policies.
- Files: `backend/Program.cs`, `backend/Helpers/RateLimitPartitioner.cs`, `backend/Tests/RateLimitPartitionerTests.cs`.