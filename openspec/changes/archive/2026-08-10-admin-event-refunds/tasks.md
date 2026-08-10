# Tasks: Admin Event Refunds ("Compras" section)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,500 (range 1,300–1,700) |
| 4000-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR (size:exception pre-approved ≤ 4000) |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

```text
Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
4000-line budget risk: Low
```

Decisions resolved in this artifact: buyer email/DNI uses a **simple mask** (e.g. `j***@gmail.com`, `3****1`), NOT `LogRedactor.HashIdentifier` (proposal says masked, not hashed). `totalRefunded` = Σ `Transaction.Amount` where `Status=Refunded` (flip-consistent; never sums ticket prices). `LookupTicketsByEmailAsync` **excludes** refunded tickets (matches sold-count semantics, APR-005). `InitiateRefundAsync` untouched (APR-008).

| Phase | Est. lines | Tasks | Focus |
|-------|-----------|-------|-------|
| 1 Model + Migration + Backfill | ~207 | 6 | APR-009: `ReservationId` FK + `IsRefunded`/`RefundedAt` + chunked backfill |
| 2 IAdminPurchaseService | ~403 | 3 | APR-002/003/004/007/008: atomic refund + listing |
| 3 Ticket-state consumers | ~191 | 6 | APR-005/006: QR reject + 4 sold-count sites + lookups/resend |
| 4 Controller endpoints | ~254 | 3 | APR-001/007/008: routes + audit |
| 5 Frontend "Compras" | ~441 | 4 | APR-010: route + page + dialog |
| 6 Verification | 0 | 3 | APR-011: suites green |
| **Total** | **~1,500** | **25** | |

## Phase 1: Model + Migration + Backfill (APR-009) — Foundation

- [x] 1.1 **RED: backfill tests** — legacy tickets chunked by reservation quantity assign `ReservationId`; ambiguous candidates stay NULL (APR-009). Files: `backend/Tests/` (migration/backfill test). Accept: chunked assignment + NULL leftovers proven. Verify: `dotnet test` (new test fails). ~80 lines.
- [x] 1.2 **Add ticket refund fields** — `Ticket.cs`: `ReservationId (Guid?)`, `IsRefunded`, `RefundedAt`, `Reservation` nav. Accept: compiles, nullable. Verify: `dotnet build`. ~15 lines.
- [x] 1.3 **Add audit enum member** — `AuditLog.cs`: `AuditActionType.RefundPurchase` (varchar-stored, no migration). Accept: enum present. Verify: build. ~2 lines.
- [x] 1.4 **Map FK + fields** — `ApplicationDbContext.cs`: Ticket→Reservation nullable FK `Restrict`, index on `ReservationId`, map new fields. Accept: model config compiles. Verify: build. ~20 lines.
- [x] 1.5 **Create migration** — `backend/Migrations/{ts}_AddTicketReservationAndRefund.cs`: columns + FK + index + best-effort chunked backfill (per EventId+TypeId+DNI+Email ordered by CreatedAt, chunk by `reservation.Quantity`; unmatched → NULL). Accept: migration applies idempotently. Verify: `dotnet ef database update` (local). ~90 lines.
- [x] 1.6 **Verify migration** — run `dotnet build` + apply migration. Accept: schema matches design. Verify: build + update succeed. 0 lines.

## Phase 2: IAdminPurchaseService — Core (APR-002/003/004/007/008)

- [x] 2.1 **RED: service tests** — InMemory+Moq mirroring `ReservationServiceTests`: refund happy (tickets `IsRefunded`, single Tx row `Refunded`), no-Approved-tx fails (APR-003), `IsUsed` blocks + race re-check arm (APR-004), audit once/no motivo (APR-007), listing masks + `totalRefunded` + 404 missing event (APR-002). Files: `backend/Tests/AdminPurchaseServiceTests.cs`. Accept: all behaviors specified. Verify: `dotnet test` (new tests fail). ~220 lines.
- [x] 2.2 **GREEN: service implementation** — `IAdminPurchaseService.cs` + `AdminPurchaseService.cs`: `GetPurchasesAsync` (simple-mask buyer, `totalRefunded` = Σ Tx.Amount `Status=Refunded`, `Refunded` flag); `RefundPurchaseAsync` (`BeginTransactionAsync` + ExecutionStrategy; Npgsql `FOR UPDATE` on tickets / SQLite no-op UPDATE / InMemory plain, mirroring `AddTicketStockAsync`; re-check `IsUsed` under lock → rollback; flip Approved Tx→`Refunded` + `UpdatedAt`; commit; audit after). Accept: contract + failures (`KeyNotFoundException`→404, `InvalidOperationException`→409). Verify: `dotnet test` (2.1 passes). ~180 lines.
- [x] 2.3 **Register service** — `backend/Program.cs`: `AddScoped<IAdminPurchaseService, AdminPurchaseService>()`. Accept: DI resolves. Verify: build + test. ~3 lines.

## Phase 3: Ticket-State Consumers (APR-005/006)

