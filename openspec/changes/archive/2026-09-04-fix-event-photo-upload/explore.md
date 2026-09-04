# Exploration — Event photo upload to Cloudflare R2 still broken in production

Change: `fix-event-photo-upload`
Branch: `fix/r2-upload-linux-tls` (fast-forwarded to `6f4fe27`)
Date: 2026-09-04
Mode: read-only investigation + live endpoint probe

## Executive summary

The backend R2 client **works against the real Cloudflare R2 endpoint** — verified with a live probe
using the production `R2StorageClient.cs` code verbatim (PUT 200, public GET 200, DELETE 200) and with
default TLS (1.2+1.3). The six commits of TLS/SDK "fixes" were chasing a symptom that the current code
does not exhibit: the SigV4 signature, path-style URI, UNSIGNED-PAYLOAD, host header, and TLS 1.2-only
forcing are all correct. The test suite (733 pass / 4 fail) never exercises the real transport — every
test mocks `IR2StorageClient`. The remaining suspects are **deployment-side**: (1) CloudflareR2 env vars
missing/misnamed in the Render dashboard (no fail-fast, placeholders silently used), (2) the last user
test predating the current commits (deploy lag — branch was just fast-forwarded), (3) the Cloudflare
Pages proxy path (untested), and (4) the frontend swallowing the upload error so the user never sees the
real cause.

## Current State

### Backend flow

`POST /api/events/{id}/image` (EventController.UploadEventImage, `[Authorize(Policy = "EventOwnership")]`)
→ `EventService.ReplaceEventImageAsync` → validates ownership + `EventFinalizedGuard.EnsureMutable` →
`UploadEventImageAsync` (MIME whitelist `image/jpeg|png|webp`, max 5 MB, buffered to MemoryStream) →
`IR2StorageClient.PutObjectAsync` (`R2StorageClient`, raw SigV4 over plain HttpClient) → stores
`ImageUrl = {PublicUrl}/events/{guid}.{ext}` on the event → best-effort delete of the previous object.

`R2StorageClient` (backend/Services/R2StorageClient.cs):
- Region `auto`, service `s3`, `UNSIGNED-PAYLOAD`.
- Canonical request built correctly (method, encoded path-style URI, empty query, sorted headers each
  terminated with `\n`, signed headers, UNSIGNED-PAYLOAD). String-to-sign, HMAC key derivation, and
  Authorization header all match AWS SigV4.
- `SocketsHttpHandler` with `EnabledSslProtocols = Tls12` only (commit `6f4fe27`).
- Registered `AddSingleton` in Program.cs; **no fail-fast** for the `CloudflareR2` section (constructor
  only throws when a key is entirely absent; appsettings.json supplies non-empty placeholders).

### Frontend flow

EventForm.jsx: create/update event via JSON, then `apiClient.post(`/events/${eventId}/image`, formData,
{ headers: { 'Content-Type': 'multipart/form-data' } })`. Field name `image` matches the controller
parameter. axios 1.18.x strips the manual Content-Type for FormData in standard browsers
(`resolveConfig` → `headers.setContentType(undefined)`), so the browser adds the boundary — **this is
not the bug**. The axios interceptor adds `X-CSRF-PROTECT: 1` to all mutations and `withCredentials`
sends the cookie. **The upload failure is swallowed**: the `catch {}` block sets a success message
("...pero la imagen no pudo cargarse") — the event persists without an image and no error surfaces.

### Production topology

Browser → Cloudflare Pages (SPA + `frontend/functions/api/[[path]].js` proxy) → Render
(`ticketstart.onrender.com`) → R2. The Pages Function forwards method/headers/body stream and rewrites
Host. Dev uses the Vite proxy — a different code path. **No render.yaml / Procfile / workflow exists in
the repo; Render env vars live only in the Render dashboard and are unverifiable from the repo.**

### Tests

- `EventImageUploadTests.cs` + `ImageStoragePropertyTests.cs`: mock `IR2StorageClient`; cover only
  EventService orchestration (validation, URL shape, key pattern, delete-on-replace). **Zero coverage of
  SigV4/TLS/HTTP.** No live-endpoint test exists. Stale AWS-SDK-era config keys
  (`CloudflareR2:AccessKeyId`/`SecretAccessKey`/`AccountId`) remain in ImageStoragePropertyTests;
  `AWSSDK.S3` is still referenced in the csproj (unused).
- Full suite on this branch: **733 passed / 4 failed**, all unrelated to R2:
  - `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized` — invalid webhook signature
    returned 200 (security-relevant; unrelated).
  - `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized` — same root cause.
  - `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` — webhook exemption failing.
  - `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted` — timing/FakeTimeProvider.

### Configuration

- `backend/appsettings.json` holds non-empty placeholders (`YOUR_R2_*`, `https://YOUR_ACCOUNT_ID.r2.cloudflarestorage.com`,
  `https://YOUR_PUBLIC_DOMAIN`). In Production these are used verbatim when Render env vars are
  missing → upload fails with 403/404/500 at request time while the app still boots.
- `backend/appsettings.Development.json` is **committed to git** with real secrets (R2 keys, Supabase
  password, Mercado Pago access token, JWT/QR/HMAC keys) — the 2026-07-19 proposal claims it is
  gitignored; it is not. Security finding.
- Config keys are consistent across the SDK era (`8dc78a3`) and the raw client
  (`CloudflareR2:AccessKey|SecretKey|ServiceUrl`) — no schema rename. The
  `AccessKeyId/SecretAccessKey/AccountId` keys in tests are stale leftovers.

