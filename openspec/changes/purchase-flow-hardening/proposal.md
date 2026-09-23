# Proposal: Purchase-Flow Hardening (Refund Integrity, Webhook Resilience, Scan Concurrency)

**Change name:** `purchase-flow-hardening`
**Intent:** Close the integrity and resilience gaps found in a read-only end-to-end audit of the purchase flow: a Mercado Pago refund/chargeback that leaves tickets valid (G1), an always-200 webhook ACK with no retry or reconciliation for payments (G2), a concurrent double-scan race on QR validation (M1), plus the stale tests and operational security items the audit surfaced.

**Source:** read-only E2E audit of 2026-09-23 (no code changed by the audit). Evidence below was verified line-by-line against the working tree.

---

## Problem Statement

### G1 — GRAVE: MP refund/chargeback after approval silently no-ops; tickets stay scannable

- `IsFinalFailureStatus` treats `refunded` / `charged_back` as terminal failures (`backend/Services/PaymentService.cs:302-306`) and routes them to `ProcessFailedPaymentAsync`.
- That method inserts **another** `Transaction` with the same `MercadoPagoId` (`PaymentService.cs:425-445`) without checking for an existing `Approved` row, while the approved path already created one.
- `ApplicationDbContext.cs:153` declares `HasIndex(t => t.MercadoPagoId).IsUnique()` → the insert throws `DbUpdateException`. The concurrency catch exists **only in the approved path** (`PaymentService.cs:381-389`); the failure path has none, so the exception escapes to the controller catch-all → **HTTP 200 ACK** (`PaymentController.cs:164-169`).
- Result: reservation stays `Confirmed`, `Tickets.IsRefunded` stays `false`, QR keeps validating. Money refunded, ticket still usable.
- The unit tests cannot catch this: they use EF InMemory, which does not enforce unique indexes.

**Repro:** buy a ticket → refund that payment from the MP dashboard → backend log shows `Unexpected error processing webhook` + `duplicate key value violates unique constraint "IX_Transactions_MercadoPagoId"` → `SELECT "IsRefunded" FROM "Tickets" WHERE "ReservationId" = '<id>'` is still `false` → QR scan returns valid.

### G2 — GRAVE: Webhook always ACKs 200, and no retry/reconciliation exists for payments

- `PaymentController.Webhook` returns `Ok(...)` for every non-auth failure and for every unhandled exception (`PaymentController.cs:147-169`). The code itself notes MP retries on non-200 (`PaymentController.cs:104,151`) — that protection is deliberately forfeited.
- No payment retry queue and no reconciliation hosted service exist: `Program.cs:87-88` registers only `ReservationExpirationService` and `EventNotificationDispatchService`. (Email failures DO have `pending_email_send` + retry — the payment path has the weaker guarantee than the less critical one.)
- `MercadoPagoClient.GetPaymentByIdAsync` throws on any non-404 error (`MercadoPagoClient.cs:168-173`, no try/catch), so an MP API blip during webhook processing → catch-all → 200 → MP never retries → **paid customer with no tickets and no refund**.
- Same hole in the automatic-refund branch: if `InitiateRefundAsync` fails for an expired reservation (`PaymentService.cs:403-418`), the webhook ACKs 200 and the money is never returned.

**Repro:** in a scratch environment point the MP token/base URL at something unreachable, replay an approved payment's webhook (`curl -X POST /api/payments/webhook -d '{"action":"payment.updated","type":"payment","data":{"id":"<APPROVED_ID>"}}'`) → 200 `{"status":"acknowledged"}`, zero tickets, zero transactions, no retry.

### M1 — MEDIUM: Concurrent double-scan of the same QR

- `Ticket` has no concurrency token (`backend/Models/Ticket.cs:12` — only `IsUsed`).
- `ValidateQRCodeAsync` does read-check-write without a lock (`TicketService.cs:272-411`): plain `FirstOrDefaultAsync`, then `IsUsed` check, then `IsUsed = true` + save. Under READ COMMITTED two simultaneous scans can both succeed.

### G3 — Operational security (separate track, not code in this change)