- [x] 3.1 **RED: QR + sold-count tests** — refunded ticket → `IsValid=false`/`"Entrada reembolsada"`/ticket attached (APR-006); refunded excluded at all 4 sites (APR-005): `ComputeAvailabilityAggregatesAsync`, `CreateReservationTransactionalAsync`, `GetOrganizerMetricsAsync`, `CalculateMetricsAsync`; resend/lookup exclude. Files: extend `TicketServiceTests`, `MetricsConsolidationTests`, `MetricsPropertyTests`, `EventServiceTicketStockTests`, `ReservationServiceTests`. Accept: behaviors specified with refunded fixture. Verify: `dotnet test` (fail). ~150 lines.
- [x] 3.2 **GREEN: DTO fields** — `ITicketService.cs`: `TicketValidationDetails` add `IsRefunded`, `RefundedAt`. Accept: fields exist. Verify: build. ~4 lines.
- [x] 3.3 **GREEN: QR + lookups + resend** — `TicketService.cs`: `ValidateQRCodeAsync` refunded branch → `IsValid=false`, `Error="Entrada reembolsada"`, `Ticket=ticket` (APR-006); `LookupActiveTicketsByEmailAndDniAsync` + `LookupTicketsByEmailAsync` add `!t.IsRefunded`; `ResendTicketsByEmailAsync` filter `!t.IsRefunded` on load; `CreateTicketsAsync` sets `ReservationId` (APR-009). Accept: all 5 behaviors. Verify: `dotnet test` (3.1 passes). ~25 lines.
- [x] 3.4 **GREEN: EventService** — `EventService.cs` `ComputeAvailabilityAggregatesAsync` sold-by-type add `!t.IsRefunded`. Accept: refunded excluded. Verify: test. ~2 lines.
- [x] 3.5 **GREEN: ReservationService** — `ReservationService.cs` `CreateReservationTransactionalAsync` sold `CountAsync` add `!t.IsRefunded`. Accept: excluded. Verify: test. ~2 lines.
- [x] 3.6 **GREEN: MetricsService** — `MetricsService.cs` `GetOrganizerMetricsAsync` pre-GroupBy + `CalculateMetricsAsync` `ticketsSold`/`totalRevenue` add `!t.IsRefunded`; `TicketsScanned` unchanged (no overlap). Accept: excluded. Verify: test. ~8 lines.

## Phase 4: Controller Endpoints (APR-001/007/008)

- [x] 4.1 **RED: controller tests** — `AdminControllerTicketStockTests` pattern: 403 non-admin both endpoints (APR-001), 404 missing event (APR-002), audit `RefundPurchase` written w/o motivo (APR-007), NO MP call/email (APR-008). Files: `backend/Tests/AdminControllerPurchaseTests.cs`. Accept: behaviors specified (`SetAuthenticatedUser`, mock `IAdminPurchaseService`+`IAuditLogService`). Verify: `dotnet test` (fail). ~180 lines.
- [x] 4.2 **GREEN: endpoints** — `AdminController.cs`: inject `IAdminPurchaseService` + `IAuditLogService`; `GET events/{eid}/purchases`; `POST events/{eid}/purchases/{rid}/refund`; `TryLogAuditAsync(RefundPurchase/Payment)`; map `KeyNotFoundException`→404, `InvalidOperationException`→409. Accept: class-level `RequireAdminRole` covers both (APR-001); audit after commit; no MP/email (APR-008). Verify: `dotnet test` (4.1 passes). ~70 lines.
- [x] 4.3 **GREEN: map DTO** — `TicketController.cs` map `IsRefunded`/`RefundedAt` into `TicketValidationDetails`. Accept: StaffScan receives fields. Verify: test. ~4 lines.

## Phase 5: Frontend "Compras" (APR-010)

- [x] 5.1 **RED: Vitest** — `frontend/src/pages/AdminPurchases.test.jsx` mirroring `AdminPanel.test.jsx`: panel navigates to page, refund confirm success → invalidation, failure → error + list unchanged, non-admin denied route. Accept: behaviors specified. Verify: `npm test` (fail). ~200 lines.
- [x] 5.2 **Route** — `frontend/src/App.jsx`: `/admin/events/:id/purchases` wrapped `ProtectedRoute`+`RoleGuard allowedRoles={['Admin']}`. Accept: non-admin redirected (APR-010). Verify: test. ~6 lines.
- [x] 5.3 **Panel action** — `frontend/src/pages/AdminPanel.jsx`: "Acciones" column add "Compras" `Button` → `navigate`. Accept: button navigates. Verify: test. ~5 lines.
- [x] 5.4 **Page** — `frontend/src/pages/AdminPurchases.jsx`: event picker → table (masked buyer, type, qty, amount, date, status, `Refunded` badge) + per-event `totalRefunded` + "Reembolsar" confirm (`useDialog`/`GlassCard`/`Badge`); `useMutation` + `invalidateQueries` on success; error without state mutation. Accept: APR-010 scenarios. Verify: `npm test` (5.1 passes). ~230 lines.

## Phase 6: Verification (APR-011)

- [x] 6.1 **Backend suite** — `dotnet build` + `dotnet test`: all new tests pass, existing ~202 unaffected. Accept: green. Verify: command. 0 lines.
- [x] 6.2 **Frontend suite** — `npm test`: all Vitest green. Accept: green. Verify: command. 0 lines.
- [x] 6.3 **APR-008 audit check** — grep: no MP/refund-email/motivo calls in new path; `InitiateRefundAsync` untouched. Accept: no external side effects. Verify: `git diff` review. 0 lines.

## Work-Unit Evidence (single PR)

| Unit | Goal | PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|----|---------------------|-----------------|-------------------|
| 1 | Whole change (25 tasks) | PR 1 | `dotnet test` + `npm test` | Local API: `POST /api/admin/events/{eid}/purchases/{rid}/refund` + panel walk-through | Migration Down (drop columns/FK); flip `Refunded`→`Approved` SQL; restore 4 queries; remove endpoints/service/route |

Rollback per design: drop columns/endpoints/service, restore 4 sold-count queries, flip tx rows back, reset `IsRefunded=false`; audit rows kept.
