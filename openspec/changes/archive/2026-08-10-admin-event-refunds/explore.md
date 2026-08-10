# Exploration: Admin event refunds ("Compras" section)

> Read-only investigation for change `admin-event-refunds`. Artifact store: hybrid
> (this file + Engram `sdd/admin-event-refunds/explore`). All line numbers are
> current on-disk (Aug 2026).

## Current State

**Stack**: ASP.NET Core net9.0 + EF Core 9 + PostgreSQL (Supabase), interface-based
services, Scoped DI (`backend/Program.cs:33-40`). Frontend: React 19 + Vite SPA,
axios client (`frontend/src/api/client.js`), TanStack Query for availability.

**Data model** (`backend/Models/`):
- `Ticket` — Id, EventId, TicketTypeId, PurchaserEmail, PurchaserDNI, QRCodeData,
  IsUsed, UsedAt, CreatedAt. **NO ReservationId** → a purchase (Reservation) cannot
  currently be mapped to its tickets.
- `Reservation` — Id, UserId?, EventId, TicketTypeId, Quantity, PurchaserDNI,
  PurchaserEmail?, ExpiresAt, Status (`Active|Expired|Confirmed|Cancelled`), CreatedAt.
  Linked to Event/TicketType/User. NOT linked to Tickets.
- `Transaction` — Id, ReservationId, MercadoPagoId, Amount, Status
  (`Pending|Approved|Rejected|Refunded`), CreatedAt, UpdatedAt.
  **`MercadoPagoId` has a UNIQUE index** (`ApplicationDbContext.cs:136-137` +
  migration `20260715190343_UniqueTransactionMercadoPagoId`).
- `AuditLog` — UserId?, ActionType (`AuditActionType`), ResourceType
  (`AuditResourceType: User|Event|Payment|Ticket`), ResourceId, Details, Timestamp.
  Enum values are stored as varchar — adding a member needs **no migration**
  (precedent: `AddTicketStock`/`AddTicketType` added in ATS-005, `AuditLog.cs:82-83`).

**Availability is MATHEMATICAL** — `Quantity - sold tickets - active unexpired
reservations`, no counter. "Release stock on refund" = refunded tickets stop being
counted as sold; no counter exists to touch.

**Existing refund machinery (must NOT be reused for money)**
- `PaymentService.InitiateRefundAsync` (`backend/Services/PaymentService.cs:383`):
  calls `MercadoPagoClient.RefundPaymentAsync` (REAL money movement via MP API
  `v1/payments/{id}/refunds`) then **INSERTs a new Refunded Transaction row** with the
  same MercadoPagoId. Only reachable from the stock-failure path of
  `ProcessApprovedPaymentAsync` (`PaymentService.cs:334`), where **no Approved
  transaction exists** — hence the unique MercadoPagoId index is not violated there.
  An admin refund (purchase already has an Approved transaction) **cannot insert a
  second row** (unique index) → it must **FLIP** the existing Approved row to Refunded.
- `SendRefundNotificationAsync` (`backend/Services/EmailService.cs:161`) sends a
  "Notificación de reembolso" email. Locked rule says NO refund email → not called.

## Affected Areas

### Backend — models & migration
- `backend/Models/Ticket.cs` — add `ReservationId (Guid?)`, `IsRefunded (bool)`,
  `RefundedAt (DateTime?)` (mirror IsUsed/UsedAt shape).
- `backend/Data/ApplicationDbContext.cs` — Ticket → Reservation FK + index
  (Restrict delete; nullable so legacy rows stay valid).
- `backend/Services/TicketService.cs:42` `CreateTicketsAsync` — **already receives
  `reservationId`**; simply set `ticket.ReservationId = reservationId` for all new
  tickets (line 81-92 object initializer).
- New migration (e.g. `AddTicketReservationAndRefund`) + best-effort backfill:
  match existing tickets to Confirmed reservations on
  (EventId, TicketTypeId, PurchaserDNI, PurchaserEmail) ordered by CreatedAt,
  chunked by `reservation.Quantity`. Ambiguity exists (repeat buyers, same type);
  leftovers keep NULL ReservationId — acceptable for MVP (admin-only refunds).
- `backend/Models/AuditLog.cs` — add `AuditActionType.RefundPurchase` (no migration);
  ResourceType: reuse `Payment` (ResourceId = transaction/reservation id).

### Backend — every place tickets are counted as sold (refund must stop counting)
1. `backend/Services/EventService.cs:183` `ComputeAvailabilityAggregatesAsync` —
   `GroupBy(TicketTypeId).Count()` over ALL tickets → add `!t.IsRefunded`.
