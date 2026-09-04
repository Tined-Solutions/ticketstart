# Matriz de Autorización — Referencia Rápida

> **Contrato canónico de comportamiento:** `openspec/specs/role-access/spec.md`.
> Este archivo es una instantánea de referencia rápida. Ante cualquier discrepancia con el código, gana el código y se corrige este documento.
> **Última actualización:** 2026-09-04.

## Modelo de autenticación

- JWT emitido en **cookie httpOnly** (`token`; HttpOnly, Secure, SameSite=Lax). No se usan headers `Authorization: Bearer`.
- Las mutaciones (POST, PUT, PATCH, DELETE) requieren el header **`X-CSRF-PROTECT`** (`Middleware/CsrfHeaderMiddleware.cs`). Exenciones: `POST /api/auth/login` y `POST /api/payments/webhook` (callback de Mercado Pago).
- Los roles viajan como claims del JWT: `Organizador`, `Staff`, `Admin`.

## Políticas (definidas en `Program.cs`)

| Política | Regla | Uso principal |
|----------|-------|---------------|
| `EventOwnership` | Handler custom: dueño del evento o Admin | Editar/eliminar evento, métricas por evento |
| `RequireOrganizadorRole` | Rol `Organizador` o `Admin` | Crear eventos, upload de imagen (event-agnostic), métricas de organizador |
| `RequireScanAccessRole` | Rol `Staff`, `Organizador` o `Admin` | Validar QR, lista de eventos escaneables |
| `RequireAdminRole` | Solo rol `Admin` | Todo `AdminController`, reintento de envío de emails |
| `[Authorize]` (sin política) | Cualquier usuario autenticado | `GET /api/auth/me` |

## Handler de autorización

| Handler | Requirement | Lógica |
|---------|-------------|--------|
| `EventOwnershipHandler` (`Authorization/EventOwnershipHandler.cs`) | `EventOwnershipRequirement` | Si el rol es `Admin`, pasa. Si no, extrae `eventId` de la ruta y verifica `OrganizerId == userId` contra la base de datos |

## Matriz rol × acción

| Acción | Público | Organizador | Staff | Admin |
|--------|---------|-------------|-------|-------|
| Ver catálogo y detalle de evento aprobado | ✅ | ✅ | ✅ | ✅ |
| Reservar entradas, pagar, consultar entradas | ✅ | ✅ | ✅ | ✅ |
| Crear evento | ❌ | ✅ | ❌ | ✅ |
| Editar evento propio | ❌ | ✅ | ❌ | ✅ |
| Editar evento ajeno | ❌ | ❌ | ❌ | ✅ |
| Subir imagen de evento (endpoint event-agnostic; adjuntar sigue por `POST /events` / `PUT /events/{id}`) | ❌ | ✅ | ❌ | ✅ |
| **Eliminar evento** | ❌ | ❌ | ❌ | ✅ |
| Ver métricas de evento propio | ❌ | ✅ | ❌ | ✅ |
| Ver métricas de cualquier evento | ❌ | ❌ | ❌ | ✅ |
| Revisar evento en vista read-only (cualquier estado, previo a aprobar) | ❌ | ✅ (propios) | ❌ | ✅ |
| Escanear / validar QR | ❌ | ✅ | ✅ | ✅ |
| Gestión admin (usuarios, eventos, aprobación, stock, tipos de entrada, compras, reembolsos, auditoría) | ❌ | ❌ | ❌ | ✅ |

> **Nota:** la eliminación de eventos es **solo Admin**. Refleja un merge pendiente (PR de cambio de organizador); en la rama actual el guard del servicio (`EventService.DeleteEventAsync`) aún permite también al dueño.

## Autorización por controlador

| Controlador | Endpoints y autorización |
|-------------|--------------------------|
| `AuthController` | `POST /login` y `POST /logout` públicos; `GET /me` autenticado |
| `EventController` | `GET /` y `GET /{id}` públicos; `GET /manage` Staff/Organizador/Admin; `GET /{id}/manage` EventOwnership; `POST` RequireOrganizadorRole; `PUT /{id}`, `DELETE /{id}` EventOwnership |
| `UploadsController` | `POST /event-image` RequireOrganizadorRole (Organizador/Admin) + rate limit `EventImageUpload` (10/min) |
| `ReservationController` | `POST /` (crear) y `PATCH /{id}` (actualizar comprador vía token de reserva) públicos |
| `PaymentController` | `POST /create-preference`, `POST /webhook`, `POST /confirm` públicos; `POST /emails/retry-pending` Admin |
| `TicketController` | `GET /lookup` y `POST /resend` públicos; `POST /validate` Staff/Organizador/Admin |
| `MetricsController` | `GET /events/{id}` EventOwnership; `GET /organizer` RequireOrganizadorRole |
| `AdminController` | Toda la clase con `RequireAdminRole` |

## No confundir

### AdminController (policy-based, current)

The whole controller is gated at class level with `[Authorize(Policy = "RequireAdminRole")]`
(`backend/Controllers/AdminController.cs`) — every endpoint below inherits it; there are no
per-method overrides. Mutating verbs additionally require the `X-CSRF-PROTECT` header
(`CsrfHeaderMiddleware`); only POST `/api/auth/login` and POST `/api/payments/webhook` are exempt.

