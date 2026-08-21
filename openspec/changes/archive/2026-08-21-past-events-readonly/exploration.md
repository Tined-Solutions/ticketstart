# Exploration — past-events-readonly

## Business rule (decided)
An event whose date has already passed is **immutable (read-only)** for both Admin and Organizer (owner). NO mutation is allowed on a past event: no editing content (PUT), no adding ticket stock or ticket types, no image upload/replacement, no delete, no approve/reject. **Consultation stays fully available** (view detail + view purchases). Carve-out: purchase-level operations (refunds on past-event tickets, viewing purchases) live in the payments domain and MUST keep working — only event mutation is frozen.

## Current state
Today an event's `Date` is only checked in two places:
1. **Create** — `EventService.CreateEventAsync` rejects past dates (`request.Date <= _clock.GetUtcNow()`, EventService.cs:485).
2. **Public query filters** — `GetEventByIdAsync`/`GetAllPublishedEventsAsync` hide expired events via inline EF predicate `e.Date > now` (EventService.cs:146/183).

No mutation method checks "past". The code *explicitly supports* editing past events:
- `EHE-006` comments at `EventController.cs:135-137` (PUT) and `:216-218` (image upload) say `includeExpired: true` exists so an organizer editing a past event gets the result back instead of a 404/500.
- `EventOwnershipHandler.cs:44-48` grants Admin access to all events; owner access via `OrganizerId == userId` (`:67-68`). Role policy treats admin as universal, with no date awareness.

## Backend mutation points (file:line + method)
**EventController.cs** (policy `EventOwnership` = Admin + owner):
- `PUT /api/events/{id}` → `UpdateEvent` (`:118-164`) → `EventService.UpdateEventAsync` (`EventService.cs:458`) — ownership `:471`, **no date guard**
- `DELETE /api/events/{id}` → `DeleteEvent` (`:166-200`) → `EventService.DeleteEventAsync` (`:585`) — ownership `:598`, **no date guard**
- `POST /{id}/image` → `UploadEventImage` (`:202-250`) → `EventService.ReplaceEventImageAsync` (`:727`) — ownership `:739`, **no date guard**

**AdminController.cs** (class-level `RequireAdminRole`):
- `POST /admin/events/{eventId}/ticket-types/{ticketTypeId}/stock` → `AddTicketStock` (`:186-202`) → `EventService.AddTicketStockAsync` (`:290`) — **no date guard**
- `POST /admin/events/{eventId}/ticket-types` → `AddTicketType` (`:210-226`) → `EventService.AddTicketTypeAsync` (`:368`) — **also a mutation, must freeze**
- `POST /admin/events/{eventId}/approve` → `ApproveEvent` (`:312`) → `AdminService.ApproveEventAsync` (`AdminService.cs:104`) — **no date guard**
- `POST /admin/events/{eventId}/reject` → `RejectEvent` (`:348`) → `AdminService.RejectEventAsync` (`AdminService.cs:121`) — **no date guard**

**Service signatures:** `IAdminService.ApproveEventAsync(Guid)` (:34), `RejectEventAsync(Guid, string?)` (:45); `IEventService.UpdateEventAsync(Guid, UpdateEventRequest, Guid, UserRole)` (:61), `ReplaceEventImageAsync(...)` (:102), `AddTicketStockAsync(Guid, Guid, int)` (:113), `AddTicketTypeAsync(Guid, string, decimal, int)` (:125).

**Carve-out (do NOT touch):** `AdminController.GetPurchases` (`:234`) and `RefundPurchase` (`:269`) → `_adminPurchaseService` (`AdminPurchaseService`), separate payments-domain service.

## Frontend touchpoints
**AdminPanel.jsx** (admin surface; `EventSummary` includes `event.date`, AdminService.cs:81/176, so "past" is client-computable):
- Row actions (`:383-445`): `Aprobar` (→`handleApprove`, `:156`), `Rechazar` (`:176`), `Agregar entradas` (opens `AddTicketsModal`, `:409-417`), `Compras` (→`/admin/events/{id}/purchases`, `:418-426` — **keep**), `Editar` (→`/organizer/events/{id}`, `:427-435`), `Eliminar` (→`handleDeleteClick`, `:202/218`).
- `formatDate(event.date)` at `:373`. Add `isPast = new Date(event.date) < new Date()` per row to disable Aprobar/Rechazar/Agregar entradas/Editar/Eliminar and add a "Ver" read-only action.