- The Supabase DB password and project-ref are committed and pushed: `.engram/sessions/2026-07-13-martin.md` and `openspec/changes/ticketera-online/apply-progress.md` (verified across all 381 commits; remote `github.com:Tined-Solutions/ticketstart`).
- The MP production token is **not** in history (0 matches), `backend/appsettings.Development.json` is properly gitignored (`.gitignore:26`) and untracked.
- Action: rotate the DB password, gitignore `.engram/`, scrub history or accept the risk explicitly.

### Stale tests (not product bugs, but they hide regressions)

Three tests fail today and assert outdated contracts — verified against the code:

| Test | Reality |
|---|---|
| `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized` | Product intentionally returns 200 ACK (`PaymentController.cs:149-155`); the mock exercises a failure type the service no longer produces. |
| `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized` | Asserts the pre-`fix-mp-webhook-400` short-circuit; signature mismatch now continues and integrity rests on the MP API fetch. |
| `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` | Posts to `/webhook` instead of `/api/payments/webhook` (`AuthCookieTests.cs:296` vs `CsrfHeaderMiddleware.cs:31`) — wrong URL. |

### Known UX gap: back/forward during the reservation hold (LOW)

- The cart survives history navigation (`location.state`), but the reservation lives only in component state — there is no persistence and no unmount cleanup that cancels it (`frontend/src/pages/Checkout.jsx:42-89, 233-238`).
- Back from the "Confirma tu reserva" phase → the reservation stays `Active`, holding stock for up to 10 minutes; forward → phase 1 form again (data lost) → resubmitting creates a **second reservation** (double hold, bounded by expiry). No double charge.

---

## Root Cause

1. **G1:** the terminal-failure path was written assuming it is the first write for a payment id; the unique index plus the approved-path idempotency check make it unreachable for any payment that was already approved. The exception is swallowed by the always-200 policy.
2. **G2:** "always 200 to MP" was chosen to stop MP's retries, but no internal retry/reconciliation replaced the guarantee MP provides — so the failure mode moved from "MP retries forever" to "silent loss".
3. **M1:** the QR validation transaction provides atomicity but not isolation against a concurrent read-check-write (no row lock, no version column, no conditional update).
4. **Tests:** they encode pre-refactor contracts and EF InMemory hides relational constraints.

## Non-Goals

- No redesign of the MP integration (no multi-provider, no preference-flow changes).
- No change to the "always ACK" policy for ignorable notifications (non-terminal statuses, unknown payment ids) — only for genuine processing failures.
- No backfill of historical payments (that is what the reconciliation work item is for, run once).
- No frontend test harness changes beyond the affected specs.
- G3 is tracked here for visibility only; it is an ops/security task, not part of the code change.

---

## Proposed Approach

### Work Item 1 — Refund/chargeback must invalidate tickets (G1)

- In `ProcessFailedPaymentAsync`, look up an existing transaction by `MercadoPagoId` first: if an `Approved` row exists, do **not** insert a duplicate — mark the refund/chargeback on the existing record (or add a dedicated refund record keyed by a different unique key) and invalidate the reservation's tickets (`IsRefunded = true`) inside a transaction, mirroring the admin refund path (`AdminPurchaseService`).
- Keep the reservation state machine consistent (Confirmed → refunded/cancelled as appropriate).
- Tests must exercise the unique index (relational provider, not InMemory) so the regression cannot return.

### Work Item 2 — Retry + reconciliation for payment processing (G2)

- Return a **retryable** status (5xx) from the webhook catch-all and for `FailureType.Processing`, keeping 200 for auth noise, unknown ids and non-terminal statuses. MP then retries on its own schedule.
- Add a `PaymentReconciliationService` hosted service: periodically fetch approved payments from MP for the recent window and process any that have no `Approved` `Transaction` in our DB (reusing `ProcessApprovedPaymentAsync`). This closes the whole "lost webhook" class, including the `GetPaymentByIdAsync` failure and the 404-for-a-real-payment case (`MercadoPagoClient.cs:170-171`).
- Surface processing failures (structured log + counter) so silent loss becomes visible.

### Work Item 3 — QR scan concurrency (M1)

- Add a concurrency token to `Ticket` (`RowVersion`/`xmin`) **or** switch to a conditional update (`UPDATE ... SET "IsUsed" = true WHERE "Id" = @id AND "IsUsed" = false`, assert affected rows). Prefer the conditional update: no schema migration, atomic by construction.

### Work Item 4 — Fix or retire the three stale tests