| Endpoint | Method | Authorization | Notes |
|----------|--------|---------------|-------|
| `/api/admin/users` | POST | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | Create user account |
| `/api/admin/users` | GET | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | List all users |
| `/api/admin/users/{userId:guid}/role` | PUT | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | **AUM-001**: edit user role — 400 on self-edit, 404 unknown user, audited (`UpdateUserRole`) |
| `/api/admin/users/{userId:guid}/reset-password` | POST | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | **AUM-003**: one-time temporary credential in the response body (never stored/logged/audited), 404 unknown user, audited (`ResetPassword`), self-reset allowed |
| `/api/admin/events` | GET | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | List all events |
| `/api/admin/audit-logs` | GET | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | Audit log access |
| `/api/admin/events/{eventId:guid}/ticket-types` | POST | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | Add ticket type |
| `/api/admin/events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/stock` | POST | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | Add ticket stock |
| `/api/admin/events/{eventId:guid}/purchases` | GET | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | List event purchases |
| `/api/admin/events/{eventId:guid}/purchases/{reservationId:guid}/refund` | POST | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | Refund purchase |
| `/api/admin/events/{eventId:guid}/approve` | POST | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | Approve event |
| `/api/admin/events/{eventId:guid}/reject` | POST | `[Authorize(Policy = "RequireAdminRole")]` (inherited) | Reject event |

## Role Capabilities Matrix

| Feature | Guest | Organizador | Staff | Admin | SinAcceso |
|---------|-------|-------------|-------|-------|-----------|
| Browse events | ✅ | ✅ | ✅ | ✅ | ✅ |
| View event details | ✅ | ✅ | ✅ | ✅ | ✅ |
| Reserve tickets | ✅ | ✅ | ✅ | ✅ | ✅ |
| Lookup tickets | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create events | ❌ | ✅ | ❌ | ✅ | ❌ |
| Edit own events | ❌ | ✅ | ❌ | ✅ | ❌ |
| Delete own events | ❌ | ✅ | ❌ | ✅ | ❌ |
| View own metrics | ❌ | ✅ | ❌ | ✅ | ❌ |
| Scan tickets | ❌ | ✅ | ✅ | ✅ | ❌ |
| Validate tickets | ❌ | ✅ | ✅ | ✅ | ❌ |
| Edit any event | ❌ | ❌ | ❌ | ✅ | ❌ |
| Delete any event | ❌ | ❌ | ❌ | ✅ | ❌ |
| View all users | ❌ | ❌ | ❌ | ✅ | ❌ |
| View audit logs | ❌ | ❌ | ❌ | ✅ | ❌ |
| Edit user role (AUM-001) | ❌ | ❌ | ❌ | ✅ | ❌ |
| Reset user password (AUM-003) | ❌ | ❌ | ❌ | ✅ | ❌ |

`SinAcceso` is a **pure revocation state** (AUM-002): no policy grants it anything, so every
role-gated endpoint returns 403. Login still succeeds for a `SinAcceso` account and the
frontend post-login redirect lands on `/`. The account row is never deleted or disabled —
setting `SinAcceso` (via role editing) is the only revoke mechanism, because `AuditLogs`
FK-restrictions depend on the user row existing.

## Next-login semantics (AUM-004)

Role changes and password resets apply on **next login**. The JWT role claim is frozen inside
the httpOnly `token` cookie for up to 7 days, and this project intentionally does NOT include
JWT-revocation middleware: a user whose role was changed (e.g. to `SinAcceso`) keeps the
previous role's authority until their cookie expires or they log in again. The next login
reads the role from the database, so the new role (and its restrictions) take effect then.

## Authorization Policies

| Policy Name | Description | Allowed Roles |
|-------------|-------------|---------------|
| `EventOwnership` | Requires event ownership or admin | Event Owner, Admin |
| `RequireOrganizadorRole` | Requires organizador or admin | Organizador, Admin |
| `RequireScanAccessRole` | Requires staff, organizador or admin | Staff, Organizador, Admin |
| `RequireAdminRole` | Requires admin only | Admin |

## Custom Authorization Handlers

| Handler | Requirement | Logic |
|---------|-------------|-------|
| `EventOwnershipHandler` | `EventOwnershipRequirement` | Checks if user owns the event (via OrganizerId) or is an Admin |

## Testing Checklist

- [x] Public endpoints accessible without token
- [x] Protected endpoints require valid JWT token
- [x] Role-based endpoints enforce correct roles
- [x] Event ownership policy works correctly
- [x] Admin can access all resources
- [x] Non-owners cannot modify events
- [x] Staff and Organizadores can validate tickets (organizer scans as staff)
- [x] Organizadores can create events

## Requirements Coverage

| Requirement | Description | Status |
|-------------|-------------|--------|
| 1.6 | Role-based authorization enforcement | ✅ Implemented |
| 14.1 | Admin access to all events | ✅ Implemented |
| 14.2 | Admin can modify any event | ✅ Implemented |
| 14.3 | Admin can delete any event | ✅ Implemented |

## Security Best Practices Applied

1. ✅ **Explicit authorization** - All endpoints have explicit authorization attributes
2. ✅ **Fail secure** - Default to requiring authorization
3. ✅ **Least privilege** - Users only get access to what they need
4. ✅ **Policy-based** - Using policies instead of hardcoded role checks
5. ✅ **Custom handlers** - Event ownership validated at authorization level
6. ✅ **JWT validation** - Tokens validated on every request
7. ✅ **Role claims** - Roles stored in JWT claims for efficient checking

## Notes

- All existing controllers have proper authorization applied
- Future controllers have documented authorization patterns
- Authorization is configured in `Program.cs`
- Custom handlers are in `Authorization/` folder
- Test controller available for verification
- Documentation is comprehensive and up-to-date

## No confundir

- **No existe endpoint de registro público** — los usuarios se crean solo vía `POST /api/admin/users`.
- **No existe `TestAuthorizationController`** — fue eliminado del código.
