# Exploration: dynamic-refund-amount

Model A — admin selects K tickets and enters the refund amount (0 < amount ≤ unit price × K). Percent input is frontend sugar only; backend receives one decimal `amount`. Ledger-only refund unchanged (no MP money movement, no email, no motivo). Tickets still become unusable (IsRefunded).

## Current State (verified)

- `AdminController.RefundPurchase` (backend/Controllers/AdminController.cs:287-321): POST `/admin/events/{eventId}/purchases/{reservationId}/refund`, body `RefundPurchaseRequest(int Quantity)` (line 550, positional record, deliberately NO data annotations — validation lives in the service → InvalidOperationException → 409). Audit after commit (APR-007), details string "Admin refunded {quantity} tickets of purchase {reservationId} for event {eventId}".
- `AdminPurchaseService.RefundPurchaseAsync` (backend/Services/AdminPurchaseService.cs:123-227): execution strategy + tx; loads reservation + TicketType; `AcquireTicketLocksAsync` row-locks; aborts if any ticket IsUsed; `active = count(!IsRefunded && !IsUsed)`; guards `0 < K ≤ active`; requires Approved tx; selects K oldest (APR-013); marks IsRefunded+RefundedAt; inserts ONE Refunds row with `Amount = TicketType.Price × K` (D7, line 202); flips Approved→Refunded ONLY when active == K (D2).
- `GetPurchasesAsync` (AdminPurchaseService.cs:32-119): `RefundedAmount = Σ Refunds.Amount` per reservation (lines 87-94), `TotalRefunded = Σ Refunds.Amount` per event (line 117), `Refunded` derived from `RefundedQuantity >= Quantity` (line 112). **None of this math assumes unitPrice × K — it survives custom amounts untouched.**
- `Refund` model (backend/Models/Refund.cs): Amount is `decimal(18,2)` (ApplicationDbContext.cs:249, migration 20260814134333 `numeric(18,2)`), no CHECK constraint. TicketIds uuid[], Quantity int, AdminId nullable (legacy APR-014).
- Frontend `AdminPurchases.jsx`: `RefundConfirmationDialog` has quantity input (`refund-quantity`, min 1 / max activeRemaining), preview `unitPrice × K` where `unitPrice = purchase.amount / purchase.quantity` (line 36), mutation posts `{ quantity }` (line 115). `formatCurrency` (lib/format.js) renders **0 fraction digits** (ARS whole pesos) — relevant for a cents-capable amount input.
- Canonical spec: `openspec/specs/admin-purchase-refunds/spec.md` (APR-001…APR-015). Change folder `openspec/changes/dynamic-refund-amount/` does not exist yet.

## Affected Areas

### Backend
- `backend/Controllers/AdminController.cs` — DTO `RefundPurchaseRequest` (add `decimal Amount`); pass amount to service; audit detail string should include the amount (keep "no motivo" invariant, ≤1000 chars).
- `backend/Services/IAdminPurchaseService.cs` — `RefundPurchaseAsync(reservationId, quantity, adminId)` → add `decimal amount` param; doc comments ("Amount = unit price × K") must be rewritten.
- `backend/Services/AdminPurchaseService.cs` — the core change: new guard `0 < amount ≤ TicketType.Price × quantity` (+ 2-decimal policy), ledger `Amount = amount` instead of `unitPrice * quantity`. Everything else (locks, selection, D2 flip, tx) untouched.
- `backend/Models/Refund.cs` — doc comment only ("= unit price × K (D7)" no longer always true). **No schema change recommended.**

