# Tasks: Admin partial refunds (per-quantity, cumulative)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~900 (range 750–1,000) |
| 400-line budget risk | High (change exceeds default 400; effective risk **Low** vs pre-approved 4000-line size:exception) |
| Chained PRs recommended | No |
| Suggested split | Single PR (size:exception ≤ 4000 pre-approved) |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

```text
Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High
```

Note: `strict-tdd.md` does not exist on disk; strict TDD is enforced via `openspec/config.yaml` (`testing.strict_tdd: true`) + `dotnet-testing` skill. Every backend task below is RED-first.

| Phase | Est. lines | Tasks | Focus |
|-------|-----------|-------|-------|
| 1 Model + Migration | ~200 | 4 | Refund entity + DbSet + `AddRefundsTable` pure-SQL backfill (APR-014) |
| 2 Service | ~400 | 3 | 11-arg row, 3-arg refund, group queries, flip-at-zero (APR-002/003/012/013) |
| 3 Controller | ~110 | 2 | `RefundPurchaseRequest` body + audit with quantity (APR-003/007/008) |
| 4 Frontend | ~210 | 2 | Badge + quantity selector + `{quantity}` mutation (APR-010) |
| 5 Verification | 0 | 3 | Suites green + migration apply + rollback (APR-011/015) |
| **Total** | **~900** | **14** | |

## Phase 1: Model + Migration (APR-012/013/014) — Foundation

- [x] 1.1 **RED: structural backfill test** — `backend/Tests/` new `AddRefundsTable_BackfillContainsPureSqlInsertSelect`: asserts `{ts}_AddRefunds.cs` + `.Designer.cs` exist and `Up()` contains the `INSERT…SELECT` backfill (no EF-context, no try/catch). Fails before migration exists. Verify: `dotnet test` (fails).
- [x] 1.2 **GREEN: `backend/Models/Refund.cs` (NEW)** — per D5/D6/D7: Id, ReservationId, `TicketIds Guid[]` `[Column(TypeName="uuid[]")]` = `Array.Empty<Guid>()`, Quantity, Amount decimal, `AdminId Guid?`, CreatedAt (NO UpdatedAt — immutable), Reservation nav. Verify: `dotnet build`.
- [x] 1.3 **GREEN: `backend/Data/ApplicationDbContext.cs`** — `DbSet<Refund> Refunds` + OnModelCreating block (mirror PendingEmailSend): PK Id; `HasIndex(ReservationId)`; TicketIds `HasColumnType("uuid[]").IsRequired()`; Quantity IsRequired; Amount IsRequired `.HasColumnType("decimal(18,2)")`; AdminId `IsRequired(false)`; CreatedAt IsRequired; `HasOne→Reservation.WithMany().HasForeignKey(ReservationId).OnDelete(DeleteBehavior.Restrict)` (D6). Verify: build.
- [x] 1.4 **GREEN: generate migration** — `dotnet ef migrations add AddRefunds` from `backend/` (auto `{ts}_AddRefunds` + MANDATORY Designer). Review `Up()`: CreateTable Refunds; CreateIndex IX_Refunds_ReservationId; AddForeignKey Restrict; then pure-SQL `migrationBuilder.Sql` backfill — `INSERT…SELECT gen_random_uuid(), t."ReservationId", COALESCE(agg."TicketIds", ARRAY[]::uuid[]), COALESCE(agg."Quantity",0), t."Amount", NULL, t."UpdatedAt" FROM "Transactions" t LEFT JOIN (array_agg over IsRefunded tickets) WHERE t."Status" = 3` (Refunded=3, no HasConversion). `Down()`: drop FK → index → table. Verify: 1.1 passes.

## Phase 2: Service (APR-002/003/012/013) — Core, strict TDD

- [x] 2.1 **RED: rewrite `backend/Tests/AdminPurchaseServiceTests.cs`** — replace :143/:217/:246/:284 with: `RefundPurchaseAsync_Partial_MarksTwoTicketsAndLeavesTxApproved`, `_FullAtZeroActive_FlipsTransaction` (one tx row per MercadoPagoId), `_InsertsRefundRow_WithTicketIdsQuantityUnitPriceAmountAdminId` (TicketIds=selected, Amount=Price×K), `_Cumulative_SecondRefundAppendsAndFlipsAtZero`, `_QuantityAboveActiveRemaining_ThrowsNoChange`, `_QuantityZeroOrNegative_ThrowsNoChange`, `_SelectsOldestTickets_ByCreatedAt` (distinct CreatedAt → earliest K), `_ConcurrentQuantityGuard_SecondSeesFirstCommittedState` (InMemory sequential), `_NoApprovedTransaction_ThrowsNoChange` + `_UsedTicket_ThrowsNoChange` (add quantity arg), `GetPurchasesAsync_PartialAndFullRefunded_ReturnsRefundedQuantityRefundedAmountAndDerivedFlag`, `_TotalRefunded_SumOfRefundsAmount`, `_LegacyRefundWithBackfilledRow_KeepsCountingTotalRefunded` (seed `Refund{AdminId=null}` + Refunded tx); update all 2-arg calls to 3-arg. Verify: `dotnet test` (fails/compile-fails).
- [x] 2.2 **GREEN: `backend/Services/IAdminPurchaseService.cs`** — `AdminPurchaseRow` → 11-arg (insert `RefundedQuantity, RefundedAmount` after PurchasedAt); `RefundPurchaseAsync(Guid reservationId, int quantity, Guid adminId)` 3-arg. Verify: build.
- [x] 2.3 **GREEN: `backend/Services/AdminPurchaseService.cs`** — `GetPurchasesAsync`: add `refundedTicketCounts` + `refundsByRes` group queries (`AsNoTracking`, no N+1), `Refunded = refundedQuantity >= r.Quantity`, `TotalRefunded = refundsByRes.Values.Sum()`. `RefundPurchaseAsync`: extract lock trio helper `AcquireTicketLocksAsync` (:119-145); under lock: any IsUsed → block (APR-004); `active = count(!IsRefunded && !IsUsed)`; `quantity <= 0 || quantity > active` → block (APR-003); Approved tx required; `selected = OrderBy(CreatedAt).Take(quantity)` (APR-013); mark selected `IsRefunded/RefundedAt=now`; insert Refund row (`Amount = TicketType.Price × K`, D7); flip ONLY when `active == quantity` (D2); REMOVE binary guards `anyRefunded`/`existingRefundedTx` + mark-all loop; log K/active/flip. Verify: `dotnet test` (2.1 passes).