## Live probe evidence (decisive)

A throwaway console probe (temp dir, not the repo) compiled the **real `R2StorageClient.cs`** verbatim
and used the dev credentials from `appsettings.Development.json` against the real endpoint:

| Probe | Result |
|---|---|
| Real R2StorageClient (Tls12-only) PUT `explore-probe-{guid}.txt` | **200 OK** |
| Public GET `https://pub-0bfdc78a994c4fdd90c2881d171b070c.r2.dev/...` | **200**, body matches |
| DELETE via real client | **OK** |
| Same SigV4 logic with DEFAULT TLS (1.3 allowed) PUT/DELETE | **200/204 OK** — TLS 1.3 handshake works |
| ListObjectsV2 (read-only) | **200 — bucket contains 2 objects**: `events/24a894d9-….jpg`, `events/d93e417d-….webp` |

Conclusions:
- SigV4, path-style, UNSIGNED-PAYLOAD, host header, Tls12-only handler: all correct against real R2.
- The "Cloudflare rejects TLS 1.3 from Render IPs" theory is not corroborated; the original handshake
  failure (2026-07-19) was traced to a **wrong ServiceUrl (account ID typo)** — a config error, not TLS.
- Uploads have succeeded at some point with these credentials; the public URL domain serves objects.

## Affected Areas

- `backend/Services/R2StorageClient.cs` — verified correct; do not change signing/TLS without a live test.
- `backend/Program.cs` — R2 config has no fail-fast; consider validating the 5 `CloudflareR2` keys at startup.
- `backend/Controllers/EventController.cs` — `UploadEventImage` returns generic 500; error details only in logs.
- `backend/Services/EventService.cs` — `UploadEventImageAsync`/`ReplaceEventImageAsync` orchestration correct.
- `frontend/src/components/EventForm.jsx` — swallows upload errors (lines 170-179); user never sees the cause.
- `frontend/functions/api/[[path]].js` — Pages proxy is the only untested hop in the prod path.
- `backend/Tests/EventImageUploadTests.cs`, `ImageStoragePropertyTests.cs` — mock-only; no live-path coverage.
- `backend/appsettings.Development.json` — committed secrets; must be removed from git and rotated.

## Approaches

1. **Verify deployment before writing code (recommended first step, zero-code)**
   - Confirm which commit is deployed on Render and when the last deploy happened (deploy lag — branch was
     fast-forwarded to `6f4fe27` on 2026-09-03 23:05 -0300).
   - Read the Render dashboard env vars: all 5 keys (`AccessKey`, `SecretKey`, `ServiceUrl`, `BucketName`,
     `PublicUrl`) must use `CloudflareR2__*` double-underscore naming.
   - Reproduce an upload against the live Pages domain with the browser network tab + backend logs.
   - Pros: zero risk, isolates config vs code; Cons: needs Render dashboard access (user action);
     Effort: Low.

2. **Small hardening change (the actual proposal)**
   - Add fail-fast validation of `CloudflareR2` keys at startup (mirrors Brevo/JWT/HideExpiredEvents).
   - Stop swallowing the upload error in EventForm; surface the real message (and log server-side).
   - Add a live-endpoint integration test for `R2StorageClient` (opt-in/skippable, not mocked).
   - Remove `AWSSDK.S3` from the csproj; purge committed secrets from git history + rotate keys.
   - Pros: addresses every remaining root cause class; Cons: needs a redeploy + Render env fix to actually
     take effect; Effort: Low-Medium.

3. **Live-probe the Pages proxy path**
   - Replay the exact multipart request against the production Pages domain with valid credentials and
     compare with the Vite-proxy path.
   - Pros: tests the one hop the local probe cannot; Cons: needs a real auth cookie; Effort: Medium.

## Recommendation

Do **Approach 1 first** (deploy state + Render env vars + one live repro), because the current backend
code is proven to work against real R2 and the most probable causes are now deployment-side. Then ship
**Approach 2** as the change: fail-fast config, surfaced upload errors, a live integration test, and
secret/cleanup hygiene. Add Approach 3 only if the live repro still fails after the config is confirmed.

## Risks

- If the running deployment still predates `e7b49e0`/`6f4fe27`, the user's "still broken" report does not
  test the current code — redeploying may resolve it without any code change.
- Render env vars are invisible from the repo; the exploration cannot confirm or deny a `CloudflareR2__*`
  misconfiguration.
- Committed secrets in `appsettings.Development.json` (R2 keys, DB password, MP token) are exposed in the
  repo history; they should be rotated and the file gitignored.
- 4 unrelated test failures exist on this branch (MP webhook invalid-signature accepted with 200 is
  security-relevant) — they should be fixed before this branch is promoted.
- Forcing TLS 1.2-only is harmless for R2 but was based on an unproven theory; keep it only if a live
  Linux/Render test shows it is actually needed.

## Ready for Proposal

Yes — with the condition that the orchestrator/user first confirms (a) the commit actually deployed on
Render and its timestamp, and (b) the CloudflareR2 env vars in the Render dashboard. The proposal should
cover Approach 2 (fail-fast + error surfacing + live integration test + hygiene) and optionally a
proxy-path probe.

## Appendix — probe script location

`%TEMP%/opencode/r2probe` (throwaway console project, compiles the real `R2StorageClient.cs`). Not part
of the repo. Probe objects were deleted after the run; two pre-existing objects remain in the bucket.