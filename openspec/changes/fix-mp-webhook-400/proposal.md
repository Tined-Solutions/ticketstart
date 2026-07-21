# Proposal: Fix Mercado Pago Webhook HTTP 400 + Email Resilience

**Change name:** `fix-mp-webhook-400`
**Intent:** Restore end-to-end ticket delivery: MP webhooks must ACK with HTTP 200 using MP's real envelope, payment status must be fetched from the Payments API, and failed confirmation emails must be logged + queued for retry instead of being swallowed.

---

## Problem Statement

Buyers pay successfully in Mercado Pago but never receive their ticket email. The MP panel shows HTTP 400 for every webhook call, so MP keeps retrying useless notifications and no ticket is created from the webhook path. The frontend `/api/payments/confirm` path can create tickets, but its email send silently swallows failures (`PaymentService.cs:268-278`), so even that path is unreliable.

Evidence (from exploration artifact #353):

- `WebhookPayload` (`backend/Services/IPaymentService.cs:61-66`) declares `PaymentId`, `ExternalReference`, `Status` as **top-level** JSON properties.
- MP's real notification body is `{ action, api_version, data: { id }, date_created, id, live_mode, type, user_id }`. There is **no** top-level `PaymentId`/`Status`/`ExternalReference`.
- `System.Text.Json` deserializes the mismatched body silently — no exception — leaving `PaymentId = ""`.
- `PaymentController.Webhook` (`backend/Controllers/PaymentController.cs:111`) deserializes into the wrong model, then at line 120 `string.IsNullOrEmpty(payload.PaymentId)` is true → returns `BadRequest(...)` → HTTP 400 (line 123).
- CSRF middleware is **not** the cause: `CsrfHeaderMiddleware.cs:30-36` already exempts `/api/payments/webhook`.
- Email send is wrapped in try/catch (`PaymentService.cs:268-278`): failure is only logged, never retried, never surfaced. The success path still returns 200.
- Resend `FromEmail` is `tickets@resend.dev` (Resend's shared sandbox domain) — deliverability risk / likely SPAM.

## Root Cause

1. **Primary — Webhook DTO mismatch.** `WebhookPayload` models a payload shape MP never sends. The controller then hard-fails on the empty `PaymentId` it created. MP does **not** include `status` or `external_reference` in the webhook body — those must be fetched via `GET /v1/payments/{id}`, which is currently missing from `MercadoPagoClient`.
2. **Secondary — Swallowed email failures.** `PaymentService.cs:268-278` swallows the `SendTicketEmailAsync` exception. No retry record, no observability, no manual re-send path.
3. **Secondary — Resend test domain.** `tickets@resend.dev` is Resend's sandbox sender. Production traffic from sandbox senders is routinely demoted to SPAM by inbox providers.

## Non-Goals

- No new anti-fraud, payment routing, or multi-provider support.
- No refactor of `ConfirmPaymentAsync`'s approval path beyond reusing `ProcessApprovedPaymentAsync`.
- No frontend test harness (no runner configured — out of scope per `openspec/config.yaml`).
- No migration of `WebhookPayload`'s old shape for third-party callers (MP is the only producer).
- No email templating redesign.
- No idempotency overhaul — the existing check at `PaymentService.cs:224-232` is reused as-is; only **verified** for the new flow.

---

## Proposed Approach

Seven work items, layered so each is independently reviewable.

### Work Item 1 — New MP webhook envelope DTO

Replace `WebhookPayload` (`IPaymentService.cs:61-66`) with the real MP envelope. Keep the type name `WebhookPayload` to minimize churn, or introduce a new `MercadoPagoWebhookEnvelope` and alias.

```csharp
public class WebhookPayload
{
    public string? Action { get; set; }       // "payment.updated"
    public string? ApiVersion { get; set; }   // "v1"
    public string? Type { get; set; }         // "payment"
    public long? Id { get; set; }             // top-level id (numeric)
    public WebhookData? Data { get; set; }
    public DateTime? DateCreated { get; set; }
    public bool? LiveMode { get; set; }
}

public class WebhookData
{
    public string? Id { get; set; }           // THE payment id (string)
}
```

Notes:
- `System.Text.Json` defaults: snake_case → use `[JsonPropertyName("api_version")]` etc., or configure `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` on the deserialize call in the controller.
- `data.id` is the canonical payment id per MP docs. `Id` (top-level) echoed it historically but `data.id` is the contract.

### Work Item 2 — `IMercadoPagoClient.GetPaymentByIdAsync`

Add to `IMercadoPagoClient` and implement in `MercadoPagoClient.cs`:

```csharp
Task<MercadoPagoPaymentDetail?> GetPaymentByIdAsync(string paymentId, CancellationToken ct = default);
```

`MercadoPagoPaymentDetail` extends `MercadoPagoPaymentInfo` with `ExternalReference` and `TransactionAmount` (returned by `GET /v1/payments/{id}`):

```csharp
public class MercadoPagoPaymentDetail
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public decimal TransactionAmount { get; set; }
}
```

Implementation: `_httpClient.GetAsync($"v1/payments/{Uri.EscapeDataString(paymentId)}", ct)`, parse `id` / `status` / `external_reference` / `transaction_amount`. Return `null` on 404 (as `GetPreferenceAsync` does on line 121). Log + throw on other non-2xx (preserve existing pattern).

### Work Item 3 — Refactor `ProcessWebhookAsync`

Rewrite `PaymentService.ProcessWebhookAsync` (`PaymentService.cs:143-219`) to:

1. Read `payload.Data?.Id` (string). If absent, fall back to `payload.Id?.ToString()`. If still empty → return `WebhookResult { Success = false, FailureType = Processing, Error = "Missing payment id" }`.
2. Signature validation unchanged (lines 149-180) — operates on `rawBody` regardless of payload shape.
3. Call `await _mercadoPagoClient.GetPaymentByIdAsync(paymentId)` → fetch `status` + `external_reference`.
4. If the fetch returns `null` (404) → return `Processing` failure. MP will retry.
5. Parse `external_reference` into `reservationId` (replaces lines 182-192). On invalid → `Processing` failure.
6. Reuse `reservation` lookup (194-209), `amount` calc (211), and the `status == "approved"` branch (213-218) — but route through the fetched `paymentDetail.Status` instead of `payload.Status`.
7. **Idempotency verification:** confirm `ProcessApprovedPaymentAsync` (line 221-232) still guards on `MercadoPagoId == paymentId`. The new flow passes `paymentDetail.Id` (string from API), which equals `data.id` — so a duplicate webhook for the same payment hits the existing guard and returns 200 idempotently. Race with the frontend `ConfirmPaymentAsync` path is also covered: `ConfirmPaymentAsync` (line 407-413) and `ProcessApprovedPaymentAsync` (line 224-232) both check `Transactions.MercadoPagoId`. Both paths converge on the same id, so whichever wins, the other returns 200 idempotently. **No new idempotency code needed.**

### Work Item 4 — Controller ACK semantics

In `PaymentController.Webhook` (`PaymentController.cs:94-159`):

- Replace the BadRequest-on-empty-`PaymentId` block (lines 120-124) with: validate `payload.Data?.Id` presence (or `payload.Id`). If absent → log warning + return `Ok(new { status = "ignored" })` so MP does **not** retry malformed envelopes we can't act on. (Returning 400 makes MP retry forever; returning 200 tells MP to stop. We log so we can debug.)
- Keep the `BadRequest` only for malformed JSON (deserialization exception, line 114-118) — MP should not retry truly undecodable bodies.
- Successful processing still returns `Ok(new { paymentId = result.PaymentId })`. Authentication failures still 401. The catch-all 500 (line 153-158) stays as a safety net.
- Update the audit log `Details` string (line 135) to use `result.PaymentId` and the fetched status, since `payload.Status` no longer exists.

### Work Item 5 — Email resilience: log + queue pending retry

Replace the silent swallow at `PaymentService.cs:268-278`. After a successful DB commit, email send MUST:

1. Attempt `SendTicketEmailAsync`.
2. On success → no-op.
3. On failure →
   - `_logger.LogError(ex, ...)` with reservation + payment + email.
   - Insert a row into a new `pending_email_send` table (Supabase migration). Do **NOT** throw; the webhook still returns 200 so MP stops retrying.

Proposed `pending_email_send` schema (new table; no existing table fits — verified via Supabase):

| column | type | notes |
|---|---|---|
| `id` | uuid PK | default `gen_random_uuid()` |
| `reservation_id` | uuid NOT NULL | FK → `Reservations.Id` |
| `payment_id` | text NOT NULL | Mercado Pago payment id (string) |
| `recipient_email` | text NOT NULL | the email we tried to send to |
| `ticket_ids` | uuid[] NOT NULL | array of ticket ids to resend |
| `last_error` | text | truncated exception message |
| `attempts` | integer NOT NULL DEFAULT 0 | retry counter |
| `max_attempts` | integer NOT NULL DEFAULT 5 | stop after this many |
| `status` | text NOT NULL DEFAULT 'pending' | `pending` / `sent` / `exhausted` |
| `last_attempt_at` | timestamptz | nullable |
| `created_at` | timestamptz NOT NULL DEFAULT now() | |
| `updated_at` | timestamptz NOT NULL DEFAULT now() | trigger-maintained |

RLS: locked to the service role (backend uses service key) — no client-facing access.

Add an EF Core entity `PendingEmailSend` + DbSet on `ApplicationDbContext`, plus the migration (DDL via `supabase apply_migration` named `create_pending_email_send`).

The `try/catch` becomes:

```csharp
try { await _emailService.SendTicketEmailAsync(email, tickets, eventEntity); }
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to send ticket email for reservation {ReservationId}. Queued for retry.", reservation.Id);
    await QueueEmailRetryAsync(reservation.Id, paymentId, email, tickets.Select(t => t.Id).ToArray(), ex.Message);
}
```

`QueueEmailRetryAsync` is a new private method that inserts the row in its own EF transaction (separate from the committed payment tx — failure to queue must not affect the committed payment).

### Work Item 6 — Retry mechanism

Simplest sound design: a new `POST /api/payments/emails/retry-pending` admin endpoint (NOT `[AllowAnonymous]`; gated by an existing admin auth policy) that:

1. Queries `pending_email_send` rows with `status = 'pending'` and `attempts < max_attempts`, ordered by `created_at`, limited to a configurable batch (default 50).
2. For each row: load tickets by `ticket_ids`, load the reservation + event, call `SendTicketEmailAsync`.
3. On success → set `status='sent'`. On failure → `attempts += 1`, `last_error = ...`, `last_attempt_at = now()`. If `attempts >= max_attempts` → `status='exhausted'`.
4. Returns a summary `{ attempted, sent, failed, exhausted }`.

No background `IHostedService` for now — a manual admin-triggered endpoint is the simplest sound design and is enough for the current scale. A future iteration can wrap a `BackgroundService` with a timer around the same query. The endpoint name keeps that option open without committing to it.

### Work Item 7 — Resend domain deliverability

Documented decision in the proposal (no code in this change beyond what's needed for a config knob):

- Add `Resend:FromEmail` and `Resend:FromName` to `appsettings.json` / `appsettings.Development.json` (currently hardcoded or sourced elsewhere — verify during apply). Default `FromEmail` stays `tickets@resend.dev` in dev; **production must** set a custom verified domain (e.g. `tickets@ticketera.com` or a verified subdomain) before this change ships to prod.
- Verify the custom domain in the Resend dashboard (out-of-band; documented as a runbook step, not code).
- Recommendation: do NOT ship to production with `tickets@resend.dev`. The config knob lets us switch without a redeploy once the domain is verified.

---

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Services/IPaymentService.cs` | Modified | Replace `WebhookPayload` with MP envelope DTO + `WebhookData` class. |
| `backend/Services/IMercadoPagoClient.cs` | Modified | Add `GetPaymentByIdAsync` + `MercadoPagoPaymentDetail`. |
| `backend/Services/MercadoPagoClient.cs` | Modified | Implement `GET /v1/payments/{id}`. |
| `backend/Services/PaymentService.cs` | Modified | Rewrite `ProcessWebhookAsync` (143-219); replace silent email swallow (268-278) with log + queue; add `QueueEmailRetryAsync`. |
| `backend/Controllers/PaymentController.cs` | Modified | Drop the `PaymentId`-empty BadRequest on new envelope; keep OK-on-malformed-envelope semantics; update audit `Details`. Add `POST /api/payments/emails/retry-pending` endpoint (or a new `EmailRetryController`). |
| `backend/Data/ApplicationDbContext.cs` | Modified | Add `PendingEmailSend` DbSet + entity. |
| `backend/Models/PendingEmailSend.cs` | New | EF entity mapping to `pending_email_send`. |
| `supabase/migrations/` (or `apply_migration`) | New | DDL to create `pending_email_send` table with RLS. |
| `backend/appsettings*.json` | Modified | Add `Resend:FromEmail` / `Resend:FromName` knobs. |

## Risks & Tradeoffs

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| MP `api_version` change shifts `data.id` location again | Low | Pin acceptance on `api_version == "v1"` (log + reject others); MP versioning is documented and slow-moving. Add a regression test capturing the v1 envelope shape. |
| ngrok URL churn in dev invalidates `NotificationUrl` between sessions | Med | Already a known limitation — `WebhookBaseUrl` is in `appsettings.Development.json:16`. Document the ngrok restart step in the runbook; no code change. |
| Race: frontend `ConfirmPaymentAsync` vs webhook arrive simultaneously | Low | Both paths converge on `ProcessApprovedPaymentAsync`'s `Transactions.MercadoPagoId` idempotency check (`PaymentService.cs:224-232` and `407-413`). Verified — no new idempotency code needed. Add an integration test that exercises both paths against the same payment id. |
| `pending_email_send` row insert fails (DB down) after payment commit | Low | Queue insert runs in its own transaction after commit. If it fails, payment is still committed and user has tickets — email is best-effort. Log loudly. A future BackgroundService retry could scan transactions with no email record. |
| Returning 200 to malformed envelopes hides real bugs | Med | Log warning with the raw envelope (truncated) at Warning. Add a counter/metric in a future iteration. Defense: a malformed envelope from MP itself means MP changed shape — we WANT to ACK so MP stops hammering us, then investigate via logs. |
| Resend custom domain not verified before merge to prod | Med | Config knob lets us merge to main with dev default `tickets@resend.dev`; deployment to prod blocked on runbook step (domain verification). Add a `// PROD GATE:` comment next to the config setting. |

## Rollback Plan

Each work item is independently revertible:

1. DTO revert — restore the old `WebhookPayload` shape. (Take the webhook back to 400; documented above as the known-bad state.)
2. `GetPaymentByIdAsync` — remove the method; only used by the new webhook flow. No other caller.
3. `ProcessWebhookAsync` — git revert the file; old behavior restored.
4. Controller — git revert the file.
5. Email resilience — git revert `PaymentService.cs` + remove the `PendingEmailSend` entity / DbSet. The `pending_email_send` table can stay (dormant) — drop later via a follow-up migration if needed.
6. Retry endpoint — git revert; table remains.
7. Config knobs — revert `appsettings*.json` deltas; no runtime impact.

Full rollback: revert the merge commit. The only durable side-effect is the new Supabase table, which is safe to leave in place or drop with a follow-up migration.

## Dependencies

- Resend dashboard access to verify a custom domain (out-of-band, runbook step).
- MP access token already configured (`MercadoPagoOptions.AccessToken`) — `GET /v1/payments/{id}` uses the same Bearer auth as existing calls.
- Supabase service-role key for the backend (already in use).

## Success Criteria

Acceptance (all must pass to mark the change done):

- [ ] MP's real webhook body (`{ action, type, data: { id } }`) returns HTTP **200** from `/api/payments/webhook`. Verified with a captured real MP payload replayed against the dev ngrok URL.
- [ ] For an `approved` payment, the webhook creates tickets + an approved `Transaction` row, exactly as the frontend `confirm` path does. Verified by inspecting `Tickets` and `Transactions` after a webhook-only test payment.
- [ ] A duplicate webhook for the same payment id returns 200 and creates **no** new tickets/transactions (idempotency preserved at `PaymentService.cs:224-232`).
- [ ] A webhook for a non-`approved` status (e.g. `rejected`, `pending`) calls `ProcessFailedPaymentAsync` and returns 200 — no 400, no 500.
- [ ] When `SendTicketEmailAsync` throws, the webhook still returns 200, an `Error` log is emitted with reservation/payment/email, and a `pending_email_send` row is inserted with `status='pending'` and `attempts=0`.
- [ ] `POST /api/payments/emails/retry-pending` (admin-gated) re-sends a queued email, marks the row `status='sent'` on success, increments `attempts` and stores `last_error` on failure, and flips to `exhausted` at `max_attempts`.
- [ ] `dotnet test` passes with no new failures; new tests include: unit tests for the new DTO deserialization, `MercadoPagoClient.GetPaymentByIdAsync` (mocked `HttpClient`), the rewritten `ProcessWebhookAsync` (approved / rejected / duplicate / missing-data-id), the email-queue catch block, and the retry endpoint.
- [ ] `Resend:FromEmail` is configurable via `appsettings.json`; production deployment is blocked until a verified custom domain is set (runbook step documented).

## Out of Scope / Follow-Ups

- `BackgroundService` timer-driven retry (this change ships the manual admin endpoint; the timer is a follow-up).
- Email send metrics / dashboards (log-only for now).
- Multi-provider payment routing.
- Idempotency hardening beyond the existing `Transactions.MercadoPagoId` check.
- Frontend test runner setup.
- Migration of any third-party webhook consumer of the old `WebhookPayload` shape (there are none other than MP).

## Proposal question round

Run during interactive shaping — captured assumptions below for traceability. Resolved by user before finalizing:

1. **Business problem:** buyers pay but don't get emails → support tickets + reputational risk. Confirmed as worth doing now.
2. **Email-on-failure behavior:** user chose **"Log + reintentar"** — webhook returns 200 to MP, logs Error, queues for retry. (Reflected in Work Items 5 & 6.)
3. **Retry trigger:** manual admin endpoint, not a background timer (simplest sound design). User confirmed full scope incl. retry mechanism.
4. **Resend domain:** custom verified domain recommended; `tickets@resend.dev` kept as dev default. Out-of-band verification is a runbook step, not code.
5. **Scope:** all three (webhook fix + email swallow + Resend domain) inside this single change. User confirmed.

No open questions remaining; proposal ready for `sdd-spec`.