## Phase 3: Controller (APR-003/007/008) — Integration

- [x] 3.1 **RED: `backend/Tests/AdminControllerPurchaseTests.cs`** — :117 9-arg row → 11-arg (`…, DateTime.UtcNow, 2, 200m, true, false`); 2-arg calls (:94, :166) → pass `new RefundPurchaseRequest(quantity)`; 2-arg mock verifies → 3-arg; new `RefundPurchase_Success_PassesQuantityBodyAndWritesAuditWithoutMotivo` (replaces :157 — verify `RefundPurchaseAsync(resId, qty, adminId)`; audit RefundPurchase/Payment, detail includes quantity, no motivo) + `RefundPurchase_InvalidQuantity_ReturnsConflict` (mock throws → 409, no audit). Verify: `dotnet test` (fails).
- [x] 3.2 **GREEN: `backend/Controllers/AdminController.cs`** — add `public record RefundPurchaseRequest(int Quantity)` near `AdminCreateUserRequest` (NO annotations — D8); `RefundPurchase` gains `[FromBody] RefundPurchaseRequest request` (missing body → 400 auto); 3-arg service call; audit detail `$"Admin refunded {request.Quantity} tickets of purchase {reservationId} for event {eventId}"`; error mapping 200/404/409/500 unchanged. Verify: `dotnet test` (3.1 passes).

## Phase 4: Frontend (APR-010) — Integration

- [x] 4.1 **RED: `frontend/src/pages/AdminPurchases.test.jsx`** — mock rows add `refundedQuantity`/`refundedAmount` (res-1: qty 2, refundedQuantity 0; res-2: qty 1, refundedQuantity 1, refundedAmount 150); badge variants ("1 de 1 reembolsadas" error / "Confirmada" success); partial refund via selector posts `{ quantity: K }` + preview + row "K de N reembolsadas"; fully-refunded (`refundedQuantity >= quantity`) disables button; failure shows error + no refetch; invalidation test asserts updated `refundedQuantity` + post body. Verify: `npx vitest run` (fails).
- [x] 4.2 **GREEN: `frontend/src/pages/AdminPurchases.jsx`** — replace `statusBadge(refunded)` with `refundBadge(qty, refundedQty)`: 0 → success/Confirmada; partial → warning; full → error; label `${refundedQty} de ${qty} reembolsadas`; `disabled={purchase.refundedQuantity >= purchase.quantity}`; `RefundConfirmationDialog`: `useState(selectedQuantity)` default 1, selector 1..`activeRemaining` (`type="number" min=1 max={activeRemaining}`), live preview `Reembolsar {K} × {formatCurrency(unitPrice)} = {formatCurrency(unitPrice*K)}` (`unitPrice = amount/quantity`); mutation `apiClient.post(url, { quantity: selectedQuantity })`; invalidation unchanged. Verify: `npx vitest run` (4.1 passes).

## Phase 5: Verification / Rollout (APR-011/014/015)

- [x] 5.1 **Full suites** — `dotnet test` (backend) + `npx vitest run` (frontend): replaced + new tests green, ripple consumers unaffected (D10); no MP/motivo/reservation-status/refund-editing path (APR-015). Verify: both commands green.
- [x] 5.2 **Apply migration (manual PG)** — **EXECUTED 2026-08-14 (owner-approved)**: `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --context TicketeraOnline.Api.Data.ApplicationDbContext`; history 15/15 → 16/16, zero pending; `Refunds` table created + legacy backfill applied on Supabase dev.
- [x] 5.3 **Rollback doc** — PR notes: `dotnet ef migrations remove` pre-deploy OR drop `Refunds` table post-deploy (additive, no FK consumer blocks revert); revert service/DTO/row/UI to binary; `IsRefunded` flags + audit rows kept (no data loss). Verify: PR description.

## Work-Unit Evidence (single PR)

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Whole change (14 tasks) | PR 1 | `dotnet test` then `npx vitest run` | Local API: `POST /api/admin/events/{eid}/purchases/{rid}/refund` body `{"quantity":2}` + panel walk-through | Drop `Refunds` table + revert binary service/DTO/row/UI; `IsRefunded` history kept |