- Update each to the current contract (200 ACK, correct webhook URL) or delete with a note. Target: a fully green suite so future regressions are visible.

### Work Item 5 — Checkout back/forward UX (LOW)

- Persist the created reservation (id + token + `expiresAt`) in `sessionStorage`, keyed by cart signature (event + ticket type + quantity) so a deliberately new purchase does not resurrect a stale reservation. Clear the entry on confirmed payment and on expiry.
- **Security note (token exposure):** the reservation token is a capability — HMAC over `reservationId:nonce:timestamp`, 10-minute expiry, bound to the reservation (`ReservationService.GenerateReservationToken` / `ValidateReservationToken`, `ReservationService.cs:464-536`) — that authorizes PATCH of purchaser data (email/DNI) and payment-preference creation. Persisting it moves it from React memory to origin-scoped storage, so a future XSS could read it in one line; bounded by the 10-minute TTL, tab-scoped lifetime, and the app's current lack of `dangerouslySetInnerHTML` (verified: no occurrences in `frontend/src`). Zero-capability variant if preferred: persist only the purchaser form fields and re-create the reservation on return (no token at rest; the orphan hold still self-heals by expiry).
- **Do NOT cancel the reservation on unmount.** At the Mercado Pago redirect the component unmounts while the payment is pending, so a naive unmount-cancel would kill the reservation right before payment (auto-refund branch, and G2 if the refund call fails). There is also no cancel endpoint today — only `IReservationService.CancelReservationAsync` (`IReservationService.cs:65`) with no controller route. Rely on the 10-minute expiry for orphans.
- Add a retry control to the `pending` state in `CheckoutSuccess.jsx` (currently only `error` has one, `CheckoutSuccess.jsx:176-182`).

### Work Item 6 — Ops/security (G3)

- Rotate the Supabase password; add `.engram/` to `.gitignore`; scrub the leaked docs from history or document the accepted risk.

### Work Item 7 — Return from Mercado Pago: unstuck the pay button and re-verify before re-paying

**Finding (live test, 2026-09-23, headless Chrome against the local env):** pressing the browser **Back** button from the Mercado Pago page restores the checkout from bfcache (same document) and the pay button stays **permanently stuck** on "Preparando pago…" (disabled) — `handlePay` leaves `payLoading = true` on the success path and bfcache preserves the frozen React state. No error is shown; the only way out is F5 (WI5 then restores phase 2 with the button enabled). The reservation keeps holding stock in the meantime.

**Related risk:** the app **discards the `preferenceId`** that `create-preference` already returns (`PaymentController.cs:60-64`), so on return it cannot tell whether a payment is in flight. After an F5 the buyer can create a second preference for the same reservation; if the first payment was already approved and its webhook has not processed yet, paying that second preference charges twice.

**Approach:**
- Persist the `preferenceId` in the checkout sessionStorage entry when a preference is created.
- On return (mount and `pageshow` with `persisted`), re-verify with `POST /payments/confirm` **before** rendering an actionable pay button: `confirmed` → confirmed panel (clear the entry, invalidate queries); a pending payment → pending state with "Verificar de nuevo" and no re-pay offer; no payment → phase 2 with the pay button enabled.
- Reset `payLoading` on bfcache restore so the button is never left dead.
- Backend: `ConfirmPaymentAsync` must distinguish "no payment at all" from "a non-approved payment exists" (`SearchPaymentsByExternalReferenceAsync` already returns every status) and expose it in the `confirm` response through a new backward-compatible `reason` field, keeping the `{status: confirmed|pending}` contract that `CheckoutSuccess` already consumes.

## Rollback Plan

- WI1/WI2 touch the payment webhook: ship behind the existing tests + a manual MP sandbox replay; each work item is independently revertable (no schema migration in WI1-WI2 except WI3's optional token).
- WI3 (conditional update) is a pure query change; reverting restores current behavior.
- WI5 is frontend-only.
- WI7 is frontend plus a backward-compatible `reason` field on the existing confirm response.
- G3 rotation is independent of code deploy.

## Evidence References

- Audit memory: Engram observation #801 (`analysis/purchase-flow-audit`).
- Related shipped fix: purchaser email normalization at the write boundary (`fix/purchaser-email-normalization`, Engram #800).