2. `backend/Services/ReservationService.cs:130` `CreateReservationTransactionalAsync` —
   `CountAsync(t => t.TicketTypeId == ticketTypeId)` → add `!t.IsRefunded`.
3. `backend/Services/MetricsService.cs:157` `CalculateMetricsAsync` — `TicketsSold`
   count and `TotalRevenue` join sum (lines 162-170) → filter `!IsRefunded`.
   `TicketsScanned` (line 173) needs no change (refund blocked if IsUsed ⇒ no overlap).
4. `backend/Services/MetricsService.cs:72-87` `GetOrganizerMetricsAsync` —
   consolidated `ticketAggregates` GroupBy: `TicketsSold = g.Count()` and
   `Revenue = ...Sum()` → filter `!IsRefunded`.
   **NOTE**: revenue is computed from TICKETS joined with TicketType.Price, NOT from
   Transactions — "excluded from revenue metrics" lands at the ticket-count level.
5. Informational lookups (decision): `LookupTicketsAsync`, `LookupTicketsByEmailAsync`,
   `LookupActiveTicketsByEmailAndDniAsync`, `ResendTicketsByEmailAsync`
   (`TicketService.cs:396-454`) — recommend excluding refunded tickets from resend and
   from "active" lookup so refunded QRs are never re-delivered as valid-looking.

### Backend — QR validation & StaffScan
- `backend/Services/TicketService.cs:227` `ValidateQRCodeAsync` — after the IsUsed
  check (line 340) add: if `ticket.IsRefunded` → `IsValid=false`,
  `Error="Entrada reembolsada"`, `Ticket=ticket` (rollback transaction).
- `backend/Controllers/TicketController.cs:101` `ValidateQRCode` — map `IsRefunded`/
  `RefundedAt` into `TicketValidationDetails` (`ITicketService.cs:174-182`).
- `frontend/src/pages/StaffScan.jsx` — already renders `result.message` (the apiError)
  and a ticket `<dl>` when `result.ticket` is present → "Entrada reembolsada" displays
  with zero JSX changes if the ticket object is returned (optional: add badge).

### Backend — admin surface (new endpoints + service)
- `backend/Controllers/AdminController.cs` — class-level
  `[Authorize(Policy = "RequireAdminRole")]` already enforced; `RequireAdminRole` =
  role `Admin` ONLY (`backend/Program.cs:149-150`). Helpers: `TryGetUserId`,
  `TryLogAuditAsync` (best-effort), `Truncate`. Add:
  - `GET /api/admin/events/{eventId:guid}/purchases` — confirmed Reservations +
    Approved Transactions + tickets per event, buyer masked, ticket type, quantity,
    amount, date, status, refunded flag; plus `totalRefunded` per event.
  - `POST /api/admin/events/{eventId:guid}/purchases/{reservationId:guid}/refund` —
    atomic refund (see Recommendation) + AuditLog entry (no motivo).
- New service **`IAdminPurchaseService`** (list + refund) registered in `Program.cs`:
  do NOT extend `PaymentService` (money path, locked rule), and keep `AdminService`
  read-only. Follows interface-per-domain pattern.
- Reuse query shape from `AdminService.GetAllEventsAsync` (paged projections) and
  `MetricsService.GetOrganizerMetricsAsync` (consolidated GroupBy, no N+1).

### Frontend — "Compras" entry point
- `frontend/src/pages/AdminPanel.jsx` — stacked GlassCard sections (Eventos / Usuarios /
  Crear usuario), NO tabs. Events table "Acciones" column (line 323) gets a "Compras"
  button → navigate to new route.
- `frontend/src/App.jsx` — new route `/admin/events/:id/purchases` wrapped in
  `<ProtectedRoute><RoleGuard allowedRoles={['Admin']}>` (pattern at lines 91-100);
  new page `frontend/src/pages/AdminPurchases.jsx` with per-purchase list + "Reembolsar"
  action + confirmation dialog (reuse `useDialog`, `Button`, `GlassCard`, `Badge`).
- No frontend test runner (config.yaml: `strict_tdd` backend only).

### Tests to mirror
- `backend/Tests/AdminControllerTicketStockTests.cs` — controller RED tests with
  `SetAuthenticatedUser` helper + mocked `IAdminService`/`IAuditLogService`.
- `backend/Tests/MetricsConsolidationTests.cs` / `MetricsPropertyTests.cs` — must be
  extended for the `!IsRefunded` filters (existing tests assert all tickets count).
- New: service tests for refund transaction (race with scan, unique-index flip),
  backfill, `ValidateQRCodeAsync` refunded path, purchases listing shape.

## Approaches