**OrganizerDashboard.jsx** (organizer surface; metrics payload includes `m.eventDate`):
- Row actions (`:228-259`): `Editar` (admin-only via `canEdit`, `:230-240`), `Metricas` (`:241-249`, read-only, keep), `Eliminar` (`:250-258`).

**Edit page/form (both roles):** `OrganizerEventDetail.jsx` renders `<EventForm mode="edit">` (`:60`), heading "Editar evento" (`:57`), uses `useManagementEvent` → `GET /events/{id}/manage`. `EventForm.jsx` submits `PUT /events/{id}` and `POST /events/{id}/image` (`:152-160`, `:169`).

**Existing read-only consultation view:** `EventDetail.jsx` (public, `GET /events/{id}`) is immutable and even has a "Este evento ya finalizó" banner (`:296-303`) — but it is **public-only** (Approved filter, EventController.cs:46-49), NOT usable for admin/organizer past-event consultation. There is **no** admin/organizer read-only "Ver" view today; one must be added.

## "Past event" predicate + testability
- **Domain predicate:** `Event.IsExpired(DateTime asOf) => Date < asOf` (`Models/Event.cs:28`) — strict `<`, `Date == now` is NOT expired. Pure, unit-tested in `EventExpiryTests.cs`.
- **Query predicate:** `e.Date > now` inline (EF-translatable), `now = _clock.GetUtcNow().UtcDateTime` (EventService.cs:145-146, 182-183). Codebase warns: **never call `e.IsExpired(...)` inside an IQueryable** (EF cannot translate).
- **Clock:** injected via DI `TimeProvider` (EventService.cs:22/40-48), singleton `TimeProvider.System` (Program.cs:73-75). Tests replace it with a frozen `FakeTimeProvider` (EventControllerTests.cs:518-521; `EventServiceTests.CreateServiceWithClockAndOptions` at :975).
- **Guard recommendation:** shared, testable helper evaluating `eventEntity.IsExpired(_clock.GetUtcNow().UtcDateTime)` on the **materialized entity** (pure method is fine, no EF translation), throwing 409/400. Inject `TimeProvider` into `AdminService` (currently has no clock, AdminService.cs:13-20). The immutability rule is a **hard business rule**, independent of `HideExpiredEvents.Enabled` (same rationale as the scannable window at EventService.cs:215).

## Tests
- **No backend test currently asserts past-event mutation SUCCEEDS** — all Update/Delete/Approve/Reject/stock tests seed future dates (`AddDays(30/60)`). Change is **guard addition + new negative tests**.
- Existing `EventControllerTests` Update/Delete use mocked `_eventService` with future dates (e.g. `UpdateEvent_AdminRole_LogsUpdateEventAudit` :54) — stay green (guard lives in real service).
- **Must STAY green (consultation carve-out):** `GetEventById_ManagementIncludeExpired_200` (:358), `Organizer_ManagementEvent_Expired_200` (:379) — GET on past events keeps working.
- Test command: `dotnet test` from `backend/`. Subset: `dotnet test --filter "FullyQualifiedName~EventServiceTests"`. Frontend: `npm test` (frontend/package.json:11).

## Risks / Edge cases
- `Event.Date` is non-nullable `DateTime`; strict `<` is safe. Keep comparisons in UTC (`DateTime.Kind` mixing).
- **Timezone boundary:** admin UI computes `isPast` client-side (`new Date(event.date) < new Date()`); backend compares server UTC. Backend guard is authoritative; UI disable is cosmetic.
- `HideExpiredEvents` flag only affects read filters, not mutations. Decide if immutability is hard (recommended) or flag-gated (hard rule recommended).
- **AddTicketType vs AddTicketStock:** both modify capacity — freeze BOTH.
- Approve/reject on past events: rule freezes regardless; AdminService needs a clock injected.
- Image upload: `EventForm` sends PUT then POST /image; if PUT blocked, read-only path must not reach image call.
- Notification side-effect: `UpdateEventAsync` on date change enqueues buyer emails (EventService.cs:509-575). Guard must throw BEFORE any save/notification.

## next_recommended
propose
