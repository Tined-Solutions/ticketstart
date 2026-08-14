# Design: Admin partial refunds (per-quantity, cumulative)

> Change `admin-partial-refunds`. Hybrid artifact (this file + Engram `sdd/admin-partial-refunds/design`).
> Builds on the archived `2026-08-10-admin-event-refunds` design. Strict TDD active.
> Line numbers are current on-disk (Aug 2026). Apply phase gets this as an exact blueprint.

## Technical Approach

Refund becomes partial + cumulative. A new `Refunds` ledger table records exactly one
immutable row per refund **operation** (ReservationId, the K refunded TicketIds, Quantity,
Amount, AdminId, CreatedAt). `RefundPurchaseAsync(reservationId, quantity, adminId)` reuses
the existing three-provider lock trio (`AdminPurchaseService.cs:119-145`) and adds an
under-lock quantity guard: refund the K **oldest** non-refunded, non-used tickets
(`OrderBy(CreatedAt).Take(K)`, APR-013), insert one `Refunds` row, and flip the Approved
`Transaction` to Refunded **only when 0 active tickets remain** (preserves the
"Refunded tx == fully refunded" invariant + every per-ticket consumer). `GetPurchasesAsync`
projects `RefundedQuantity`/`RefundedAmount` group-wise (no N+1); `TotalRefunded = Σ Refunds.Amount`.
Migration `AddRefundsTable` creates the table and **pure-SQL `INSERT…SELECT` backfills**
one `Refunds` row per legacy Refunded Transaction (`AdminId NULL`) — EF-context backfill in
`Up()` provably fails on first apply (memory #442). Controller takes a `RefundPurchaseRequest`
body; audit after commit (APR-007), no motivo/MP (APR-008). No ripple: all verified per-ticket.

## Architecture Decisions

| # | Decision | Choice (REJECTED in italics) | Rationale |
|---|----------|------------------------------|-----------|
| D1 | Refund accounting | New immutable `Refunds` ledger table. *REJECTED: derive amount = tx.Amount × refundedCount/Quantity at read time.* | Locked business decision: needs per-op audit (admin/timestamp/which TicketIds/amount) + exact cumulative total. Derivation loses the operation records and is lossy. |
| D2 | Flip invariant | Flip Approved→Refunded **only at 0 active tickets**; partial ops leave tx Approved. *REJECTED: flip on every partial op.* | Preserves "Refunded tx == fully refunded" so MetricsService/EventService/ReservationService/Ticket/Payment consumers + tests stay coherent with zero changes. |
| D3 | Ticket selection | K oldest non-refunded/non-used `OrderBy(CreatedAt).Take(K)` under row lock. *REJECTED: random / newest / unsorted.* | Stable, replayable, auditable; deterministic under concurrent ops; oldest-first matches refund intent. |
| D4 | Legacy backfill | **Pure SQL** `INSERT…SELECT` with `array_agg` in `AddRefundsTable`. *REJECTED: EF-context backfill inside Up() (memory #442) — runs at SQL-generation time before DDL applies, swallowed by catch, never backfills.* | One `Refunds` row per pre-existing Refunded tx keeps `TotalRefunded` from regressing to 0 for old refunds (HIGH risk). Atomic with the CreateTable (PG DDL transactions); no try/catch — fail loudly. |
| D5 | `TicketIds` storage | `Guid[]` + `[Column(TypeName="uuid[]")]` + `HasColumnType("uuid[]")` (PG-only). *REJECTED: separate `RefundTickets` join table.* | `PendingEmailSend.TicketIds` precedent (`ApplicationDbContext.cs:200`); array is an immutable snapshot fitting an operation record (a join table implies mutability this record forbids). PG-only; InMemory ignores column type. |
| D6 | Delete behavior | **Restrict** Refund→Reservation. *REJECTED: Cascade (PendingEmailSend style).* | Refunds are a permanent audit ledger; Restrict blocks Reservation deletion that would orphan the row and silently drop `TotalRefunded`. Matches Ticket→Reservation Restrict (history-preservation); PendingEmailSend Cascade fits its transient retry queue, not a ledger. |
| D7 | Unit-price source | `Amount = reservation.TicketType.Price × K`. *REJECTED: tx.Amount / reservation.Quantity × K.* | `.Price` is the canonical stable unit price (decimal(18,2) exact × int = exact). tx-ratio risks decimal rounding when not divisible; in this system `Price == tx.Amount/Quantity` always (verified: purchase creation + seed). |
| D8 | DTO validation style | Plain record `RefundPurchaseRequest(int Quantity)`, **no data annotations**; service throws `InvalidOperationException` → 409 for `K ≤ 0`. *REJECTED: `[Range(1,…)]` annotation → auto 400.* | Keeps the refund-block error set uniform at 200/404/409/500 (unchanged mapping). Matches `AdminCreateUserRequest` positional-record convention (validation in service). APR-011 plans a controller body-validation test. |
| D9 | Quantity transport | Request **body** `{ quantity }`. *REJECTED: route param `/refund/{quantity}`.* | Quantity is an operation attribute (write), not a resource id; body DTO is extensible and non-cached. |
| D10 | Ripple consumers | No change to MetricsService / EventService / ReservationService / TicketService / PaymentService / AuditActionType. | All verified per-ticket `!IsRefunded` (explore §Ripple) + `AuditActionType.RefundPurchase` already exists (varchar-stored, no migration). `PaymentService.InitiateRefundAsync` is an OWNER-EXCLUSION (stock-failure path, inserts a distinct Refunded tx, no conflict). |

## Data Model

```csharp
// backend/Models/Refund.cs  (NEW)
public class Refund
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    [Column(TypeName = "uuid[]")]
    public Guid[] TicketIds { get; set; } = Array.Empty<Guid>();   // D5: immutable snapshot
    public int Quantity { get; set; }          // K marked this op
    public decimal Amount { get; set; }         // unitPrice × K (D7)
    public Guid? AdminId { get; set; }          // NULL only for backfilled legacy rows (APR-014)
    public DateTime CreatedAt { get; set; }    // no UpdatedAt — immutable
    public Reservation Reservation { get; set; } = null!;
}
```

`ApplicationDbContext` OnModelCreating block (mirrors PendingEmailSend):
```csharp
modelBuilder.Entity<Refund>(entity => {
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.ReservationId);
    entity.Property(e => e.TicketIds).HasColumnType("uuid[]").IsRequired();
    entity.Property(e => e.Quantity).IsRequired();
    entity.Property(e => e.Amount).IsRequired().HasColumnType("decimal(18,2)");
    entity.Property(e => e.AdminId).IsRequired(false);
    entity.Property(e => e.CreatedAt).IsRequired();
    entity.HasOne(e => e.Reservation).WithMany()
          .HasForeignKey(e => e.ReservationId).OnDelete(DeleteBehavior.Restrict); // D6
});
// + public DbSet<Refund> Refunds { get; set; } = null!;
```

## Data Flow / Sequence Diagrams

### A) Partial refund (K < N active)

```
Admin │ AdminController │ AdminPurchaseService │ EF/Tickets │ Transactions │ Refunds
──────┼────────────────┼──────────────────────┼───────────┼──────────────┼────────
POST {quantity:2} ──→ RefundPurchaseAsync(rid,2,adminId)
                      ├ ExecutionStrategy.BeginTransaction
                      ├ load Reservation (.Include TicketType) → KeyNotFound? 404
                      ├ LOCK tickets trio (Npgsql FOR UPDATE / SQLite no-op / InMemory)
                      ├ re-check under lock:
                      │   Any IsUsed? ──yes──→ InvalidOperationException→409 (APR-004)
                      │   active = count(!IsRefunded && !IsUsed)
                      │   quantity≤0 || quantity>active? ──yes──→ 409 (APR-003)
                      │   Approved tx missing? ──yes──→ 409
                      ├ selected = tickets.Where(…).OrderBy(CreatedAt).Take(2)   (APR-013)
                      ├ mark selected IsRefunded/RefundedAt=now
                      ├ Amount = TicketType.Price × 2; insert Refund{TicketIds,Quantity=2,Amount,AdminId,CreatedAt} (APR-012)
                      ├ active(2)==quantity(2)? NO → tx stays Approved        (D2)
                      ├ SaveChanges + Commit ──→ Audit after commit (APR-007) ──→ 200
```
Outcome: 2 tickets refunded, **1 Refunds row**, tx stays **Approved**.

### B) Concurrent partial refunds (serialize under lock)

```
Req1 ─→ BEGIN ─→ LOCK tickets(FOR UPDATE) ─→ active=4 → refund K=2 ─→ commit ─→ (release)
                                                          │
Req2 ─→ BEGIN ─→ WAIT (blocked on same rows) ─Wake→ LOCK ─→ re-read active=2 (sees Req1's committed IsRefunded)
                                   → refund K=2 ─→ active==K → flip tx→Refunded ─→ commit
```
Req2's quantity guard runs **on the locked list** so it observes Req1's committed refunds;
no ticket is refunded twice. The FOR UPDATE arm is Npgsql-only (covered by the
EventService/ReservationService trio precedent — not integration-tested); the **serialization
logic** is tested via the InMemory path (sequential ops, second sees first's committed state).

## Migration `AddRefundsTable`

`{ts}` per `dotnet ef migrations add` (auto-names `YYYYMMDDHHMMSS_AddRefunds`); **Designer
.font.cs file is MANDATORY** — without it EF silently does not discover the migration
(memory #442). `dotnet ef migrations add` auto-generates both.

`Up()`:
1. `CreateTable` "Refunds" — Id uuid PK, ReservationId uuid NOT NULL, TicketIds uuid[] NOT NULL,
   Quantity int NOT NULL, Amount decimal(18,2) NOT NULL, AdminId uuid NULL, CreatedAt
   timestamptz NOT NULL.
2. `CreateIndex` IX_Refunds_ReservationId.
3. `AddForeignKey` FK_Refunds_Reservations_ReservationId → Reservations.Id `Restrict` (D6).
4. **Pure-SQL backfill** (NO EF-context, NO try/catch — atomic with the DDL, fail loudly):
```csharp
migrationBuilder.Sql(@"
INSERT INTO ""Refunds"" (""Id"",""ReservationId"",""TicketIds"",""Quantity"",""Amount"",""AdminId"",""CreatedAt"")
SELECT gen_random_uuid(),
       t.""ReservationId"",
       COALESCE(agg.""TicketIds"", ARRAY[]::uuid[]),
       COALESCE(agg.""Quantity"", 0),
       t.""Amount"",
       NULL,
       t.""UpdatedAt""
FROM ""Transactions"" t
LEFT JOIN (
  SELECT ""ReservationId"", array_agg(""Id"") AS ""TicketIds"", COUNT(*) AS ""Quantity""
  FROM ""Tickets""
  WHERE ""IsRefunded"" = TRUE AND ""ReservationId"" IS NOT NULL
  GROUP BY ""ReservationId""
) agg ON agg.""ReservationId"" = t.""ReservationId""
WHERE t.""Status"" = 3;  -- 3 == TransactionStatus.Refunded (no HasConversion → stored as int)");
```
`TransactionStatus` has no `HasConversion` (verified OnModelCreating) → Refunded=3. `gen_random_uuid()`
is native in PG13+ (Supabase is PG15). Orphan Refunded tx with no refunded tickets → COALESCE to
empty array + 0 Quantity (keeps `Amount`/TotalRefunded correct, AdminId NULL).

`Down()`: drop FK → drop index → drop table. Why EF-context backfill fails: code creating a context
inside `Up()` runs during SQL **generation**, before the DDL applies → "column does not exist",
swallowed by the existing catch → never backfills (memory #442). `migrationBuilder.Sql` emits ordered
DDL that runs at **apply** time after the table exists. **No `{0}` WriteLine gotcha** here — the
earlier migrations' catch+`WriteLine(format,arg)` needed positional `{0}`; this migration has no
catch and no WriteLine since it fails loudly. (If apply adds logging, use positional `{0}`.)

## Service refactor — `RefundPurchaseAsync(Guid reservationId, int quantity, Guid adminId)`

```csharp
var strategy = _context.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () => {
  using var tx = await _context.Database.BeginTransactionAsync();
  try {
    var reservation = await _context.Reservations
        .Include(r => r.TicketType)                 // D7 unit-price source
        .FirstOrDefaultAsync(r => r.Id == reservationId)
        ?? throw new KeyNotFoundException($"Reservation {reservationId} not found");
    // lock trio — UNCHANGED (:119-145): Npgsql FOR UPDATE / SQLite no-op / InMemory
    var tickets = await AcquireTicketLocksAsync(reservationId);   // helper wraps :119-145
    var now = DateTime.UtcNow;
    // guards under lock (NEW ORDER):
    if (tickets.Any(t => t.IsUsed))
        throw new InvalidOperationException("Cannot refund a purchase with used tickets");   // APR-004
    var active = tickets.Count(t => !t.IsRefunded && !t.IsUsed);
    if (quantity <= 0 || quantity > active)
        throw new InvalidOperationException($"Cannot refund {quantity} tickets; {active} active remaining"); // APR-003
    var approvedTx = await _context.Transactions
        .FirstOrDefaultAsync(t => t.ReservationId == reservationId && t.Status == TransactionStatus.Approved)
        ?? throw new InvalidOperationException("No approved transaction found for this purchase"); // APR-003
    // deterministic selection (APR-013):
    var selected = tickets.Where(t => !t.IsRefunded && !t.IsUsed)
                          .OrderBy(t => t.CreatedAt).Take(quantity).ToList();
    foreach (var t in selected) { t.IsRefunded = true; t.RefundedAt = now; }
    var unitPrice = reservation.TicketType.Price;                       // D7
    _context.Refunds.Add(new Refund {
        ReservationId = reservationId,
        TicketIds = selected.Select(t => t.Id).ToArray(),
        Quantity = quantity,
        Amount = unitPrice * quantity,
        AdminId = adminId,
        CreatedAt = now });
    if (active == quantity) { approvedTx.Status = TransactionStatus.Refunded; approvedTx.UpdatedAt = now; }  // D2 flip-only-at-zero
    await _context.SaveChangesAsync(); await tx.CommitAsync();
    _logger.LogInformation("Refunded {K}/{Active} tickets of {Rid}; tx {Flip} by {AdminId}",
        quantity, active, reservationId, active == quantity ? "flipped" : "kept-Approved", adminId);
  } catch { await tx.RollbackAsync(); throw; }
});
```
Replaces the old binary guards (`anyRefunded` :155-159, `existingRefundedTx` :161-167) with the
single quantity guard. Old "mark ALL tickets" loop (:181-185) → mark `selected` only.

## `GetPurchasesAsync` projection changes

Same confirmed-reservations + Approved/Refunded transactions + APR-009 `linkedTicketCounts` (unchanged).
**Add** two group queries (no N+1, parallel structure to `linkedTicketCounts`):
```csharp
var refundedTicketCounts = reservationIds.Count == 0 ? new Dictionary<Guid,int>() :
    await _context.Tickets.AsNoTracking()
        .Where(t => t.ReservationId != null && reservationIds.Contains(t.ReservationId.Value) && t.IsRefunded)
        .GroupBy(t => t.ReservationId!.Value)
        .Select(g => new { g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.Key, x => x.Count);            // APR-012 RefundedQuantity
var refundsByRes = reservationIds.Count == 0 ? new Dictionary<Guid,decimal>() :
    await _context.Refunds.AsNoTracking()
        .Where(r => reservationIds.Contains(r.ReservationId))
        .GroupBy(r => r.ReservationId)
        .Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) })
        .ToDictionaryAsync(x => x.Key, x => x.Sum);              // APR-012 RefundedAmount
```
Row projection sets `RefundedQuantity = refundedTicketCounts.GetValueOrDefault(r.Id)`,
`RefundedAmount = refundsByRes.GetValueOrDefault(r.Id)`, `Refunded = refundedQuantity >= r.Quantity`.
`TotalRefunded = refundsByRes.Values.Sum()` (was `Σ Refunded tx amounts` :89-91). `linkedTicketCounts`
(APR-009) and `PurchasedAt`/`Amount` from tx stay unchanged.

## Controller / API

```csharp
// AdminController.RefundPurchase — signature gains [FromBody]
[HttpPost("events/{eventId:guid}/purchases/{reservationId:guid}/refund")]
public async Task<IActionResult> RefundPurchase(Guid eventId, Guid reservationId,
    [FromBody] RefundPurchaseRequest request)        // null/missing body → 400 auto ([ApiController])
{
    if (!TryGetUserId(out var adminId)) return Unauthorized();
    try {
        await _adminPurchaseService.RefundPurchaseAsync(reservationId, request.Quantity, adminId);  // 3-arg
        await TryLogAuditAsync(adminId, new AuditLogContext(adminId,
            AuditActionType.RefundPurchase, AuditResourceType.Payment, reservationId,
            Truncate($"Admin refunded {request.Quantity} tickets of purchase {reservationId} for event {eventId}", 1000)));
        return Ok(new { message = "Purchase refunded successfully" });
    }
    catch (KeyNotFoundException) { return NotFound(new { error = "Reservation not found" }); }   // 404 unchanged
    catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }        // 409 unchanged (incl. K≤0, K>active, IsUsed, no-Approved)
    catch (Exception ex) { _logger.LogError(ex, "…"); return StatusCode(500, ...); }             // 500 unchanged
}
// near other request records (AdminCreateUserRequest, RejectEventRequest):
public record RefundPurchaseRequest(int Quantity);   // NO annotations (D8)
```
Error mapping 200/404/409/500 unchanged; `K≤0` and `K>active` route through the service as
`InvalidOperationException` → 409 (uniform). Audit unchanged: after commit, no motivo (APR-007/008).

## Frontend — `AdminPurchases.jsx`

Mock row adds `refundedQuantity`, `refundedAmount` (e.g. res-1: qty 2, `refundedQuantity:0`; res-2: qty 1,
`refundedQuantity:1`, `refundedAmount:150`). Replace `statusBadge(refunded)`:
```js
function refundBadge(qty, refundedQty) {
  if (refundedQty === 0) return { variant: 'success', label: 'Confirmada' }
  const de = refundedQty >= qty
    ? { variant: 'error', label: 'Reembolsada' }         // fully → rose
    : { variant: 'warning', label: 'Reembolsada' }         // partial → amber
  return { ...de, label: `${refundedQty} de ${qty} reembolsadas` } // APR-010
}
```
Row uses `const badge = refundBadge(purchase.quantity, purchase.refundedQuantity)`; button
`disabled={purchase.refundedQuantity >= purchase.quantity}` (was `disabled={purchase.refunded}`).
`RefundConfirmationDialog` gains a local `useState(selectedQuantity)` (default 1):
`activeRemaining = purchase.quantity - purchase.refundedQuantity`; selector 1..activeRemaining
(`type="number"` input `min=1 max={activeRemaining}` or ± stepper). Live preview:
`unitPrice = purchase.amount / purchase.quantity`; preview text
`Reembolsar {selectedQuantity} × {formatCurrency(unitPrice)} = {formatCurrency(unitPrice*selectedQuantity)}`.
Mutation takes a body: `apiClient.post(url, { quantity: selectedQuantity })`;
`handleConfirmRefund` passes `selectedQuantity` from dialog state. Query invalidation unchanged (`:97`).
Pattern follows repo: default-export page component, `useDialog`/`Button`/`Badge`/`formatCurrency`,
TanStack `useMutation`+`useQueryClient` (`react-patterns` skill).

## Testing Strategy (strict TDD — Red→Green map)

| Spec scenario | Test (file) | Red→Green focus |
|---|---|---|
| APR-003 partial happy | `RefundPurchaseAsync_Partial_MarksTwoTicketsAndLeavesTxApproved` (AdminPurchaseServiceTests) | replaces binary HappyPath :143; exactly 2 of 4 `IsRefunded`, tx stays Approved |
| APR-003 full-at-zero flips | `RefundPurchaseAsync_FullAtZeroActive_FlipsTransaction` (AdminPurchaseServiceTests) | replaces binary flip; flip only when active==quantity, one tx row remains |
| APR-012 op row + amount | `RefundPurchaseAsync_InsertsRefundRow_WithTicketIdsQuantityUnitPriceAmountAdminId` (AdminPurchaseServiceTests) | one Refunds row; TicketIds = selected; Amount = Price×K |
| APR-012 cumulative | `RefundPurchaseAsync_Cumulative_SecondRefundAppendsAndFlipsAtZero` (AdminPurchaseServiceTests) | second refund appends 2nd Refunds row; TotalRefunded = Σ |
| APR-003 K>active blocked | `RefundPurchaseAsync_QuantityAboveActiveRemaining_ThrowsNoChange` (AdminPurchaseServiceTests) | replaces AlreadyRefunded :217; nothing mutated |
| APR-003 K≤0 blocked | `RefundPurchaseAsync_QuantityZeroOrNegative_ThrowsNoChange` (AdminPurchaseServiceTests) | service InvalidOperationException (409 path); no state change |
| APR-013 oldest selection | `RefundPurchaseAsync_SelectsOldestTickets_ByCreatedAt` (AdminPurchaseServiceTests) | distinct CreatedAt → earliest K marked |
| APR-003 concurrent serialize | `RefundPurchaseAsync_ConcurrentQuantityGuard_SecondSeesFirstCommittedState` (AdminPurchaseServiceTests) | InMemory sequential; 2nd active reflects 1st's commit; no double-refund. FOR UPDATE arm = Npgsql-only, covered by trio precedent (not integration-tested). |
| APR-003 no Approved | `RefundPurchaseAsync_NoApprovedTransaction_ThrowsNoChange` (AdminPurchaseServiceTests) | keep/extend :167 with quantity param |
| APR-004 used blocks all | `RefundPurchaseAsync_UsedTicket_ThrowsNoChange` (AdminPurchaseServiceTests) | keep :184; ANY used blocks whole K |
| APR-002 listing partial+full | `GetPurchasesAsync_PartialAndFullRefunded_ReturnsRefundedQuantityRefundedAmountAndDerivedFlag` (AdminPurchaseServiceTests) | replaces :246; RefundedQuantity/RefundedAmount/derived Refunded |
| APR-002 TotalRefunded | `GetPurchasesAsync_TotalRefunded_SumOfRefundsAmount` (AdminPurchaseServiceTests) | replaces :284; Σ Refunds.Amount across reservations |
| APR-014 legacy keeps counting | (a) `AddRefundsTable_BackfillContainsPureSqlInsertSelect`×(structural — asserts migration + Designer exist; one-off, not InMemory-capable) (b) `GetPurchasesAsync_LegacyRefundWithBackfilledRow_KeepsCountingTotalRefunded` (AdminPurchaseServiceTests) seeds a `Refund{AdminId=null}` + Refunded tx → TotalRefunded includes Amount (simulates post-backfill) | migration apply is a manual PG step (like `VerifyDatabaseSchema`'s live-Supabase mode); reads-side asserts no regression |
| APR-007/008 controller body | `RefundPurchase_Success_PassesQuantityBodyAndWritesAuditWithoutMotivo` (AdminControllerPurchaseTests) | replaces :157; `_mockPurchaseService.Verify(…RefundPurchaseAsync(resId, qty, adminId))`; audit RefundPurchase/Payment no motivo |
| APR-003 controller 409 K≤0 | `RefundPurchase_InvalidQuantity_ReturnsConflict` (AdminControllerPurchaseTests) | mock service throws InvalidOperationException → 409, no audit |
| 9-arg ctor ripple | update `GetPurchases_HappyPath…` build of `AdminPurchaseRow` to 11 args (add `RefundedQuantity,RefundedAmount` after `PurchasedAt`) + all 3-arg `RefundPurchaseAsync` mocks | AdminControllerPurchaseTests :117 + existing controller test setups |
| APR-010 frontend badge | `renders X de Y reembolsadas badge variants` (AdminPurchases.test.jsx) | res-2 (1/1) → error/"1 de 1 reembolsadas"; res-1 (0/2) → success/"Confirmada" |
| APR-010 partial selector | `partial refund via quantity selector posts {quantity}` (AdminPurchases.test.jsx) | dialog shows preview; select K; mockPost called with `{ quantity: K }`; row → "K de N reembolsadas" |
| APR-010 disabled when full | `fully refunded row disables refund button` (AdminPurchases.test.jsx) | refundedQuantity>=quantity → button disabled |
| APR-010 failure | `refund failure shows error and leaves list unchanged` (AdminPurchases.test.jsx) | update existing test; assert error alert, no refetch |
| Frontend mock shape | update `mockPurchases` rows to include `refundedQuantity`,`refundedAmount`; refund success test posts body + asserts updated `refundedQuantity` | success invalidation test |

Notes: `uuid[]` (`TicketIds`) is PG-only — never read via raw SQL on SQLite; InMemory stores the CLR
`Guid[]` natively (the only raw SQL read is the ticket `FOR UPDATE` on the Tickets table, not Refunds;
Refunds is pure LINQ → safe on all providers). Backend `dotnet test`, frontend `npx vitest run`.

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or
process-integration boundary. Refund is one in-process EF Core transaction; the only external side
effect (audit) is best-effort log. The Npgsql `FOR UPDATE` lock is a DB row lock, not a shell/process
boundary (covered by the existing trio precedent and the test plan above).

## Migration / Rollout

One migration `AddRefundsTable` (table + pure-SQL backfill, atomic, no try/catch). No feature flag
(Admin-only surface). Apply manually: `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update
--context TicketeraOnline.Api.Data.ApplicationDbContext` (migrations are manual per efcore-data skill;
history is 15/15 applied, applies cleanly on top). Rollback per proposal: `dotnet ef migrations remove`
before deploy OR drop the `Refunds` table post-deploy (additive, no FK consumer blocks revert) and
revert the service/DTO/row/UI to binary — `IsRefunded` flags stay (history preserved, no data loss);
audit rows kept.

## Open Questions

None — all decisions resolved above (delete behavior D6, unit-price source D7, DTO validation style D8,
badge variants APR-010, positional-record field order, fail-loud backfill). The `delivery_strategy =
single-pr` + `size:exception` (budget 4000 lines) is owned by the orchestrator/tasks phase, not design.

## Key Learnings

1. Migration backfill inside `Up()` via an EF DbContext executes during SQL generation before DDL applies and fails on first run.
2. `TransactionStatus` has no `HasConversion`, so it is stored as an int and the legacy backfill filters `WHERE "Status" = 3` for Refunded.
3. Flipping the Approved Transaction to Refunded only at zero active tickets preserves every per-ticket consumer without code change.