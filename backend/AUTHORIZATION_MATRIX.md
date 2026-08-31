# Matriz de Autorización — Referencia Rápida

> **Contrato canónico de comportamiento:** `openspec/specs/role-access/spec.md`.
> Este archivo es una instantánea de referencia rápida. Ante cualquier discrepancia con el código, gana el código y se corrige este documento.
> **Última actualización:** 2026-08-31.

## Modelo de autenticación

- JWT emitido en **cookie httpOnly** (`token`; HttpOnly, Secure, SameSite=Lax). No se usan headers `Authorization: Bearer`.
- Las mutaciones (POST, PUT, PATCH, DELETE) requieren el header **`X-CSRF-PROTECT`** (`Middleware/CsrfHeaderMiddleware.cs`). Exenciones: `POST /api/auth/login` y `POST /api/payments/webhook` (callback de Mercado Pago).
- Los roles viajan como claims del JWT: `Organizador`, `Staff`, `Admin`.

## Políticas (definidas en `Program.cs`)

| Política | Regla | Uso principal |
|----------|-------|---------------|
| `EventOwnership` | Handler custom: dueño del evento o Admin | Editar/eliminar evento, imagen, métricas por evento |
| `RequireOrganizadorRole` | Rol `Organizador` o `Admin` | Crear eventos, métricas de organizador |
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
| Editar / subir imagen de evento propio | ❌ | ✅ | ❌ | ✅ |
| Editar / subir imagen de cualquier evento | ❌ | ❌ | ❌ | ✅ |
| **Eliminar evento** | ❌ | ❌ | ❌ | ✅ |
| Ver métricas de evento propio | ❌ | ✅ | ❌ | ✅ |
| Ver métricas de cualquier evento | ❌ | ❌ | ❌ | ✅ |
| Escanear / validar QR | ❌ | ✅ | ✅ | ✅ |
| Gestión admin (usuarios, eventos, aprobación, stock, tipos de entrada, compras, reembolsos, auditoría) | ❌ | ❌ | ❌ | ✅ |

> **Nota:** la eliminación de eventos es **solo Admin**. Refleja un merge pendiente (PR de cambio de organizador); en la rama actual el guard del servicio (`EventService.DeleteEventAsync`) aún permite también al dueño.

## Autorización por controlador

| Controlador | Endpoints y autorización |
|-------------|--------------------------|
| `AuthController` | `POST /login` y `POST /logout` públicos; `GET /me` autenticado |
| `EventController` | `GET /` y `GET /{id}` públicos; `GET /manage` Staff/Organizador/Admin; `GET /{id}/manage` EventOwnership; `POST` RequireOrganizadorRole; `PUT /{id}`, `DELETE /{id}`, `POST /{id}/image` EventOwnership |
| `ReservationController` | `POST /` (crear) y `PATCH /{id}` (actualizar comprador vía token de reserva) públicos |
| `PaymentController` | `POST /create-preference`, `POST /webhook`, `POST /confirm` públicos; `POST /emails/retry-pending` Admin |
| `TicketController` | `GET /lookup` y `POST /resend` públicos; `POST /validate` Staff/Organizador/Admin |
| `MetricsController` | `GET /events/{id}` EventOwnership; `GET /organizer` RequireOrganizadorRole |
| `AdminController` | Toda la clase con `RequireAdminRole` |

## No confundir

- **No existe endpoint de registro público** — los usuarios se crean solo vía `POST /api/admin/users`.
- **No existe `TestAuthorizationController`** — fue eliminado del código.