### Frontend
- `frontend/src/pages/AdminPurchases.jsx` — `RefundConfirmationDialog`: add amount input (step 0.01) + percent helper; prefill `amount = K × unitPrice` (today's behavior becomes the default); mutation posts `{ quantity, amount }`.
- `frontend/src/lib/format.js` — possibly a 2-decimal variant for dialog previews (`formatCurrency` hides cents; admins entering cents can't see them in previews today).

### Specs (delta targets in `openspec/specs/admin-purchase-refunds/spec.md`)
- **APR-003** — body `{ "quantity": K }` → `{ "quantity": K, "amount": A }`; new guard 0 < A ≤ unitPrice × K. MODIFIED.
- **APR-010** — dialog gains amount input; mutation posts `{ quantity, amount }`. MODIFIED.
- **APR-012** — "Amount (= unit price × K)" → "Amount = admin-defined value ≤ unit price × K". MODIFIED.
- **APR-011** — test coverage list. MODIFIED.
- Purpose paragraph — mention admin-defined amount (minor). Non-Goals, APR-002, APR-004/013/014 unchanged (Σ formulas still hold).

## Amount = unitPrice × K assumption sites (inventory)

1. `AdminPurchaseService.RefundPurchaseAsync` line 202 — the only computation site. Replaced by admin amount.
2. `AdminController` audit string (line 303) — quantity mentioned, amount should be added.
3. `IAdminPurchaseService` / `Refund.cs` doc comments — prose only.
4. `GetPurchasesAsync` RefundedAmount/TotalRefunded — Σ Refunds.Amount, **no change needed**.
5. Frontend preview `unitPrice * selectedQuantity` — replaced by amount input (prefilled to K × unitPrice).
6. Specs listed above.

## Approaches

### 1. Amount param end-to-end, NO migration (recommended)
DTO `RefundPurchaseRequest(int Quantity, decimal Amount)`; service signature gains `decimal amount`; guard `0 < amount ≤ TicketType.Price × quantity` throws InvalidOperationException → 409 (same bucket as quantity guards, consistent with the DTO's no-annotations convention); ledger stores the admin amount verbatim. "Custom refund" is derived contextually (`Amount ≠ Price × K`), never stored.
- Pros: minimal diff; no migration/snapshot churn; Σ Refunds.Amount semantics untouched; cap checked inside the tx against the locked reservation's TicketType (race-safe); matches the agreed Model A exactly.
- Cons: custom-vs-full-price is inferred, not stored; the pre-existing TicketType.Price-mutation quirk (cap uses current price, tx.Amount was historical) remains — out of scope, flag in design.
- Effort: Low-Medium.

### 2. Same + explicit `IsCustomAmount` column
- Pros: explicit reporting/audit query surface.
- Cons: new migration + model snapshot + APR-014 legacy backfill rows must be backfilled to false; no current consumer; YAGNI.
- Effort: Medium.

### 3. Backend receives percent
- Rejected by the agreed Model A: backend receives one decimal `amount`; percent is UI sugar only. Also pushes rounding semantics to the server for no benefit.
- Effort: n/a.

## Validation boundary decision (recommendation)

| Rule | Where |
|------|-------|
| JSON body present | [ApiController] auto-400 (existing) |
| `amount > 0`, `amount ≤ TicketType.Price × quantity`, `quantity` range, no Approved tx, used ticket | Service → InvalidOperationException → 409 (repo convention: DTO has no annotations) |
| ≤ 2 decimal places | Service rejects with 409 (WYSIWYG ledger: what the admin confirmed is what numeric(18,2) stores; PG would otherwise silently round) |
| Percent → amount conversion, rounding, inline UX errors, disabled submit | Frontend only (sugar) |

Rounding policy: client converts percent with integer-cents math (`Math.round(pct × totalCents / 100) / 100`, half-up) to avoid float drift; backend rejects >2 decimals rather than rounding.

## Frontend UX notes

- Prefill amount input with `K × unitPrice` → untouched dialog behaves exactly like today (backward-compatible UX and tests).
- Percent helper: separate small input or quick buttons (25/50/100%); conversion is client-side; amount field always shows the resulting value.
- On quantity change: if amount was percent-derived, recompute; if manually typed, re-validate against the new cap `K × unitPrice`.
- Reuse existing conventions: `glass-surface` dialog, `border-border bg-surface text-text-1` inputs, `min-h-[44px]` targets, `role="alert"` error box (rose), Button variants. No new tokens needed.
- Flag for design: `formatCurrency` shows whole pesos only; the amount input needs `step 0.01` and cent-visible preview.

## Edge cases

- **amount == full price of K** — identical to today; no special path; existing parity test should prove it.
- **amount < price of oldest K** — allowed by Model A; tickets are fungible; ledger records actual money returned.
- **Cumulative custom refunds cannot exceed total paid** — proven: each op amount ≤ Kᵢ × unitPrice and Σ Kᵢ ≤ Quantity ⇒ Σ amounts ≤ tx.Amount. Add a property test to lock it.
- **TicketType.Price mutated after purchase** — cap uses current TicketType.Price (D7 canonical), which may differ from historical tx.Amount. Pre-existing quirk (already true today for the ledger Amount); document, don't fix here.
- **K > active remaining with a valid amount** — quantity guard fires first (existing message), then amount guard; order quantity→amount for clearer errors.
- **Float precision** — client integer-cents math; backend decimal(18,2) exact.
- **Concurrent refunds** — existing lock trio + re-read inside tx covers the amount cap too (reservation read inside the transaction).
- **Legacy APR-014 backfilled rows** — Amount from tx; inference `Amount ≠ Price × K` still yields "full-price" for them. No backfill needed.

## Test impact inventory

**Backend — broken (mechanical):**
- `AdminPurchaseServiceTests`: ~10 call sites of 3-arg `RefundPurchaseAsync` → 4-arg; `RefundPurchaseAsync_InsertsRefundRow_...` (asserts 200m = 100 × 2); `RefundPurchaseAsync_Cumulative_...` (asserts every row 200m, Σ 400m).
- `AdminControllerPurchaseTests`: ~8 `RefundPurchaseRequest(n)` constructor sites; mock `It.IsAny<int>()` verifies gain a decimal arg; `RefundPurchase_Success_PassesQuantityBodyAndWritesAuditWithoutMotivo` audit assertion updated if details include the amount.

**Backend — new:**
- amount == full price parity; custom partial amount ledger row (exact amount, K tickets, D2 unchanged); amount ≤ 0 → 409; amount > cap → 409; >2 decimals → 409 (if adopted); cumulative custom Σ ≤ tx.Amount; controller body `{quantity, amount}` flow + audit-with-amount-still-no-motivo.
- FsCheck property suite (pattern: `PaymentPropertyTests`): for arbitrary valid (K, amount): ledger Amount == amount, exactly K tickets marked, Σ reservation refunds ≤ tx.Amount, D2 flip iff active == K.

**Frontend (Vitest):**
- Update `partial refund via quantity selector posts {quantity}` (line 150) → posts `{quantity, amount}`; preview assertions change.
- New: prefill = K × unitPrice; percent → amount conversion; invalid amount blocks submit (0, negative, > cap); backend 409 error path (already covered pattern); cap re-validation when quantity changes.

## Recommendation

Approach 1 (amount param end-to-end, no migration). Validation in the service per repo convention (409), DTO extended positionally, frontend owns percent sugar and prefill. Keep D7 (TicketType.Price as the cap basis) for consistency with the existing ledger. Defer any explicit `IsCustomAmount`/`UnitPriceAtRefund` columns until a concrete reporting need exists.

## Risks

- **Medium** — Frontend float precision in percent→amount conversion (mitigate: integer-cents math + backend 2-decimal rejection).
- **Medium** — `formatCurrency` hides cents while the new input accepts them (UX confusion; decide preview format in design phase).
- **Low** — Wide-but-mechanical signature change: ~12 `RefundPurchaseAsync` mock/call sites + ~8 DTO constructor sites across tests (compiler catches all).
- **Low** — TicketType.Price mutation makes the cap diverge from historical tx.Amount (pre-existing; document in design).
- **Low** — Breaking API body shape for any scripted caller (SPA is the only known consumer).
- **Low** — Audit string change; keep the no-motivo assertion green.

## Ready for Proposal

Yes — Model A is fully mapped; the only open product-level choices for the design phase are: (a) percent helper as input vs quick buttons, (b) cent display in dialog previews, (c) reject-vs-round for >2 decimals (recommend reject).
