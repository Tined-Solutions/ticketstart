# Proposal: Admin event refunds ("Compras" section)

## Intent

Admins have no way to record that a buyer's purchase was refunded. Today a refund can only happen in the stock-failure auto-path (`PaymentService.InitiateRefundAsync`), which moves real money via Mercado Pago and is unreachable for an already-Approved purchase. Admins need an admin-only surface to list an event's purchases and mark one as refunded, so refunded tickets stop counting as sold, stop being scannable, and drop out of revenue — without the system touching money or sending email.

## Current-state gap

- `Ticket` has **no `ReservationId`** → a purchase (Reservation) cannot be mapped to its tickets; refunding a purchase cannot target its tickets.
- Refund is **not an admin action**: no endpoint, no UI, no audit type.
- Approved `Transaction` is the only money record; the unique `MercadoPagoId` index forbids inserting a second Refunded row → refund must **flip** the existing Approved row.
- Availability/metrics count every Ticket as sold regardless of refund state.
- `ValidateQRCodeAsync` has no refunded branch; refunded QRs would still scan as valid.

## Scope

### In Scope (MVP)
- New `IAdminPurchaseService`: `GetPurchasesAsync(eventId)` listing + `RefundPurchaseAsync(reservationId, adminId)`.
- Two `AdminController` endpoints under existing `RequireAdminRole`: `GET /api/admin/events/{eventId}/purchases`, `POST /api/admin/events/{eventId}/purchases/{reservationId}/refund`.
- Migration: `Ticket.ReservationId` (nullable FK, Restrict) + `IsRefunded` + `RefundedAt`; set `ReservationId` in `CreateTicketsAsync` going forward; best-effort chunked backfill by `(EventId, TicketTypeId, PurchaserDNI, PurchaserEmail)` ordered by `CreatedAt`, chunked by `reservation.Quantity`; leftovers stay NULL.
- `AuditActionType.RefundPurchase` (no migration; varchar-stored) + `AuditResourceType.Payment`.
- Atomic refund: `SELECT ... FOR UPDATE` tickets by reservation → re-check `IsUsed` inside lock → set `IsRefunded`/`RefundedAt` → flip Approved `Transaction` → `Refunded` → commit; audit after.
- `!IsRefunded` filter at the 4 sold-count sites: `EventService.ComputeAvailabilityAggregatesAsync`, `ReservationService.CreateReservationTransactionalAsync`, `MetricsService.CalculateMetricsAsync`, `MetricsService.GetOrganizerMetricsAsync`. Exclude refunded from `ResendTicketsByEmailAsync` and active lookups.
- `ValidateQRCodeAsync` returns `IsValid=false`, `Error="Entrada reembolsada"`, `Ticket` attached; `TicketController` maps `IsRefunded`/`RefundedAt` into `TicketValidationDetails`. StaffScan shows message as-is.
- Frontend: "Compras" button in `AdminPanel` event Acciones → `/admin/events/:id/purchases` (`ProtectedRoute` + `RoleGuard` Admin) → `AdminPurchases` page with per-purchase rows, per-event `totalRefunded`, "Reembolsar" confirm dialog.
- Full-purchase refund only; **modeled at ticket level** so partial refund is a later additive change.

### Out of Scope
- Partial / per-ticket refund (designed-for, not built).
- Mercado Pago money movement / any external refund call.
- Refund email / buyer notification.
- Motivo / refund-reason field.
- Organizer-facing refund view (Admin only).
- Editing or reverting a refund once applied.

## Capabilities

### New Capabilities
- `admin-purchase-refunds`: admin-only listing of an event's purchases and atomic full-purchase refund with ticket state, transaction flip, audit, and sold-count exclusion.

### Modified Capabilities
- None. (Sold-count filter and QR refunded branch are internal implementation of the new capability; `admin-ticket-stock` spec behavior is unchanged — availability stays mathematical.)

## Approach

