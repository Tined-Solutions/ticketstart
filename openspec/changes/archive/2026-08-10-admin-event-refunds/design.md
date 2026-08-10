# Design: Admin event refunds ("Compras" section)

## Technical Approach

Admin-only surface that records a full purchase as refunded without MP money
movement or email. Ticket state is the source of truth: new
`Ticket.ReservationId` (nullable FK) maps a purchase to its tickets; new
`IsRefunded`/`RefundedAt` flags (mirroring `IsUsed`/`UsedAt`) mark tickets
refunded. A new `IAdminPurchaseService` (list + atomic refund) backs two
`AdminController` endpoints under the existing class-level `RequireAdminRole`.
Refund FLIPS (never inserts — unique `IX_Transactions_MercadoPagoId`
`ApplicationDbContext.cs:137` forbids a 2nd row) the Approved `Transaction`
to `Refunded` inside one EF Core transaction with `SELECT ... FOR UPDATE` row
locks on the tickets, mirroring `EventService.AddTicketStockAsync`
(`EventService.cs:206-253`) and the SQLite no-op UPDATE / InMemory fallback
trio. Four sold-count sites add `!IsRefunded`; `ValidateQRCodeAsync` returns
`"Entrada reembolsada"`. Covers APR-001..011.

## Architecture Decisions

| Decision | Choice / Rejected | Rationale |
|---|---|---|
| Refunded state | `IsRefunded`+`RefundedAt` bools (NOT a `Status` enum `Valid\|Used\|Refunded`) | Enum touches every `IsUsed` consumer (QR, metrics, lookups, DTOs, StaffScan) + data migration. Bools mirror `IsUsed`/`UsedAt`; partial refund stays an additive later change. |
| Locate tickets | New `ReservationId` FK (NOT buyer email+DNI+type+event) | Buyer-key is ambiguous for repeat buyers of the same type/event (common) → wrong tickets refunded. FK is precise going forward; backfill best-effort with NULL leftovers (APR-009). |
| Service home | New `IAdminPurchaseService` (NOT reuse `PaymentService.InitiateRefundAsync` nor `AdminService`) | `InitiateRefundAsync` (`PaymentService.cs:383`) does real MP money movement AND INSERTs a 2nd `Refunded` row → both violate APR-008 + the unique index. `AdminService` is read-only by contract. Interface-per-domain precedent. |
| Tx refund record | FLIP existing Approved row (NOT INSERT new) | Unique `IX_Transactions_MercadoPagoId`. Money path can INSERT only because its stock-failure branch runs before any Approved row exists. Flip `Status→Refunded` + `UpdatedAt`. |
| Audit member | Add `AuditActionType.RefundPurchase` (no migration) | `AuditLog.ActionType` is `.HasConversion<string>().HasMaxLength(100)` (`ApplicationDbContext.cs:157-160`) — varchar-stored. Precedent: `AddTicketStock`/`AddTicketType` (ATS-005, no migration). Resource = `AuditResourceType.Payment`. |
| Race | Re-check `IsUsed` inside row lock | Refund + scan both row-lock the same tickets; loser reads post-lock committed state. APR-004. |

## Data Flow

Refund (APR-003/004):

```
POST /api/admin/events/{eid}/purchases/{rid}/refund
└→ AdminController.RefundPurchase (RequireAdminRole) → IAdminPurchaseService.RefundPurchaseAsync(rid, adminId)
   BeginTransactionAsync + ExecutionStrategy
   Npgsql: Tickets … WHERE "ReservationId"={rid} FOR UPDATE ; SQLite: no-op UPDATE/row ; InMemory: plain
   re-check any IsUsed → throw, rollback (APR-004)
   set IsRefunded=true, RefundedAt=now ; Tx==Approved? flip Status=Refunded, UpdatedAt=now (else throw, rollback APR-003)
   SaveChanges+Commit → AuditLogService.LogActionAsync(RefundPurchase/Payment) (best-effort, after commit)
```