1. **Ticket-level flags + ReservationId FK + new `IAdminPurchaseService`**
   Add `IsRefunded/RefundedAt` to Ticket (mirrors IsUsed), nullable `ReservationId`
   FK backfilled best-effort, refund = single transaction: `SELECT ... FOR UPDATE`
   tickets by reservation → re-check `IsUsed` inside lock → set IsRefunded/RefundedAt →
   flip Approved Transaction → Refunded (+UpdatedAt) → commit; audit after commit.
   Concurrency pattern mirrors `EventService.AddTicketStockAsync`
   (`EventService.cs:206-277`: Npgsql `FOR UPDATE` / SQLite no-op UPDATE / InMemory).
   - Pros: minimal blast radius (IsUsed/UsedAt precedent); ticket-level state is the
     source of truth → partial refund later is a design trivium; atomic & race-safe;
     money path untouched; unique MercadoPagoId index respected.
   - Cons: backfill ambiguity for legacy tickets; 4+ sold-count sites must all add the
     `!IsRefunded` filter (miss one → refunded still counts).
   - Effort: Medium-High (migration+backfill, service+2 endpoints, 4 filter sites,
     frontend page, ~1 new migration, tests).

2. **Ticket status enum (`Valid|Used|Refunded`)** instead of bool flags.
   - Pros: richer modeling, no flag combinations (a refunded ticket can never be used).
   - Cons: replaces `IsUsed` consumers everywhere (ValidateQRCodeAsync, metrics,
     lookup, DTOs, StaffScan, email) → large blast radius; data migration
     IsUsed→status for existing rows; frontend DTOs change shape. Overkill for MVP.
   - Effort: High.

3. **No ReservationId; locate tickets by buyer identity (email+DNI+type+event)**.
   - Pros: no migration/backfill.
   - Cons: ambiguous when the same buyer purchased the same ticket type twice for the
     same event (common for events) → wrong tickets refunded. Fragile. Rejected.

4. **Reuse `PaymentService.InitiateRefundAsync`** — REJECTED: performs real money
   movement (violates locked rule), inserts a second Refunded row (violates unique
   MercadoPagoId index), and sends nothing/email path mismatch.

## Recommendation

**Approach 1.** Backend: new `IAdminPurchaseService` with `GetPurchasesAsync(eventId)`
(listing + per-event totalRefunded) and `RefundPurchaseAsync(reservationId, adminId)`
(atomic, FOR UPDATE row locks on tickets, IsUsed re-check inside lock, flip Approved
Transaction → Refunded, mark tickets refunded). Two AdminController endpoints under
the existing `RequireAdminRole`. New migration: Ticket.ReservationId (nullable FK,
Restrict) + IsRefunded + RefundedAt + best-effort backfill; set ReservationId in
`CreateTicketsAsync` going forward. Add `!IsRefunded` filters to the 4 sold-count
sites. `ValidateQRCodeAsync` returns "Entrada reembolsada" for refunded tickets
(with Ticket attached so StaffScan shows details). Frontend: "Compras" button in
AdminPanel event rows → `/admin/events/:id/purchases` (Admin-only route) →
AdminPurchases page with per-purchase rows + refund confirm dialog. AuditLog entry
`RefundPurchase` (no motivo). No refund email, no MP call.

## Risks

- **Backfill ambiguity**: best-effort chunked matching can mis-assign legacy tickets;
  admin might refund tickets belonging to a different reservation of the same buyer.
  Mitigate: nullable FK, UI shows ticket type/quantity per purchase, spec requires
  listing to show what will be refunded; document that only post-change purchases are
  guaranteed precise.
- **Missed sold-count site**: revenue/availability correctness depends on every
  Tickets-as-sold query filtering `!IsRefunded` — 4 enumerated sites + lookup/resend
  decision; MetricsConsolidationTests/MetricsPropertyTests must pin this.
- **Race scan-vs-refund**: staff scanning while admin refunds → resolved by
  FOR UPDATE row locks + IsUsed re-check inside the refund transaction; needs a
  dedicated test.
- **Unique MercadoPagoId index**: refund must FLIP the Approved row, never insert;
  test must assert single row remains.
- **Stale delivered emails** still contain QRs post-refund; scanning rejects them
  ("Entrada reembolsada") — acceptable, document in spec.
- **AuditActionType enum** is varchar-stored (no migration) per ATS-005 precedent —
  verify column type in design phase before relying on it.
- Frontend has no test runner (backend-only strict TDD); frontend quality unchecked.

## Ready for Proposal

**Yes.** Orchestrator should launch `sdd-propose` with: scope = full-purchase refund
(MVP), ticket-level modeling, admin-only policy, no money movement, no email, no
motivo, ticket state "refunded" (not deleted), and the backfill decision recorded.
