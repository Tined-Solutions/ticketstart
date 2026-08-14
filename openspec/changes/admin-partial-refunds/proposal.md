# Proposal: Admin partial refunds (per-quantity, cumulative)

## Intent

Admins can only record a refund in all-or-nothing terms: `RefundPurchaseAsync(reservationId, adminId)` marks every ticket of a purchase refunded and flips the Approved Transaction to `Refunded` (`AdminPurchaseService.cs:97-200`). The business needs partial refunds — refund K of N tickets of one purchase (tickets are fungible: one TicketType per Reservation, same unit price) — with multiple operations accumulating over time. Without per-operation records, cumulative refunds and their audit trail (which admin, when, which tickets, how much) cannot be reconstructed and `TotalRefunded` becomes a guess. This change tracks each refund operation in a new `Refunds` table.

## Current-state gap

- Refund is binary: all tickets flipped + tx flipped Approved→Refunded; no partial path, no per-operation record.
- No `Refunds` table → legacy Refunded transactions have no operation rows; cumulative refunds + per-op audit impossible.
- `AdminPurchaseRow` exposes only a boolean `Refunded` → admin cannot see how many of N were refunded or refund part of a purchase.

## Scope

### In Scope
- New `Refund` entity + migration `AddRefundsTable` with **pure-SQL INSERT…SELECT backfill** of legacy Refunded transactions (`AdminId` nullable).
- `RefundPurchaseAsync(Guid reservationId, int quantity, Guid adminId)` — reuses three-provider lock trio; under-lock guards: any used → block; `quantity ≤ 0` or `> active non-refunded` → block; Approved tx must exist; deterministic selection of K oldest non-refunded/non-used tickets; insert `Refund` row (TicketIds[], Quantity, Amount); flip Approved→Refunded ONLY when the operation leaves 0 active tickets.
- `AdminPurchaseRow` (constructor-positional record) gains `RefundedQuantity` + `RefundedAmount`; `Refunded` derived (fully refunded); `TotalRefunded` = Σ `Refunds.Amount`.
- Controller: POST refund gains body DTO `RefundPurchaseRequest(int Quantity)`, validated > 0; audit AFTER commit (APR-007, no motivo).
- Frontend `AdminPurchases.jsx`: quantity selector in confirm dialog (1..active), live amount preview, row "X de Y reembolsadas" (error badge when fully refunded, warning when partial), button disabled when fully refunded, mutation posts `{ quantity }`.
- Tests (strict TDD): replace 4-5 binary-refund tests (`AdminPurchaseServiceTests.cs:143,217,246,284`; `AdminControllerPurchaseTests.cs:117`) + ~10 new partial-refund tests (partial happy path, cumulative second refund, quantity>active blocked, quantity≤0 blocked, flip only at 0 active, scan race with partial state, Refunds row recorded with TicketIds/Amount, legacy backfill).

### Out of Scope
- No Mercado Pago / external refund API call (manual MP dashboard return; APR-008 stands).
- No motivo/refund reason (APR-008).
- No per-ticket UI selection (selection by quantity — tickets fungible).
- No Reservation status change; no auto-refund path change (`PaymentService.InitiateRefundAsync` untouched).
- No editing/reverting a refund operation.

## Capabilities

> Contract with sdd-spec. Research ran against `openspec/specs/admin-purchase-refunds/`.

### New Capabilities
- None.

### Modified Capabilities
- `admin-purchase-refunds`: 
  - **APR-002** (list) — row gains `RefundedQuantity`/`RefundedAmount`; per-event `TotalRefunded` = Σ `Refunds.Amount` (was Σ Refunded tx amounts).
  - **APR-003** (refund) — full → partial + cumulative: takes `quantity`; records one `Refunds` row per op; deterministic oldest-tickets selection; Approved→Refunded flip ONLY when 0 active tickets remain.
  - **APR-010** (UI) — quantity selector + live amount preview + "X de Y reembolsadas" badge + disabled-when-fully-refunded; mutation posts `{ quantity }`.
  - **APR-011** (tests) — replace binary-refund tests + add cumulative/partial tests; frontend vitest mock shape + assertions.
  - Spec phase adds new requirement IDs for the cumulative Refunds record, deterministic selection, and legacy backfill.
  - **Unchanged**: APR-001 (Admin auth), APR-004 (used-ticket block — ANY used blocks the whole op), APR-005/006/009 (per-ticket `!IsRefunded` already correct for partial), APR-007 (audit after commit), APR-008 (no MP/motivo).