Listing (APR-002): confirmed Reservations + Approved/Refunded Transactions,
masked buyer email/DNI, type, quantity, amount, date, `Refunded` flag;
`totalRefunded` = Σ refunded Approved amounts. 404 on missing event.

## File Changes

| File | Action | What |
|------|--------|------|
| `backend/Models/Ticket.cs` | Modify | Add `ReservationId (Guid?)`, `IsRefunded`, `RefundedAt`; `Reservation` nav. |
| `backend/Models/AuditLog.cs` | Modify | Add `AuditActionType.RefundPurchase`. |
| `backend/Data/ApplicationDbContext.cs` | Modify | Ticket→Reservation FK nullable `Restrict`; index `ReservationId`; map new fields. |
| `backend/Migrations/{ts}_AddTicketReservationAndRefund.cs` | Create | Columns + FK + index + best-effort chunked backfill (APR-009). |
| `backend/Services/IAdminPurchaseService.cs` + `AdminPurchaseService.cs` | Create | `GetPurchasesAsync`, `RefundPurchaseAsync`; Scoped in `Program.cs`. |
| `backend/Services/TicketService.cs` | Modify | `CreateTicketsAsync` set `ReservationId` (init line 81-92); `ValidateQRCodeAsync` refunded branch after line 340 → `IsValid=false`,`Error="Entrada reembolsada"`,`Ticket=ticket`; `LookupActiveTicketsByEmailAndDniAsync` add `!t.IsRefunded` (line 481); `ResendTicketsByEmailAsync` filter `!t.IsRefunded` on load (line 534-538). |
| `backend/Services/EventService.cs` | Modify | `ComputeAvailabilityAggregatesAsync` sold-by-type add `!t.IsRefunded` (line 184). |
| `backend/Services/ReservationService.cs` | Modify | `CreateReservationTransactionalAsync` sold `CountAsync` add `!t.IsRefunded` (line 130-131). |
| `backend/Services/MetricsService.cs` | Modify | `GetOrganizerMetricsAsync` `ticketAggregates` add `!t.IsRefunded` pre-GroupBy (line 72-87); `CalculateMetricsAsync` `ticketsSold`+`totalRevenue` add `!t.IsRefunded` (lines 157, 162). `TicketsScanned` unchanged (refund blocked if IsUsed ⇒ no overlap). |
| `backend/Services/ITicketService.cs` | Modify | `TicketValidationDetails` add `IsRefunded`, `RefundedAt` (line 174-182). |
| `backend/Controllers/AdminController.cs` | Modify | Add `GET events/{eid}/purchases` + `POST …/refund`; inject `IAdminPurchaseService`; `TryLogAuditAsync`. |
| `backend/Controllers/TicketController.cs` | Modify | Map `IsRefunded`/`RefundedAt` into `TicketValidationDetails` (line 133-141). |
| `frontend/src/App.jsx` | Modify | Route `/admin/events/:id/purchases` wrapped in `ProtectedRoute`+`RoleGuard allowedRoles={['Admin']}` (lines 91-100 pattern). |
| `frontend/src/pages/AdminPurchases.jsx` | Create | Event picker → purchases table → "Reembolsar" confirm dialog (`useDialog`/`GlassCard`/`Badge`); `useMutation`+`useQueryClient().invalidateQueries`. |
| `frontend/src/pages/AdminPanel.jsx` | Modify | "Acciones" column (line 324-352) add "Compras" `Button` → `navigate`. |
| `frontend/src/pages/StaffScan.jsx` | No change | Renders `result.message` + ticket `<dl>` for `{type:'error'}` (line 184) — "Entrada reembolsada" shows as-is (APR-006). |
| `frontend/src/pages/AdminPurchases.test.jsx` | Create | Vitest + RTL mirroring `AdminPanel.test.jsx`. |

## Interfaces / Contracts

```csharp
public interface IAdminPurchaseService {
    Task<AdminPurchasesResponse> GetPurchasesAsync(Guid eventId);
    Task RefundPurchaseAsync(Guid reservationId, Guid adminId);
}
public record AdminPurchasesResponse(Guid EventId, string EventName,
    IReadOnlyList<AdminPurchaseRow> Purchures, decimal TotalRefunded);
public record AdminPurchaseRow(Guid ReservationId,
    string PurchaserEmailMasked, string PurchaserDniMasked,
    string TicketType, int Quantity, decimal Amount, DateTime PurchasedAt, bool Refunded);
```

