# Bugfix Session — 2026-07-19

## Summary
Fixed multiple bugs discovered during end-to-end testing: R2 image upload (SSL handshake failure + streaming signature rejection), email paste prevention on checkout, MercadoPago 403, and broken "Editar" button on organizer/admin dashboards.

---

## Fix 1: R2 image upload (3 layered issues)

**Symptom:** Creating an event with an image showed success but image never appeared in R2 or the app.

**Root causes:**
1. **SSL HandshakeFailure** — ServiceUrl in `appsettings.Development.json` had a typo in the Cloudflare account ID hash (`...453a38ad3914...` instead of correct `...453aad38e3914...`). The wrong endpoint didn't exist, causing TLS handshake rejection.
2. **STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER not implemented (501)** — AWSSDK.S3 v4.0.23.4 defaults to trailer signatures which Cloudflare R2 rejects. Buffering to MemoryStream alone was insufficient.
3. **PublicUrl empty** — R2 bucket r2.dev subdomain was enabled but PublicUrl was not configured, causing `InvalidOperationException`.

**Fix:**
- Corrected ServiceUrl account ID hash in `appsettings.Development.json`
- Set `PublicUrl` to `https://pub-0bfdc78a994c4fdd90c2881d171b070c.r2.dev`
- Buffered stream to `MemoryStream` before `PutObjectAsync` (for known content length)
- Added `DisablePayloadSigning = true` on `PutObjectRequest` to force `UNSIGNED-PAYLOAD` signing

**Commits:** `44a9f1a`, `98d9fc7`
**Files:** `backend/Services/EventService.cs` (lines 290-306)

---

## Fix 2: Email paste prevention on checkout

**Symptom:** Users could paste email addresses in checkout form, risking replicated typos.

**Fix:** Added `onPaste={(e) => e.preventDefault()}` to `purchaserEmail` field in `Checkout.jsx`. The `confirmEmail` field already had this protection.

**Commit:** included in `44a9f1a`
**Files:** `frontend/src/pages/Checkout.jsx`

---

## Fix 3: MercadoPago 403 Forbidden

**Symptom:** Checkout flow failed with 403 when creating payment preferences via MercadoPago API.

**Root cause:** MercadoPago credentials were in `appsettings.json` as placeholders (`YOUR_MERCADO_PAGO_ACCESS_TOKEN`) but `appsettings.Development.json` had no `MercadoPago` section, so the placeholder was inherited.

**Fix:** Added `MercadoPago` section with real `AccessToken` and `WebhookSecret` to `appsettings.Development.json`.

**Note:** Config-only fix — no code changes needed.
**Files:** `backend/appsettings.Development.json` (NOT committed — contains secrets)

---

## Fix 4: "Editar" button broken on organizer/admin dashboards

**Symptom:** Clicking "Editar" on `/organizer/dashboard` or `/admin` navigated to `/organizer/events/:id` but the page never loaded event data.

**Root cause:** `OrganizerEventDetail` called `GET /api/events/{id}/manage` — an endpoint that was never implemented in `EventController`.

**Fix:** Changed to use existing `GET /api/events/{id}` endpoint which returns `EventWithAvailability` — already contains all fields needed by `EventForm` in edit mode (`id`, `name`, `description`, `date`, `location`, `imageUrl`, `ticketTypes`).

**Commit:** `c3b8b1a`
**Files:** `frontend/src/pages/OrganizerEventDetail.jsx` (line 24)

---

## Environment notes

- **Martin:** WSL2 (Linux), `appsettings.Development.json` holds real credentials for local dev
- **Edgar:** Windows, pulls from `origin/dev`
- `appsettings.Development.json` is NOT in git (contains secrets: DB, R2, JWT keys)