## Locked business rules

1. **Selection by quantity** — refund K of N (tickets fungible; one TicketType per Reservation, same unit price).
2. **Cumulative** — multiple refund ops on the same purchase allowed; each recorded in `Refunds` (ReservationId, TicketIds[], Quantity, Amount, AdminId?, CreatedAt); `TotalRefunded` = Σ `Refunds.Amount`.
3. **Used-ticket policy unchanged** — ANY ticket `IsUsed` → the whole refund op is blocked.
4. **No motivo** — APR-008 stands.
5. **Admin refund never calls MP** — local state only; real money returned manually in the MP dashboard. True for partial refunds too.
- **Derived invariants**: Refunded `Transaction` == fully refunded purchase (flip only when 0 active tickets remain); refunded tickets already excluded by per-ticket `!IsRefunded` (APR-005/006/009 stay coherent without change).

## Approach

New `Refund` entity (ReservationId, `TicketIds uuid[]` PG-only — `PendingEmailSend.TicketIds` precedent, Quantity, Amount, `AdminId Guid?`, CreatedAt) + `Refunds` DbSet. Migration `AddRefundsTable` creates the table and **pure-SQL backfills** one Refund row per pre-existing Refunded Transaction (AdminId null) via `INSERT…SELECT` with `array_agg` over refunded tickets — EF-context backfill inside `Up()` provably fails on first apply (memory #442). `RefundPurchaseAsync(reservationId, quantity, adminId)` reuses the Npgsql `FOR UPDATE` / SQLite no-op / InMemory read trio; under lock: block if any used, block if `quantity ≤ 0` or `quantity > active`, require Approved tx. Mark `IsRefunded`/`RefundedAt` on the K oldest non-refunded/non-used tickets (`OrderBy(CreatedAt).Take(K)`); insert the Refund row (Amount = unit price × K); flip Approved→Refunded ONLY when 0 active tickets remain. `GetPurchasesAsync` projects RefundedQuantity (count IsRefunded) + RefundedAmount (Σ Refunds) and `TotalRefunded` = Σ `Refunds.Amount`; `Refunded` bool derived. Controller takes `RefundPurchaseRequest(int Quantity)`; audit after commit (APR-007). Frontend quantity selector (1..active) with live amount preview → POST `{ quantity }`. No change to MetricsService / EventService / ReservationService / TicketService / PaymentService / AuditActionType (all per-ticket or neutral — verified in explore).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Models/Refund.cs` | New | Refund entity (ReservationId, TicketIds[], Quantity, Amount, AdminId?, CreatedAt) |
| `backend/Data/ApplicationDbContext.cs` | Modified | `DbSet<Refund>` + OnModelCreating; `uuid[]` PG-only mapping |
| New migration `AddRefundsTable` | New | CreateTable + pure-SQL `INSERT…SELECT` backfill of legacy Refunded tx |
| `backend/Services/IAdminPurchaseService.cs` | Modified | `AdminPurchaseRow` +RefundedQuantity/RefundedAmount (positional record → breaks 9-arg constructions); `RefundPurchaseAsync` +quantity |
| `backend/Services/AdminPurchaseService.cs` | Modified | Partial+cumulative refund logic, deterministic selection, flip-only-when-zero-active, TotalRefunded=Σ Refunds |
| `backend/Controllers/AdminController.cs` | Modified | POST refund body DTO `RefundPurchaseRequest(int Quantity)`, validated > 0 |
| `frontend/src/pages/AdminPurchases.jsx` | Modified | Quantity selector dialog, live amount, "X de Y" badge, disabled-when-fully-refunded, mutation `{ quantity }` |
| `backend/Tests/`, `frontend/.../__tests__/` | Modified | Replace 4-5 binary-refund tests + ~10 new partial-refund tests; frontend mock shape + assertions |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `TotalRefunded` regression for legacy refunds (no Refunds rows → drops to 0) | High | Pure-SQL `INSERT…SELECT` backfill in `AddRefundsTable` (AdminId nullable); dedicated legacy-backfill test |
| Binary-refund test breakage (4-5 tests encode full-refund semantics + 9-arg record ctor) | Med-High | Strict TDD replace (Red→Green) at AdminPurchaseServiceTests:143,217,246,284 + AdminControllerPurchaseTests:117 |
| Concurrency: two concurrent partial refunds | Med | Ticket `FOR UPDATE` serializes; quantity guard runs on LOCKED list; second observes first's committed refunds; test serialization logic via InMemory path |
| `uuid[]` PG-only on SQLite test contexts | Low-Med | Don't query `TicketIds` from SQLite; `PendingEmailSend.TicketIds` precedent works today |
| ~600-900 line estimate vs 400-line review budget | Med | Delivery note: single-pr with maintainer-approved `size:exception` (budget 4000 lines); NOT chained PRs |

## Rollback Plan

Drop the `Refunds` table (additive, no FK dependency that blocks revert). Revert `IAdminPurchaseService`/`AdminPurchaseRow` to the boolean `Refunded` 9-arg positional record; restore `RefundPurchaseAsync(reservationId, adminId)` binary behavior (mark all tickets + flip Approved→Refunded); remove the `RefundPurchaseRequest` DTO and controller body validation; restore `GetPurchasesAsync` TotalRefunded = Σ Refunded tx amounts; revert `AdminPurchases.jsx` dialog/row to binary. Refunded tickets stay `IsRefunded` (history preserved — no data loss). Keep `RefundPurchase` audit rows. No one-time SQL needed: partial refund only flips Approved→Refunded at 0 active, so reverting the code restores the binary flip; leftover Refunds rows are dropped with the table.

## First-slice boundaries & non-goals

**First slice**: partial quantity refund + cumulative `Refunds` table + backfill + UI quantity selector + test suite rework.
**Non-goals**: per-ticket UI selection, MP API call, motivo, reservation status change, auto-refund path edits, reverting a refund op, organizer-facing refund view.

## Delivery note

Session `delivery_strategy = single-pr` with maintainer-approved `size:exception` (budget 4000 lines). Estimated ~600-900 changed lines (migration + entity + service refactor + DTO + record fields + frontend dialog + ~20 test updates/additions). This proposal does NOT propose chained PRs; deliver as one PR with work-unit commits.

## Dependencies

- None external. Reuses EF Core 9, Npgsql `FOR UPDATE` trio precedent, `uuid[]` PG mapping precedent (`PendingEmailSend`), existing `RequireAdminRole` policy, varchar-stored `AuditActionType.RefundPurchase` (no migration).

## Success Criteria

- [ ] Admin refunds K of N tickets; K selected = oldest non-refunded/non-used; remaining N-K stay active & valid at scan.
- [ ] Cumulative refunds accumulate in `Refunds`; `TotalRefunded` = Σ `Refunds.Amount` (incl. legacy backfilled rows).
- [ ] Refund blocked when ANY ticket `IsUsed`; blocked when `quantity ≤ 0` or `> active`; blocked when no Approved tx.
- [ ] Approved tx flips to Refunded ONLY when 0 active tickets remain; partial ops leave the tx `Approved`.
- [ ] `AdminPurchaseRow` shows `RefundedQuantity` + `RefundedAmount`; UI "X de Y reembolsadas" + disabled when fully refunded.
- [ ] No MP call, no motivo (APR-008); audit after commit (APR-007).
- [ ] `dotnet test` green (binary tests replaced + new partial tests); `npx vitest run` frontend updated; no new failures in ripple consumers.