Failures → controller: `KeyNotFoundException`→404; `InvalidOperationException`
(no Approved tx / already refunded / `IsUsed`)→409; APR-004 race → `IsUsed` 409.

## Testing Strategy

| Layer | What | Approach |
|-------|------|----------|
| Unit (service) | Refund happy (tickets `IsRefunded`, single Tx row `Refunded`); no-Approved-tx fail (APR-003); `IsUsed` blocks (APR-004); audit once, no motivo | InMemory+Moq; mirror `ReservationServiceTests`/`EventServiceTicketStockTests` |
| Unit (race) | Scan wins → refund observes `IsUsed` in lock, rolls back | Npgsql `FOR UPDATE` honored; SQLite/InMemory assert re-check arm best-effort |
| Unit (listing) | Masks buyer, `Refunded` flag, `totalRefunded` sums refunded only; 404 missing event | InMemory |
| Unit (QR) | Refunded ticket → `IsValid=false`,`"Entrada reembolsada"`, ticket attached | `TicketServiceTests` |
| Unit (sold-count ×4) | Refunded excluded from availability + `TicketsSold` + `TotalRevenue` at all 4 sites | Extend `MetricsConsolidationTests`/`MetricsPropertyTests`/`EventServiceTicketStockTests`/`ReservationServiceTests` with refunded fixture |
| Unit (Tx flip) | Exactly one `Transaction` per `MercadoPagoId` after refund (unique index respected) | InMemory |
| Controller | 403 non-admin (APR-001), 404, audit `RefundPurchase` written, NO MP call/email (APR-008) | `AdminControllerTicketStockTests` pattern (`SetAuthenticatedUser`, mock `IAdminPurchaseService`+`IAuditLogService`) |
| Frontend | "Compras" navigates; refund confirm + error preserves list; query invalidation on success | Vitest + RTL mirroring `AdminPanel.test.jsx` |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file
classification, or process-integration boundary. Refund is one in-process EF
Core transaction; only external side effect (audit) is best-effort log.

## Migration / Rollout

One migration `AddTicketReservationAndRefund`: `Tickets.ReservationId uuid
NULL`, `IsRefunded boolean NOT NULL DEFAULT false`, `RefundedAt timestamptz
NULL`, FK→`Reservations.Id` `ON DELETE RESTRICT`, index. Backfill (APR-009):
per `(EventId, TicketTypeId, PurchaserDNI, PurchaserEmail)` ordered by
`CreatedAt`, chunk candidates by `reservation.Quantity`, assign sequentially;
ambiguous leftovers stay `NULL`, flagged "link unverified" in the listing.
`CreateTicketsAsync` sets the FK precisely going forward. No feature flag
(Admin-only); rollback per proposal: drop columns/endpoints/service, restore
the 4 queries, flip `Refunded` tx rows back to `Approved` (one SQL), reset
`IsRefunded=false`; keep audit rows.

## Open Questions

- [ ] `AdminPurchaseRow` masking: reuse `LogRedactor.HashIdentifier` vs new
  mask style — proposal says masked (not hashed); confirm in tasks.
- [ ] `totalRefunded`: sum `Transaction.Amount` where `Status=Refunded`
  (recommended) vs sum of ticket prices — confirm in tasks.
- [ ] `LookupTicketsByEmailAsync` (info summary): exclude refunded or surface a
  refunded count — default exclude to match sold-count semantics.

## Notes

`openspec/config.yaml` `notes`/`context` say "no frontend test runner" — STALE:
`frontend/package.json` wires `vitest` 4.1 + jsdom + @testing-library via
`npm test → scripts/wsl-test.sh`; 14 `.test.jsx` files exist. Frontend Vitest
coverage is feasible and included above. Config update is a separate task;
this design proceeds against actual repo state.