New `IAdminPurchaseService` (not `PaymentService` — money path locked; not `AdminService` — keep read-only). Two `AdminController` endpoints under existing `RequireAdminRole`. One migration (nullable FK + flags + backfill); `CreateTicketsAsync` already receives `reservationId` so new tickets link precisely. Refund is one transaction with `FOR UPDATE` row locks mirroring `EventService.AddTicketStockAsync` (Npgsql / SQLite fallbacks). Flip (not insert) the Approved `Transaction` to respect the unique `MercadoPagoId` index. Add `!IsRefunded` to the 4 sold-count sites. Frontend adds a new Admin-only route reusing existing primitives (`GlassCard`, `Badge`, `useDialog`).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Models/Ticket.cs`, `AuditLog.cs` | Modified | `ReservationId`, `IsRefunded`, `RefundedAt`; `RefundPurchase` action type |
| `backend/Data/ApplicationDbContext.cs` | Modified | Ticket→Reservation FK + index (nullable, Restrict) |
| `backend/Services/TicketService.cs`, `EventService.cs`, `ReservationService.cs`, `MetricsService.cs` | Modified | `CreateTicketsAsync` sets FK; 4 sold-count `!IsRefunded` filters; `ValidateQRCodeAsync` refunded branch; resend/active exclusion |
| `backend/Controllers/AdminController.cs`, `TicketController.cs` | Modified | 2 new endpoints; `TicketValidationDetails` mapping |
| New `backend/Services/IAdminPurchaseService` + impl | New | list + atomic refund |
| New migration `AddTicketReservationAndRefund` | New | FK + flags + backfill |
| `frontend/src/pages/AdminPanel.jsx`, `App.jsx`, new `AdminPurchases.jsx` | Modified | "Compras" button + Admin-only route + page |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Backfill mis-assigns legacy tickets to wrong reservation (repeat buyers) | Med | Nullable FK; UI shows type/quantity to be refunded; document that only post-change purchases are guaranteed precise |
| Missed sold-count site → revenue/availability drift | Med | Pin all 4 sites in `MetricsConsolidationTests`/`MetricsPropertyTests`; enumerate in spec |
| Scan-vs-refund race | Med | `FOR UPDATE` + `IsUsed` re-check inside refund tx; dedicated test |
| Unique `MercadoPagoId` index violated by insert | Low | Flip existing Approved row; test single-row remains |
| `AuditActionType` not actually varchar-stored | Low | Verify column type in design before relying on no-migration |
| Stale delivered QRs scan as valid | Low | Refunded state + "Entrada reembolsada"; document in spec |

## Rollback Plan

Revert the migration by dropping `Ticket.ReservationId`/`IsRefunded`/`RefundedAt` (additive columns, no data dependency), remove the two endpoints and `IAdminPurchaseService`, restore the 4 sold-count queries, remove the `ValidateQRCodeAsync` refunded branch, and delete `AdminPurchases.jsx` + route. No data loss: `Transaction` rows flipped to `Refunded` must be flipped back to `Approved` (one-time SQL) during rollback; refunded tickets reset `IsRefunded=false`. Keep `RefundPurchase` audit rows for history.

## Dependencies

- None external. Reuses existing auth (`RequireAdminRole`), EF Core 9, Npgsql `FOR UPDATE` precedent.

## Success Criteria

- [ ] Admin can list an event's purchases and sees `totalRefunded` per event.
- [ ] Admin can refund an unused full purchase; refunded tickets scan as "Entrada reembolsada".
- [ ] Refund is blocked if any ticket of the purchase is `IsUsed`.
- [ ] Refunded tickets stop counting in availability and revenue metrics (4 sites).
- [ ] Single `Transaction` row remains (Approved→Refunded flip), unique `MercadoPagoId` respected.
- [ ] `AuditLog` entry `RefundPurchase` written; no email sent; no MP call; no `motivo`.
- [ ] `dotnet test` green; existing tests unaffected.

## Open decision inputs for spec/design

- Backfill strategy confirmation (best-effort + NULL leftovers) and whether the listing warns "legacy purchase — link unverified".
- Whether `LookupTicketsAsync`/`LookupTicketsByEmailAsync` exclude refunded or surface a refunded flag.
- `RoleGuard` reuse vs. inline admin check on the new route.
- Whether `AdminPurchases` reuses `useDialog` for confirm or a dedicated modal.