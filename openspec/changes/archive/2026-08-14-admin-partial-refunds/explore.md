# Exploration: Admin partial refunds (per-quantity, cumulative)

> Read-only investigation for change `admin-partial-refunds`. Artifact store: hybrid
> (this file + Engram `sdd/admin-partial-refunds/explore`). All line numbers are
> current on-disk (Aug 2026). No source files were modified.

## Executive Summary

Admin manual refunds today are binary: `RefundPurchaseAsync(reservationId, adminId)`
marks ALL tickets of a confirmed purchase refunded and flips the Approved Transaction
to `Refunded` (`backend/Services/AdminPurchaseService.cs:97-200`). The locked business
decision makes refunds PARTIAL and CUMULATIVE: the admin inputs how many of N tickets
to refund (tickets are fungible — one TicketType per reservation), multiple refund
operations on the same purchase accumulate, and the transaction only flips to Refunded
when the operation leaves zero active tickets. This requires a new `Refunds` table
recording each operation. The proposed design direction is validated with evidence;
three refinements are required (legacy backfill of the Refunds table, a deterministic
"which K tickets" selection, and controller signature change with a quantity DTO), and
one stale premise is corrected: the Supabase migration history is NOW fully aligned
(15/15 applied, verified via `dotnet ef migrations list`) — the misalignment noted in
memory was fixed on 2026-08-10 (memory #442).

## Current State

**Stack**: ASP.NET Core net9.0 + EF Core 9 + PostgreSQL (Supabase), interface-based
services, Scoped DI (`backend/Program.cs:33-41`). Frontend: React 19 + Vite SPA,
axios client, TanStack Query.

**Admin purchases service** (`backend/Services/AdminPurchaseService.cs`):
- `GetPurchasesAsync(Guid eventId)` (:29-94) — event existence check (:33-41); confirmed
  Reservations with TicketType (:43-48); Transactions filtered to
  `Approved || Refunded` (:52-58); per-reservation linked ticket counts (APR-009,
  :63-70); row projection (:72-87); `totalRefunded = Σ Refunded tx amounts` (:89-91).
- `RefundPurchaseAsync(Guid reservationId, Guid adminId)` (:97-200) — execution
  strategy + BeginTransaction (:101-105); reservation lookup (:109-116); **lock trio**
  (:119-145): Npgsql `SELECT * FROM "Tickets" WHERE "ReservationId" = {id} FOR UPDATE`
  (:124-129) / SQLite no-op `UPDATE "Tickets" SET "CreatedAt" = "CreatedAt"` (:130-140) /
  InMemory plain read (:141-145); **re-check under lock**: `tickets.Any(t => t.IsUsed)`
  → block (:149-153); `tickets.Any(t => t.IsRefunded)` → block (:155-159); existing
  Refunded tx → block (:161-167); Approved tx lookup (:170-176); **FLIP** Approved →
  Refunded + UpdatedAt (:178-179); mark ALL tickets IsRefunded/RefundedAt (:181-185);
  SaveChanges + commit (:187-188).

**Contracts** (`backend/Services/IAdminPurchaseService.cs`):
- `AdminPurchasesResponse(EventId, EventName, IReadOnlyList<AdminPurchaseRow> Purchases, decimal TotalRefunded)` (:39-43).
- `AdminPurchaseRow(ReservationId, PurchaserEmail, PurchaserDni, TicketType, Quantity, Amount, PurchasedAt, bool Refunded, bool LinkUnverified)` (:52-61) — 9 positional params.

**Models** (`backend/Models/`):
- `Transaction` — Id, ReservationId, MercadoPagoId, Amount, Status, CreatedAt, UpdatedAt.
  **UNIQUE index on MercadoPagoId** (`ApplicationDbContext.cs:151`;
  migration `20260715190343_UniqueTransactionMercadoPagoId`). Admin refunds FLIP the
  existing Approved row — never insert a second row (index preserved).
- `TransactionStatus` — `Pending|Approved|Rejected|Refunded` (:1-9). No new enum value
  needed: partial refund keeps the tx `Approved` while active tickets remain.
- `Reservation` — **single `TicketTypeId`** (:8, one type per reservation, fungible
  tickets), `Quantity` (:9), PurchaserDNI/PurchaserEmail, `Status`
  (`Active|Expired|Confirmed|Cancelled`), CreatedAt.
- `Ticket` — `ReservationId (Guid?)` (:8, APR-009), IsUsed/UsedAt (:12-13),
  IsRefunded/RefundedAt (:14-15), unique QRCodeData.

**Controller** (`backend/Controllers/AdminController.cs`):
- Class-level `[Authorize(Policy = "RequireAdminRole")]` (:14) — covers both endpoints.
- `GET events/{eventId}/purchases` (:234-254) → 200/404/500.
- `POST events/{eventId}/purchases/{reservationId}/refund` (:265-298) → 200/404 (KeyNotFound)
  /409 (InvalidOperationException)/500; **audit AFTER commit** via `TryLogAuditAsync`
  (APR-007, best-effort, no motivo, :275-280); `Truncate` helper (:389).

**Migration conventions** (`backend/Migrations/`):
- Manual migrations, auto-timestamped names (`YYYYMMDDHHMMSS_Name`); **Designer file is
  MANDATORY** — a missing Designer silently hides the migration from EF (memory #442:
  `20260810120000_AddTicketReservationAndRefund` had to be regenerated).
- Backfill runs inside `Up()` inside try/catch with **positional `{0}`** placeholders in
  `WriteLine` — named placeholders throw FormatException inside the catch (memory #442).
- Precedent: `20260810120000_AddTicketReservationAndRefund.cs` (nullable Ticket→Reservation
  FK + refund flags + best-effort chunked backfill, Restrict delete, :39-68) and
  `20260814003427_AddEventApproval.cs` (NOT NULL column with default + best-effort backfill,
  :19-43). **KEY GOTCHA (memory #442)**: backfill code inside `Up()` executes during SQL
  GENERATION, before DDL applies → on first apply it fails (column not visible) and is
  swallowed by the catch. For the Refunds table the backfill must be **pure SQL**
  (`INSERT ... SELECT`) in the same migration, not EF-context backfill.

**Deploy state (CORRECTED)**: `dotnet ef migrations list` (run read-only, ASPNETCORE_ENVIRONMENT=Development)
shows **ALL 15 migrations applied — zero pending**. The prompt's premise ("misaligned
history missing AddPendingEmailSend / DropCurrentlyReserved") is STALE: memory #442
(2026-08-10) records the realignment (was 3 behind; fixed the AddPendingEmailSend raw-SQL
index quoting, regenerated the missing Designer, fixed the `{Reason}`→`{0}` backfill bug),
and memory #482 (2026-08-14) confirms `20260814003427_AddEventApproval` applied to Supabase
dev. A new `Refunds` migration will apply cleanly on top.

**Audit** (`backend/Models/AuditLog.cs`): `AuditActionType.RefundPurchase` exists (:84,
varchar-stored, NO migration needed — ATS-005 precedent). New refund ops reuse it.

## Affected Areas

### Backend — model, migration, service
- `backend/Models/Refund.cs` (NEW) — Id, ReservationId, `TicketIds (Guid[])`, Quantity,
  Amount, `AdminId (Guid?)` (nullable for legacy backfill rows), CreatedAt. Navigation →
  Reservation (Restrict or Cascade — match PendingEmailSend Cascade precedent or keep
  Restrict like Ticket; spec decision).
- `backend/Data/ApplicationDbContext.cs` — `DbSet<Refund> Refunds` + OnModelCreating block.
  `TicketIds` uses `HasColumnType("uuid[]")` — **precedent exists**: `PendingEmailSend.TicketIds`
  (`ApplicationDbContext.cs:200`). Note: `uuid[]` is PG-specific; SQLite test contexts
  must not exercise the column (PendingEmailSend proves the pattern works).
- New migration `AddRefundsTable` (e.g. `20260814xxxxxx_AddRefunds`) — CreateTable +
  **pure-SQL backfill**: `INSERT INTO "Refunds" (...,"TicketIds",...) SELECT ... FROM
  "Transactions" WHERE "Status" = <Refunded> LEFT JOIN "Tickets" ... array_agg` for every
  pre-existing Refunded transaction, so legacy full refunds keep counting in TotalRefunded.
- `backend/Services/AdminPurchaseService.cs` — signature change to
  `RefundPurchaseAsync(Guid reservationId, int quantity, Guid adminId)`:
  - Same lock trio (:119-145), keep IsUsed re-check under lock (:149-153) — policy
    unchanged (ANY used → whole refund blocked).
  - Replace the "any ticket already refunded → block" (:155-159) and "existing Refunded tx
    → block" (:161-167) guards with a quantity check under lock:
    `activeRemaining = tickets.Count(t => !t.IsRefunded)`; block if `quantity ≤ 0` or
    `quantity > activeRemaining` or no Approved tx exists.
  - **Deterministic selection**: refund the K oldest non-refunded, non-used tickets
    (`tickets.Where(t => !t.IsRefunded && !t.IsUsed).OrderBy(t => t.CreatedAt).Take(quantity)`).
  - Insert `Refund` row (TicketIds = selected ticket ids, Quantity = K, Amount = unit
    price × K — unit price from Reservation.TicketType.Price or tx.Amount / Reservation.Quantity;
    spec decision), mark those tickets IsRefunded/RefundedAt.
  - **Flip tx to Refunded ONLY when 0 active tickets remain** (preserves invariant
    "Refunded tx == fully refunded purchase" and existing consumers).
  - `GetPurchasesAsync` — row gains RefundedQuantity (count of IsRefunded tickets) +
    RefundedAmount (Σ Refunds for the reservation); `TotalRefunded = Σ Refunds.Amount`
    (was Σ Refunded tx amounts, :89-91); `Refunded` bool becomes derived
    (`refundedQuantity >= quantity` — i.e., fully refunded).
- `backend/Services/IAdminPurchaseService.cs` — `AdminPurchaseRow` gains
  `RefundedQuantity` + `RefundedAmount` (constructor-positional-record → **breaks all 9-arg
  constructions**: tests at `AdminControllerPurchaseTests.cs:117`).

### Backend — controller
- `backend/Controllers/AdminController.cs` — `RefundPurchase` endpoint gains a body DTO
  (`RefundPurchaseRequest(int Quantity)`, validated `> 0`); passes quantity to the service;
  audit detail unchanged (no motivo). Ripples: constructor-based controller tests
  (`AdminControllerPurchaseTests.cs:32-44`, `AdminControllerTests.cs:41`,
  `AdminControllerTicketStockTests.cs:35`) only change if the controller constructor changes
  (it should NOT — only the action signature/body).

### Backend — ripple consumers (verified NO change needed)
- `MetricsService.CalculateMetricsAsync` (`MetricsService.cs:162-176`) and
  `GetOrganizerMetricsAsync` (:74-89) — APR-005 filters `!t.IsRefunded` per-ticket. Partial
  refund marks individual tickets → sold/revenue correct automatically. **No change.**
- `EventService.ComputeAvailabilityAggregatesAsync` — `!t.IsRefunded` at
  `EventService.cs:268`. **No change.**
- `ReservationService.CreateReservationTransactionalAsync` — `!t.IsRefunded` at
  `ReservationService.cs:168-169`. **No change.**
- `TicketService.ValidateQRCodeAsync` — per-ticket IsRefunded check
  (`TicketService.cs:361-373`, "Entrada reembolsada"). Partial refund → only refunded
  tickets rejected at scan; the active K still validate. **No change.**
- `TicketService` lookups/resend (`LookupTicketsAsync`, `LookupTicketsByEmailAsync`,
  `LookupActiveTicketsByEmailAndDniAsync`, `ResendTicketsByEmailAsync`, APR-005) — all
  per-ticket `!IsRefunded`. **No change.**
- `EventService.UpdateEventAsync` date-change buyer query — `!t.IsRefunded` (:513-517). **No change.**
- `PaymentService.InitiateRefundAsync` (**OWNER EXCLUSION — do NOT touch**) — only
  reachable from the stock-failure path (`PaymentService.cs:354` inside
  ProcessApprovedPaymentAsync). Inserts a NEW Refunded tx row with the same MercadoPagoId —
  safe only because no Approved tx exists there (unique index). The new `Refunds` table is
  a separate concept (admin manual ops) — **no conflict, no change.**
- `AuditActionType.RefundPurchase` — varchar-stored, reused as-is. **No change.**

### Frontend — `frontend/src/pages/AdminPurchases.jsx` + test
- Row rendering (:165-199): badge from `purchase.refunded` (:166) → becomes
  "X de Y reembolsadas" using RefundedQuantity/Quantity (badge variant: error when fully
  refunded, warning when partial); button `disabled={purchase.refunded}` (:189) →
  `disabled={purchase.refundedQuantity >= purchase.quantity}`.
- Dialog `RefundConfirmationDialog` (:30-68) — add a numeric quantity selector (1..active
  remaining), live amount preview (unit price = amount / quantity — tickets fungible),
  POST body `{ quantity }`.
- Mutation (:88-99) — `mutationFn` posts `{ quantity }`; invalidate unchanged (:97).
- Tests (`AdminPurchases.test.jsx`) — mock shape needs `refundedQuantity` (+ partial-refund
  case), dialog interaction posts body, badge assertions change.

## Approaches

1. **New `Refunds` operation table + quantity-based cumulative refund (the design
   direction to validate)** — `Refund` entity (ReservationId, TicketIds[], Quantity,
   Amount, AdminId?, CreatedAt); `RefundPurchaseAsync(reservationId, quantity, adminId)`
   reuses the lock trio with under-lock re-checks; flip tx only when 0 active remain;
   `AdminPurchaseRow` gains RefundedQuantity/RefundedAmount; TotalRefunded = Σ Refunds.
   - Pros: cumulative ops preserved (1 today, 1 next week); audit trail of each op
     (admin, timestamp, amount, which tickets); tx invariant "Refunded == fully refunded"
     preserved → existing consumers/tests of the invariant stay coherent; totalRefunded
     becomes exact (per-op amounts).
   - Cons: new table + migration + **pure-SQL backfill** for legacy Refunded transactions
     (otherwise TotalRefunded silently drops to 0 for old refunds); 2 tests asserting
     binary refund semantics must be replaced; controller signature change + DTO.
   - Effort: Medium-High.

2. **Derive partial refunds without a new table** — keep flip-only, compute
   refundedAmount = tx.Amount × (refundedCount/quantity) at read time.
   - Pros: no migration, no backfill.
   - Cons: loses per-operation record (AdminId, CreatedAt per op, which TicketIds);
     violates the LOCKED decision ("REQUIRES a new Refunds table"); amount derivation is
     lossy if ticket prices change; no audit trail per operation. **Rejected by the locked
     decision** — documented for completeness only.

3. **Per-ticket refund endpoint (ticketId list in body)** — admin picks specific tickets.
   - Pros: explicit control.
   - Cons: contradicts locked decision #1 (selection by quantity — tickets fungible);
     more complex UI; same table required anyway. Rejected.

## Recommendation

**Approach 1** — the proposed design direction is CONFIRMED, with three refinements:
(a) **pure-SQL backfill** of Refunds rows for every pre-existing Refunded transaction in
the migration (EF-context backfill inside `Up()` provably fails on first apply — memory
#442); (b) **deterministic ticket selection** — K oldest non-refunded/non-used tickets
under lock; (c) **controller takes a `quantity` body DTO** and the service signature
becomes `RefundPurchaseAsync(reservationId, quantity, adminId)`.
Flip Approved→Refunded ONLY when the operation leaves zero active tickets — this preserves
the existing invariant and all consumers that rely on "Refunded tx == fully refunded".
`AdminPurchaseRow` gains `RefundedQuantity` + `RefundedAmount`; `TotalRefunded` = Σ
Refunds.Amount. Frontend adds the quantity selector with live amount preview and a
"X de Y reembolsadas" row state. No change to MetricsService / EventService /
ReservationService / TicketService / PaymentService / AuditActionType (all verified
per-ticket or already neutral).

## Risks

- **TotalRefunded regression for legacy refunds (HIGH)**: pre-existing Refunded
  transactions have no Refunds rows. Without the pure-SQL backfill, TotalRefunded (and
  per-row RefundedAmount) drop to 0 for old refunds. Backfill must be INSERT…SELECT inside
  the same migration; AdminId unknown → nullable column.
- **Test breakage from binary-refund assumptions (MEDIUM-HIGH)**:
  `AdminPurchaseServiceTests.RefundPurchaseAsync_HappyPath_...` (:143, asserts ALL tickets
  refunded + tx flipped) and `..._AlreadyRefunded_...` (:217, binary already-refunded state)
  encode full-refund semantics; `GetPurchasesAsync_HappyPath...TotalRefunded` (:246, :284)
  assert TotalRefunded = Σ Refunded tx amounts; `AdminControllerPurchaseTests` constructs
  `AdminPurchaseRow` with 9 args (:117). All must be replaced/extended under strict TDD
  (Red → Green), alongside new tests: partial happy path, cumulative second refund, quantity
  > active remaining blocked, quantity ≤ 0 blocked, flip only when 0 active remain, scan
  race with partial state, Refunds row recorded with TicketIds/Amount, legacy backfill.
- **Concurrency (MEDIUM)**: two concurrent partial refunds — the ticket FOR UPDATE lock
  serializes them; the second re-check observes the first's committed refunds under the
  lock and only K remaining can be refunded. The quantity guard MUST run on the LOCKED
  ticket list. Needs a dedicated test (InMemory has no locking — test serialization logic
  via the existing InMemory path; the FOR UPDATE arm is Npgsql-only, covered by pattern
  precedent, not integration tests).
- **`uuid[]` on SQLite (LOW-MEDIUM)**: Refund.TicketIds `uuid[]` is PG-specific; SQLite
  test contexts must not query that column. Precedent: PendingEmailSend.TicketIds works
  today (`ApplicationDbContext.cs:200`). Keep Refund mapping PG-only.
- **Stale premise corrected (INFO)**: migration history is 15/15 applied — a new migration
  applies cleanly; no unblocking work needed. `appsettings.json` uses placeholder creds;
  Development config points at the real shared Supabase.
- **Config drift (INFO)**: `openspec/config.yaml` says "frontend has NO test runner" but
  vitest exists (`frontend/vite.config.js`, `AdminPurchases.test.jsx`, ~400 tests). The
  react-testing skill documents vitest conventions; frontend tests MUST be updated in
  apply. Baseline: backend ~527 pass / 6 pre-existing failures; frontend ~400 pass / 26
  pre-existing failures (do not fix, report only).
- **400-line review budget (MEDIUM)**: estimated change (migration + entity + service
  refactor + DTO + record fields + frontend dialog + ~20 test updates/additions) likely
  600-900 lines → the tasks phase should forecast chained PRs or an accepted size exception.

## Ready for Proposal

**Yes.** Orchestrator should launch `sdd-propose` with: quantity-based selection (locked),
cumulative Refunds table (locked), used-ticket policy unchanged (locked), no motivo
(APR-008), RefundPurchaseAsync signature change with quantity DTO, flip-only-when-zero-active,
Refunds pure-SQL backfill, deterministic ticket selection, AdminPurchaseRow
RefundedQuantity/RefundedAmount, frontend quantity selector + "X de Y reembolsadas",
migration history confirmed aligned, and strict-TDD test plan (replace 4-5 binary-refund
tests, add ~10 partial-refund tests).
