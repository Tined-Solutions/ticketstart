# Design: JD Round 1 Fixes

## Technical Approach

8 ordered, independently-verified batches implementing the 75 Judgment Day findings, organized so the riskiest infrastructure changes (atomic stock, payment idempotency, cookie auth) land in their own revertible commits. The existing architecture (Controller → Service → EF Core + PostgreSQL, interface-based services, custom authorization handlers, hosted background service, HMAC reservation tokens) is preserved. No new abstractions are introduced unless a fix directive requires one (admin user creation, resend endpoint, httpOnly cookie, CSRF header middleware). Backend follows strict TDD (xUnit + FsCheck + WebApplicationFactory); frontend follows Vitest. One PR at completion; each batch is a self-contained green commit set.

## Architecture Overview

### Current structure
- **Presentation**: `Controllers/*` (Auth, Admin, Event, Reservation, Payment, Ticket, Metrics, TestAuthorization) extend `ControllerBase`. `TicketeraControllerBase` provides `TryGetUserId`. Authorization via `[Authorize(Policy=...]` + custom `EventOwnershipHandler`.
- **Domain/Service**: `Services/*` interfaces (`I AuthService`, `IEventService`, `IReservationService`, `ITicketService`, `IPaymentService`, `IMetricsService`, `IAdminService`, `IAuditLogService`, `IEmailService`) + implementations. Background `ReservationExpirationService` (`IHostedService`).
- **Data**: EF Core `ApplicationDbContext` (7 DbSets), Npgsql, 3 existing migrations. Concurrency via `TicketType.RowVersion` (dead per JD-C5).
- **Cross-cutting**: `Middleware/GlobalExceptionHandler`, `Helpers/LogRedactor`, `Helpers/HmacHelper`, JWT bearer in `Program.cs`.
- **Frontend**: React 19 SPA, `api/client.js` (axios, localStorage token), `context/AuthProvider` + `context/auth.js`, `pages/*`, `components/*`.

### Patterns in use
Service/Interface DI, repository-less direct `DbContext`, policy-based authorization, hosted `BackgroundService` (Timer-based, currently `async void`), HMAC tokens (reservation + QR), `GlobalExceptionHandler` (IExceptionHandler).

### Per-batch architectural delta
| Batch | Delta |
|------|-------|
| 1 | None — cleanup + hardening within existing modules |
| 2 | New `AdminController` + admin user-creation service method; remove public Auth register; add `Name` to User |
| 3 | Replace optimistic concurrency with `ExecuteUpdateAsync` atomic counter; remove `Include(Tickets)`; convert `async void` Timer → `async Task` `PeriodicTimer` |
| 4 | New `PurchaserEmail` field on Reservation; unique index on `Transaction.MercadoPagoId`; DB-transaction-wrapped confirmation; raw-bytes signature validation; email after commit (out of transaction) |
| 5 | Remove QR from public lookup; new `POST /api/tickets/resend` with rate limiter + CAPTCHA placeholder; remove `GET /api/reservations/{id}`; QR timestamp window validation |
| 6 | JWT bearer reads from cookie (not header); new `/auth/me`, logout endpoints; new CSRF header middleware on mutating routes; rate limiter pipeline; frontend removes localStorage |
| 7 | Metrics single `GroupBy`; FK `AuditLog.UserId → Users`; reservation token gains nonce+timestamp; PII redaction via `LogRedactor`; `AuditLog` gains `IpAddress`/`UserAgent`; `EventOwnershipHandler` parameter name |
| 8 | Frontend shared utils, ErrorBoundary, RoleGuard 403, accessibility, StaffScan hardening |

## Architecture Decisions

### Decision: Atomic stock via `ExecuteUpdateAsync` conditional UPDATE
**Choice**: `CurrentlyReserved` int column + `UPDATE TicketType SET CurrentlyReserved += @qty WHERE Id=@id AND (Quantity - CurrentlyReserved - SoldCount) >= @qty`; check rows affected.
**Alternatives**: raw `SELECT ... FOR UPDATE` pessimistic lock; Redis-backed semaphore; keep RowVersion retry loop.
**Rationale**: PostgreSQL serializes same-row UPDATEs automatically; no locks held across app code; dead `RowVersion` retry removed; hundreds of concurrent reservations per type supported; scales to Redis later without redesign. User-confirmed reset to 0 (no backfill).

### Decision: Transaction.MercadoPagoId unique index for idempotency
**Choice**: Unique index on `Transaction.MercadoPagoId`; existing-transaction lookup → early return; insert wrapped in `DbUpdateException` catch → 200 OK.
**Alternatives**: separate idempotency-key table; advisory locks; in-memory dedup cache.
**Rationale**: PostgreSQL guarantees atomic constraint check; no extra table; survives restart; matches MP's documented duplicate-delivery behavior.

### Decision: httpOnly cookie + SameSite=Lax + custom header CSRF
**Choice**: JWT in `httpOnly; Secure; SameSite=Lax` cookie; bearer `OnMessageReceived` reads cookie; mutating routes require custom `X-CSRF-PROTECT` header (cannot be sent cross-site without preflight) verified by middleware.
**Alternatives**: double-submit cookie; anti-forgery tokens; SameSite=Strict (breaks email links).
**Rationale**: SameSite=Lax blocks CSRF on cross-site POST/PUT/DELETE; custom header adds defense-in-depth for mutating routes (browser-fetched requests can't set arbitrary headers preflight-free); keeps email-link GETs working; breaking change accepted pre-launch.

### Decision: Email sent AFTER DB transaction commit, never revert payment on email failure
**Choice**: `BeginTransactionAsync` → confirm reservation + create tickets + insert transaction → `CommitAsync` → `SendTicketEmailAsync` (outside transaction, try/catch logged).
**Alternatives**: email inside transaction (rolls back sale on transient email outage); outbox pattern (overkill pre-launch); sync send queue.
**Rationale**: user constraint — better a manual re-send than a lost sale; MP won't resend on 200 so commit-first is safe; logged failure handled by resend endpoint (Batch 5).

### Decision: CAPTCHA placeholder + rate limit, no Turnstile wiring
**Choice**: `POST /api/tickets/resend` accepts `captchaToken`, validates only rate limit (3/hour/email) + ignores token with `// TODO: integrate Cloudflare Turnstile`.
**Alternatives**: full Turnstile now; no rate limit.
**Rationale**: scope-bound (proposal excludes real Turnstile); rate limit blocks enumeration; TODO marks real integration.

### Decision: `GetRequiredValue` helper + startup validation in `Program.cs`
**Choice**: local `static string GetRequiredValue(IConfigurationSection, string key)` extension; reject `YOUR_`-prefixed/short JWT keys at startup.
**Alternatives**: Options pattern validation (`IValidateOptions`); FluentValidation.
**Rationale**: minimal, matches existing inline validation style; no new abstraction unless directive explicitly requires it.

### Decision: Shared email client-side validator + backend mismatch check
**Choice**: `lib/format.js` (frontend) + backend `AuthService.ValidateEmail`; double email input with paste-blocked confirm field; backend rejects mismatch with 400.
**Alternatives**: frontend-only validation; single email field.
**Rationale**: typo protection for irreversible email delivery (QR lives in email); cheap server-side guard.

## Data Flow

### Reservation → Payment → Email (after Batch 3+4)

```
Checkout.jsx ─ PurchaserEmail (×2, paste-blocked) ─▶ ReservationController
  └▶ ReservationService.CreateReservationAsync
        └▶ ExecuteUpdateAsync(TicketType.CurrentlyReserved += qty WHERE avail>=qty)
              └─ 0 rows → 400 insufficient stock
        └▶ Reservation{ PurchaserEmail, PurchaserDNI } persisted; HMAC token issued
MP Checkout ─ webhook ─▶ PaymentController (raw bytes) ─▶ PaymentService.ProcessApprovedPaymentAsync
  1. ValidateWebhookSignature(rawBytes)
  2. Find Transaction by MercadoPagoId → exists? 200 OK (idempotent)
  3. BeginTransactionAsync
  4. Confirm reservation + CreateTicketsAsync(reservation.PurchaserEmail, TicketTypeId) + Insert Transaction
  5. CommitAsync
  6. (outside tx) try SendTicketEmailAsync → log on failure, no rollback
```

### Auth (after Batch 6)

```
Login → AuthController ─ Set-Cookie httpOnly;Secure;SameSite=Lax ─▶ 200 {role}
Subsequent request ─ Cookie ─▶ JwtBearer.OnMessageReceived ─▶ ctx.Token=cookie
  Mutating route ─▶ CsrfHeaderMiddleware (requires X-CSRF-PROTECT) ─▶ Controller
Frontend mount ─▶ AuthProvider ─▶ GET /auth/me ─▶ {id,email,name,role}
Logout → POST /auth/logout ─▶ Response.Cookies.Delete("token")
```

## File Changes

### Batch 1 — Scaffold & Config (Low)
| File | Action | Description |
|------|--------|-------------|
| `backend/Controllers/TestAuthorizationController.cs` | Delete | Remove scaffold diagnostic controller |
| `backend/Program.cs` | Modify | Remove `/weatherforecast` + `WeatherForecast` record (lines 218-243); add `GetRequiredValue` helper; replace 3 inline config checks; JWT placeholder rejection (`YOUR_`/`<32`); `AddHttpClient<MercadoPagoClient>(c => c.BaseAddress=...)` delegate |
| `backend/appsettings.json` | Modify | Remove `Jwt:SecretKey`; document env-injection in `appsettings.json.template` |
| `backend/Services/AuthService.cs` | Modify | `int.TryParse` for `ExpirationMinutes` (fallback 1440); password min 8 |
| `backend/Services/MercadoPagoClient.cs` | Modify | Remove constructor `BaseAddress` assignment |
| `backend/Middleware/GlobalExceptionHandler.cs` | Modify | Log `exception.Message` only; emit `StackTrace` as structured property |
| `backend/Tests/ScaffoldRemovalTests.cs` | Create | 404 tests for `/weatherforecast`, `/api/testauthorization/*` |
| `backend/Tests/ConfigValidationTests.cs` | Create | Placeholder rejection, `GetRequiredValue` missing/present, `int.TryParse` fallback, password boundary |

### Batch 2 — User Management (Low-Med) **migration: AddUserName**
| File | Action | Description |
|------|--------|-------------|
| `backend/Models/User.cs` | Modify | Add `public string Name { get; set; } = string.Empty;` |
| `backend/Data/ApplicationDbContext.cs` | Modify | `entity.Property(u => u.Name).HasMaxLength(200);` (nullable in migration, app enforces) |
| `backend/Migrations/<ts>_AddUserName.cs` | Create | `AddColumn<string>("Name", nullable: true);` rollback: `DropColumn` |
| `backend/Controllers/AdminController.cs` | Modify | Add `POST /api/admin/users` with `[Authorize(Policy="RequireAdminRole")]`, `{name,email,password,role}` body |
| `backend/Services/IAuthService.cs` + `AuthService.cs` | Modify | New `CreateUserAsync(name,email,password,role)`; shared `ValidateEmail`; remove `RegisterAsync` |
| `backend/Controllers/AuthController.cs` | Modify | Remove `Register` endpoint |
| `backend/Tests/AuthenticationPropertyTests.cs` | Modify | Remove public-register tests; add admin-create property tests (FsCheck valid role assignment, admin-only 403, anon 401) |
| `backend/Tests/AdminControllerTests.cs` | Modify | Cover new endpoint |
| `frontend/src/pages/Register.jsx` | Delete | Remove public registration page |
| `frontend/src/App.jsx` | Modify | Remove `/register` route |
| `frontend/src/pages/AdminPanel.jsx` (or new section) | Modify | Add user-creation form (Admin only) |

### Batch 3 — Reservation Stock (HIGH) **migration: AddCurrentlyReserved**
| File | Action | Description |
|------|--------|-------------|
| `backend/Models/TicketType.cs` | Modify | Add `public int CurrentlyReserved { get; set; }` (default 0); keep `RowVersion` (unused after) — or drop per cleanup decision (keep to minimize migration churn) |
| `backend/Data/ApplicationDbContext.cs` | Modify | `entity.Property(t => t.CurrentlyReserved).IsRequired().HasDefaultValue(0);` |
| `backend/Migrations/<ts>_AddCurrentlyReserved.cs` | Create | `AddColumn<int>("CurrentlyReserved", defaultValue: 0);` — **no backfill, RESET TO 0** (user decision). Rollback: `DropColumn` |
| `backend/Services/ReservationService.cs` | Modify | Replace `BeginTransaction + SumAsync + CountAsync + retry loop` with single `ExecuteUpdateAsync(s => s.SetProperty(t => t.CurrentlyReserved, t => t.CurrentlyReserved + qty).Where(t => t.Id==id && (t.Quantity - t.CurrentlyReserved - t.SoldCount) >= qty))`; inspect `rowsAffected==0` → throw insufficient-stock; persist Reservation |
| `backend/Services/ReservationExpirationService.cs` | Modify | `ReleaseExpiredReservationsAsync` → per reservation `ExecuteUpdateAsync` decrement `CurrentlyReserved`; change `async void CheckExpiredReservations` → `async Task ExecuteAsync`; replace `Timer` with `PeriodicTimer(TimeSpan.FromMinutes(1))` + `cancellationToken` |
| `backend/Services/EventService.cs` | Modify | Remove `.Include(e => e.Tickets)` from `GetEventByIdAsync` + `GetAllPublishedEventsAsync` (`MapToEventWithAvailability` → O(1) `Quantity - CurrentlyReserved - SoldCount`) |
| `backend/Services/IEventService.cs` | Modify | (if signature changes) |
| `backend/Tests/ReservationServiceTests.cs` + `ReservationPropertyTests.cs` | Modify | Replace RowVersion tests with `ExecuteUpdateAsync` tests; FsCheck invariant `CurrentlyReserved + SoldCount <= Quantity`; concurrent reservation test (10 parallel tasks, 1 stock → exactly 1 success) |
| `backend/Tests/ReservationExpirationServiceTests.cs` | Modify | `async Task` signature assertion; `PeriodicTimer` cancellation |
| `backend/Tests/EventServiceTests.cs` | Modify | Assert no `Include(Tickets)`; availability math |

### Batch 4 — Payment Pipeline (HIGH) **migration: UniqueTransactionMercadoPagoId**
| File | Action | Description |
|------|--------|-------------|
| `backend/Services/IReservationService.cs` + `ReservationService.cs` | Modify | Add `PurchaserEmail` param to `CreateReservationAsync`; validate `email == confirmEmail` else 400 |
| `backend/Models/Reservation.cs` | Modify | Add `public string PurchaserEmail { get; set; } = string.Empty;` |
| `backend/Data/ApplicationDbContext.cs` | Modify | `entity.HasIndex(t => t.MercadoPagoId).IsUnique();` (change from non-unique) |
| `backend/Migrations/<ts>_UniqueTransactionMercadoPagoId.cs` + `<ts>_AddReservationPurchaserEmail.cs` | Create | Add `PurchaserEmail` (nullable for legacy, app-required going forward); unique index on `MercadoPagoId`. Rollback: drop index, drop column |
| `backend/Services/PaymentService.cs` | Modify | Reorder `ProcessApprovedPaymentAsync`: (1) find existing Transaction by `MercadoPagoId` → return 200; (2) wrap confirm+tickets+insert in `BeginTransactionAsync/CommitAsync` with `catch (DbUpdateException)` for unique violation → 200; (3) `CreateTicketsAsync(reservation.PurchaserEmail, reservation.TicketTypeId)`; (4) `SendTicketEmailAsync` AFTER commit, try/catch log-only |
| `backend/Services/TicketService.cs` | Modify | `CreateTicketsAsync` uses `reservation.PurchaserEmail` (not `User?.Email ?? "guest@..."`); set `TicketTypeId = reservation.TicketTypeId` (JD-S10 covered here) |
| `backend/Services/PaymentService.cs` (signature) | Modify | `ValidateWebhookSignature(byte[] rawBody, ...)` — accept raw bytes |
| `backend/Controllers/PaymentController.cs` | Modify | Read `Request.Body` as `byte[]` before JSON deserialize; pass bytes to validator; startup validation of `WebhookSecret` |
| `backend/Tests/PaymentPropertyTests.cs` + `PaymentControllerTests.cs` | Modify | Idempotency (duplicate → 200, no-op); concurrent duplicate → unique catch; atomic rollback when step 2 fails; raw-bytes HMAC vector; email-failure-no-rollback |
| `backend/Tests/ReservationControllerTests.cs` | Modify | Email mismatch → 400; PurchaserEmail persisted |
| `frontend/src/pages/Checkout.jsx` | Modify | Double email input, paste-blocked confirm; send `purchaserEmail` + `confirmEmail` |
| `frontend/src/pages/CheckoutReturn.jsx` | Modify | Truthful copy now that email actually sends |

### Batch 5 — Ticket Lookup (Low-Med)
| File | Action | Description |
|------|--------|-------------|
| `backend/Controllers/TicketController.cs` | Modify | `GET /api/tickets/lookup` strips `qrCodeData`/`qrSrc`; new `POST /api/tickets/resend` with `[EnableRateLimiting("Resend")]`, accepts `{email, captchaToken}`, ignores token (TODO Turnstile), generic response, queues `IEmailService.ResendAsync`; remove any `GET /api/reservations/{id}` (already in ReservationController — coordinate there) |
| `backend/Controllers/ReservationController.cs` | Modify | Remove `GetReservation` endpoint |
| `backend/Services/ITicketService.cs` + `TicketService.cs` | Modify | `VerifyQRCodeSignature` adds timestamp window check: `purchaseDate <= ts <= event.EndDate+24h` and `ts <= now`; info-only lookup DTO (no QR); `ResendTicketsByEmailAsync` |
| `backend/Helpers/HmacHelper.cs` | Modify | Helper to extract timestamp from QR payload token; signature unchanged |
| `backend/Program.cs` | Modify | `AddRateLimiter` policy `"Resend"` (FixedWindow 3/hour/email] |
| `backend/Tests/TicketLookupPropertyTests.cs` | Modify | Response excludes QR; resend rate limit (4th → 429); generic response for missing email; QR timestamp boundaries (FsCheck) |
| `frontend/src/pages/TicketLookup.jsx` | Modify | Info-only card (no print/download/QR); "Revisá tu email para ver el QR"; resend form with email + CAPTCHA placeholder |

### Batch 6 — Auth Session (HIGH) **no migration**
| File | Action | Description |
|------|--------|-------------|
| `backend/Controllers/AuthController.cs` | Modify | `Login` sets cookie `httpOnly;Secure;SameSite=Lax;Expires`; `Logout` deletes; new `GET /auth/me` returns `{id,email,name,role}`; remove `register` (coordinated with Batch 2 if not done) |
| `backend/Program.cs` | Modify | `AddJwtBearer` `Events.OnMessageReceived = ctx => { ctx.Token = ctx.Request.Cookies["token"]; return Task.CompletedTask; };`; `AddRateLimiter` with `"Login"` (SlidingWindow 10/min/IP) and `"Reservations"` (FixedWindow 5/min/IP); `app.UseRateLimiter()`; register `CsrfHeaderMiddleware` |
| `backend/Middleware/CsrfHeaderMiddleware.cs` | Create | On mutating methods (POST/PUT/PATCH/DELETE) require header `X-CSRF-PROTECT` (any non-empty value) — reject 400 otherwise; allow GET/OPTIONS |
| `backend/Controllers/AuthController.cs` + `ReservationController.cs` | Modify | `[EnableRateLimiting("Login")]` / `("Reservations")]` |
| `frontend/src/api/client.js` | Modify | Remove `localStorage.getItem/setItem("token")`; remove `Authorization` header injection; fix baseURL (`PROD` → `VITE_API_BASE_URL` mandatory; `DEV` → `http://localhost:5193`); set `X-CSRF-PROTECT` header on mutating requests; `withCredentials: true` |
| `frontend/src/context/AuthProvider.jsx` + `context/auth.js` | Modify | Replace `localStorage` token with `GET /auth/me` on mount + after login; logout calls `POST /auth/logout` |
| `backend/Tests/AuthenticationPropertyTests.cs` + `AuthCookieTests.cs` | Modify/Create | Cookie attributes; `/auth/me` 200/401; logout clears cookie; CSRF middleware rejects cross-site-mimicking request; rate limit 11th login → 429; 6th reservation → 429 |
| Many frontend tests touching auth | Modify | Remove localStorage mocks; use cookie-aware test harness |

### Batch 7 — Audit & Data Integrity (Low-Med) **migration: AddAuditLogUserFkAndTracking**
| File | Action | Description |
|------|--------|-------------|
| `backend/Models/AuditLog.cs` | Modify | Add `public string? IpAddress { get; set; }`, `public string? UserAgent { get; set; }` |
| `backend/Data/ApplicationDbContext.cs` | Modify | FK `AuditLog.UserId → Users.Id` with `OnDelete(Restrict)`; index on `UserId`; configure `IpAddress`/`UserAgent` (max lengths) |
| `backend/Migrations/<ts>_AddAuditLogUserFkAndTracking.cs` | Create | Add FK constraint + columns. **Migration order constraint**: existing `AuditLog.UserId` rows with `Guid.Empty` must be handled — either set to a System user row first OR the migration cleanses `Guid.Empty` → NULL (UserId becomes nullable) before FK. Rollback: drop FK + columns. |
| `backend/Services/MetricsService.cs` | Modify | Consolidate `GetOrganizerMetricsAsync` to single `GroupBy(eventId)` projection returning all metric aggregates |
| `backend/Services/AdminService.cs` | Modify | `GetAllLogsAsync(int page, int pageSize=50)` returns `PagedResult<AuditLog>` |
| `backend/Services/AuditLogService.cs` | Modify | Wrap write in try/catch, log failure out-of-band (never throw to caller); capture IP/UA from `IHttpContextAccessor` |
| `backend/Services/ReservationService.cs` | Modify | HMAC token gains `nonce:timestamp:signature` + expiry validation (reject expired) |
| `backend/Services/TicketService.cs` | Modify | Use `LogRedactor.HashIdentifier` for email + DNI in logs |
| `backend/Controllers/EventController.cs` | Modify | `TryGetUserRole` returns `false` when `Enum.TryParse` fails (caller returns 403) |
| `backend/Controllers/PaymentController.cs` | Modify | Webhook audit `UserIdentifier = "System"` (not `Guid.Empty`) |
| `backend/Controllers/ReservationController.cs` | Modify | Persist `ClientIp` + `UserAgent` on guest reservation (`HttpContext.Connection.RemoteIpAddress`, `Request.Headers.UserAgent`) |
| `backend/Authorization/EventOwnershipHandler.cs` + `EventOwnershipRequirement.cs` | Modify | Requirement carries `RouteParameterName` (default `"id"`); handler reads `routeValues[req.RouteParameterName]` |
| `backend/Services/IReservationService.cs` + `Reservation.cs` | Modify | Optional: store `ClientIp`/`UserAgent` on Reservation for guest traceability |
| `backend/Tests/MetricsPropertyTests.cs`, `MetricsControllerTests.cs`, `AdminPropertyTests.cs`, `AuditLogTests.cs`, `EventControllerTests.cs` | Modify | Single-query assertion; pagination math; FK constraint; out-of-band audit failure; `TryGetUserRole` false; System identifier; reservation token expiry (FsCheck); PII redaction in logs (FsCheck no-raw-PII property); IP/UA capture; ownership handler with `eventId` |

### Batch 8 — Frontend Quality (Low)
| File | Action | Description |
|------|--------|-------------|
| `frontend/src/lib/format.js` | Create | `formatEventDate`, `formatCurrency` |
| `frontend/src/lib/apiError.js` | Create | `getErrorMessage` |
| 7+ pages/components | Modify | Import shared utils; delete inline copies |
| `frontend/src/components/RoleGuard.jsx` | Modify | Render 403 page instead of redirect |
| `frontend/src/components/EventForm.jsx` | Modify | Validate `eventId` before PUT; catch block → `error`/`warning`; remove explicit `Content-Type` |
| `frontend/src/components/Modal.jsx` | Modify | Re-evaluate focusable nodes on each Tab |
| `frontend/src/context/ToastProvider.jsx` | Modify | `nextId` via `useRef` |
| `frontend/src/pages/StaffScan.jsx` | Modify | GUID regex validation before API; `useRef` scanner with cleanup; `sessionStorage` history |
| `frontend/src/pages/OrganizerEventDetail.jsx` | Modify | Use `GET /events/{id}/manage` with `EventOwnership` |
| `frontend/src/App.jsx` | Modify | Wrap routes with `ErrorBoundary` |
| `frontend/src/components/ErrorBoundary.jsx` | Create | Class boundary + fallback UI |
| `frontend/src/components/Card.jsx` | Modify | Filter unknown props (allowlist) |
| `frontend/src/components/__tests__/accessibility.test.jsx` | Modify | `import { vi } from 'vitest'` explicit |
| `frontend/src/pages/EventList.jsx` | Modify | Native `<button>` instead of `<div role="button">` |
| `frontend/src/pages/NotFound.jsx` | Modify | Add home navigation link |
| `frontend/src/lib/__tests__/format.test.js` + `apiError.test.js` | Create | Unit tests for utilities |
| Frontend component tests covering each modified file | Modify/Create | Per REQ |

## Interfaces / Contracts

```csharp
// Batch 2
public record AdminCreateUserRequest(string Name, string Email, string Password, UserRole Role);
// POST /api/admin/users → 201 { id, name, email, role } | 403 | 401 | 400

// Batch 4
public record CreateReservationRequest(Guid EventId, Guid TicketTypeId, int Quantity,
    string PurchaserDNI, string PurchaserEmail, string ConfirmEmail);
// Reservation.PurchaserEmail : string (IsRequired at app layer)
// PaymentService.ProcessApprovedPaymentAsync(rawBody: byte[], ...) : Task<WebhookResult>
// IEmailService.ResendTicketsByEmailAsync(string email) : Task  (Batch 5)

// Batch 5
public record TicketResendRequest(string Email, string CaptchaToken);
// POST /api/tickets/resend → 200 { message: "Si hay entradas asociadas, recibirás un email" } | 429

// Batch 6
// POST /auth/login → Set-Cookie token; 200 { userId, role, name }
// POST /auth/logout → 204 (cookie deleted)
// GET  /auth/me → 200 { id, email, name, role } | 401
// CsrfHeaderMiddleware: mutating routes require header "X-CSRF-PROTECT: 1"

// Batch 7
// GetAllLogsAsync(int page, int pageSize = 50) → PagedResult<AuditLogDto>
// EventOwnershipRequirement(string RouteParameterName = "id")
// Reservation token format: "{nonce}:{timestamp}:{hmac}" — token secret + maxAge from ReservationTokenOptions
```

## Testing Strategy

### TDD order (backend) — tests written FIRST then implementation
- **B1**: `ScaffoldRemovalTests`, `ConfigValidationTests` → green → cleanup code.
- **B2**: update `AuthenticationPropertyTests` (admin endpoint, role FsCheck) → green → Admin endpoint, Name migration, remove Register.
- **B3**: `ReservationPropertyTests` (FsCheck invariant `CurrentlyReserved + SoldCount <= Quantity`; concurrent 1-stock test) + `ReservationExpirationServiceTests` (`async Task`, `PeriodicTimer` cancellation) + `EventServiceTests` (no `Include(Tickets)`) → green → `ExecuteUpdateAsync`, `CurrentlyReserved` migration, expiration rewrite, EventService availability.
- **B4**: `PaymentPropertyTests` (idempotency, atomic rollback on step-2 failure, raw-bytes HMAC vector from MP docs, email-failure-no-rollback) + `ReservationControllerTests` (email mismatch 400) → green → unique index migration, reordered `ProcessApprovedPaymentAsync`, atomic transaction, raw-bytes validation, PurchaserEmail flow.
- **B5**: `TicketLookupPropertyTests` (no-QR response shape, FsCheck QR timestamp window boundaries `purchaseDate ≤ ts ≤ event.End+24h ∧ ts ≤ now`, resend rate limit, generic response) → green → info-only lookup, resend endpoint, QR window, endpoint removal.
- **B6**: `AuthCookieTests` (cookie attrs, `/auth/me` 200/401, logout clears, CSRF header rejects, login rate 11th→429, reservation 6th→429) → green → cookie bearer, middleware, rate limiters, `/auth/me`, logout; then frontend `client.js`/`AuthProvider` tests.
- **B7**: `MetricsPropertyTests` (1-query assertion via `Logger`/query interceptor), `AdminPropertyTests` (pagination math FsCheck), `AuditLogTests` (out-of-band failure isolation, System identifier, PII redaction FsCheck no-raw-email/DNI property, FK Restrict, IP/UA capture, reservation token expiry FsCheck, ownership handler `eventId` param) → green → all changes + migration.
- **B8**: `format.test.js`, `apiError.test.js`, plus per-component Vitest tests → green → implementations.

### Property-based (FsCheck) definitions
| Property | Domain | Invariant |
|----------|--------|-----------|
| Stock invariant | `qty ∈ [1..50]`, `Quantity ∈ [10..1000]` | After any successful reservation, `0 ≤ CurrentlyReserved ∧ CurrentlyReserved + SoldCount ≤ Quantity` |
| Concurrent reservation | fixed `Quantity=1`, `N ∈ [2..20]` concurrent | Exactly one succeeds |
| QR timestamp window | `purchaseTs, eventEnd, scanTs` | Valid iff `purchaseTs ≤ scanTs ≤ eventEnd+24h ∧ scanTs ≤ now` |
| Reservation token | `nonce, age` | Token rejected iff `age > expiry`; nonce unique per reservation |
| PII redaction | arbitrary email/DNI strings | Logged message contains no raw email, no raw DNI — only `HashIdentifier` |
| Pagination | `page, pageSize, total` | `skip = (page-1)*pageSize`, `returned = min(pageSize, total - skip)` |
| Metrics single-query | `N events` | Query count constant (1) regardless of N |

### Frontend tests (Vitest, jsdom, RTL)
Shared-utils tests, RoleGuard 403 render, EventForm undefined-eventId + catch-block feedback + Content-Type absence, Modal focus-trap dynamic, ToastProvider remount `nextId`, StaffScan GUID validation + sessionStorage, OrganizerEventDetail fetch URL, ErrorBoundary fallback, Card prop filter, EventList native button, NotFound home link, AuthProvider `/auth/me` on mount + no `localStorage` token operations (grep-based), client baseURL env branches.

### Per-batch verification gate
After each batch: `dotnet test` (target ~333 green) AND `pnpm vitest` (for batches with frontend changes; target ~208 green). No batch N+1 until batch N is green. Batches 2‖3 after 1; 7‖8 after their deps (but 7 pulls DB migration; 8 frontend — orthogonal).

## Migration / Rollout

### Migration sequencing (4 migrations across batches)
| Order | Batch | Migration name | Action | Rollback |
|-------|-------|----------------|--------|----------|
| 1 | B2 | `AddUserName` | `AddColumn Name (string, nullable)` | `DropColumn Name` |
| 2 | B3 | `AddCurrentlyReserved` | `AddColumn CurrentlyReserved (int, default 0)` **RESET TO 0, no backfill** | `DropColumn CurrentlyReserved` |
| 3a | B4 | `AddReservationPurchaserEmail` | `AddColumn PurchaserEmail (string, nullable legacy, app-required new)` | `DropColumn PurchaserEmail` |
| 3b | B4 | `UniqueTransactionMercadoPagoId` | `Create unique index on Transactions(MercadoPagoId)` | `DropIndex` |
| 4 | B7 | `AddAuditLogUserFkAndTracking` | Pre-step: cleanse existing `UserId = Guid.Empty` rows (set NULL OR rewrite to System user); make `UserId` nullable; add FK `AuditLog.UserId → Users.Id OnDelete Restrict`; add `IpAddress`, `UserAgent` columns | `DropColumn IpAddress/UserAgent`, `DropForeignKey`, re-set `UserId` nullable |

### Rollback procedure (per batch)
1. Bring app offline (no in-flight requests).
2. `dotnet ef database update <previous-migration>` (rolls back the batch's migration).
3. `git revert <batch-commit-range>`.
4. Bring app back online.
- **B3 special**: column drop is safe — no data lost (we never backfilled).
- **B4 special**: drop unique index BEFORE reverting PaymentService (reverted code expects duplicate MP IDs to be possible; index would block inserts).
- **B7 special**: drop FK BEFORE reverting to nullable `UserId` OR before reverting `AuditLogService` (reverted code writes `Guid.Empty`, which FK would reject). Cleanse step must be reversible.

### Feature flags
None — pre-launch; full revert restores prior behavior (localStorage JWT, public registration, QR lookup, etc.).

## Cross-Batch Concerns

### Multi-batch files (modification order)
| File | Batches | Order rationale |
|------|---------|-----------------|
| `backend/Program.cs` | 1, 3, 4, 5, 6, 7 | B1 (cleanup + GetRequiredValue + http client) → B3 (currently reserved wiring N/A) → B4 (WebhookSecret validation) → B5 (Resend rate limiter) → B6 (cookie bearer + login/reservation rate limiters + CSRF middleware + UseRateLimiter) → B7 (HttpAccessor already) |
| `backend/Controllers/AuthController.cs` | 2, 6 | B2 removes `register`; B6 adds cookie/login rate/`/auth/me`/logout. Do B2 then B6. |
| `backend/Controllers/ReservationController.cs` | 4, 5, 6, 7 | B4 adds PurchaserEmail + mismatch check; B5 removes `GetReservation`; B6 adds `[EnableRateLimiting("Reservations")]`; B7 stores IP/UA. Apply in numbered order. |
| `backend/Services/ReservationService.cs` | 3, 4, 7 | B3 atomic stock; B4 adds `PurchaserEmail`; B7 adds nonce+timestamp+expiry to token. Apply B3 → B4 → B7. |
| `backend/Services/PaymentService.cs` | 4 (signature, atomicity, idempotency), 6 (email after commit still applies) | Single pass in B4. |
| `backend/Services/TicketService.cs` | 4 (PurchaserEmail + TicketTypeId set), 5 (QR window), 7 (PII redaction) | B4 → B5 → B7. |
| `backend/Tests/*` | all | Each batch updates its related tests; B6 touches ALL auth-using tests (cookie migration). |
| `frontend/src/pages/TicketLookup.jsx` | 5, 8 | B5 info-only card; B8 if any accessibility tweaks. |
| `frontend/src/api/client.js` | 6 | Single pass — baseURL fix + cookie + CSRF header. |
| `frontend/src/App.jsx` | 2 (remove /register), 8 (ErrorBoundary) | B2 → B8. |

### Test suites spanning multiple batches
- `AuthenticationPropertyTests` (B2: admin endpoint; B6: cookie auth).
- `ReservationControllerTests` + `ReservationPropertyTests` (B3 atomic; B4 PurchaserEmail mismatch; B6 rate limit; B7 token hardening).
- `PaymentPropertyTests` + `PaymentControllerTests` (B4 entirely).
- `TicketLookupPropertyTests` + `TicketServiceTests` (B5 + B7 PII).
- All frontend auth tests (B6 breaking change — see below).

### Batch 6 httpOnly cookie migration — blast radius on tests
- **Backend integration tests (WebApplicationFactory)**: every test that authenticates currently sets `Authorization: Bearer <token>` header. After B6, bearer comes from cookie. Two accepted strategies:
  1. **Recommended**: tests call `/auth/login` first, capture `Set-Cookie`, replay cookie (via `HttpClientHandler` `CookieContainer`). Realistic, tests the cookie flow.
  2. **Pragmatic fallback**: keep bearer header support in `OnMessageReceived` (`ctx.Token = cookie ?? Authorization header`) during transition — NOT recommended long-term.
- **Decision**: strategy 1. Auth-using tests get a shared `AuthTestHelper.AuthenticateAsync(role)` extension that returns an `HttpClient` with cookie container + CSRF header.
- **Frontend Vitest**: every test mocking `localStorage.getItem("token")` must be migrated to mock `/auth/me` (MSW interceptor returning 200/401). `client.js` tests must stop asserting `Authorization` header presence.
- **CSRF middleware**: mutating test requests must add `X-CSRF-PROTECT` header — covered by shared `AuthTestHelper`.

## Risk Mitigation

| Batch | Risk | Mitigation + Verification |
|-------|------|---------------------------|
| B1 | Low — over-restrict startup validation blocks deploys | Validate in dev with sample env; test both branches |
| B2 | Low-Med — admin endpoint mis-authorization | TDD: FsCheck over `UserRole` enum → only Admin gets 201, others 403, anon 401; migration rollback tested |
| **B3 (HIGH)** | Migration resets `CurrentlyReserved=0` loses in-flight reservations → oversell window until reconciliation | **Accepted by user**: 10-min expiry auto-reconciles existing Active reservations (expire → decrement counter from 0 → no-op, reservation just ends). Verify: concurrent 1-stock test (exactly one winner); deploy during low-traffic window; post-deploy monitor `CurrentlyReserved` for negative values (clamped with `Math.Max(0, ...)` in `ExecuteUpdateAsync`). Rollback: drop column (no backfill → no data lost). |
| **B4 (HIGH)** | Atomicity regression delays webhook ∥ email failure handling ∥ unique index blocks legitimate duplicates | (a) DB transaction + email out-of-band (failure no rollback). (b) MP retries on 500; on exception we return 500 (MP retries) — only return 200 after successful commit OR confirmed duplicate. (c) Unique index on `MercadoPagoId` — MP guarantees unique payment IDs; duplicate delivery handled by early-lookup + unique catch (returns 200). Verify: idempotent duplicate → 200 no-op; concurrent duplicate → one 200; step-2 forced fail → rollback; email-service mock throws → tickets still persisted. Rollback: drop index BEFORE code revert. |
| B5 | Low-Med — resend endpoint enumeration, QR window false rejects | Generic response regardless of email existence (test both cases); rate limit caps enumeration; FsCheck QR timestamp window property covers boundaries |
| **B6 (HIGH)** | CSRF exposure, breaking change to mobile/SSR; test migration scope | SameSite=Lax + custom `X-CSRF-PROTECT` header middleware on mutating routes (browser can't set custom headers cross-site preflight-free); breaking change accepted pre-launch; `AuthTestHelper` refactor reduces test migration risk; full Vitest sweep before merge |
| B7 | Low-Med — FK migration blocks audit writes; metrics single-query correctness | Migration cleanses `Guid.Empty` rows before FK (`UserId` nullable OR System user seed); FK Restrict tested; metrics single-query verified via query-count logger; PII redaction FsCheck no-raw-property |
| B8 | Low — React shared-utils import breakage | All consuming files updated in same commit; per-component Vitest coverage |

### HIGH-risk batch rollback + verification plans (detailed)

**Batch 3**: Pre-deploy snapshot `CurrentlyReserved` is absent. Deploy migration (column added, default 0). All existing Active reservations remain Active in DB; their `ExpiresAt` still applies — expiration service (now `PeriodicTimer`) decrements `CurrentlyReserved` via `ExecuteUpdateAsync`. Since they were not counted in `CurrentlyReserved` (reset to 0), decrement clamps at 0 (`Math.Max(0, CurrentlyReserved - qty)`) to avoid negatives. New reservations correctly use the counter. After 10 min all pre-existing Active reservations expire → state clean. Verification gate passes BEFORE next batch:
- Concurrent 1-stock test green
- `CurrentlyReserved >= 0` invariant after every operation
- Existing Active reservations still expire normally. Rollback: `dotnet ef database update AddUserName` (previous migration), git revert B3 commits.

**Batch 4**: Critical ordering — apply unique-index migration**and** code in same deploy (reverted code expects duplicate MP IDs allowed; new code requires unique). Pre-deploy: backup Transactions table. Deploy: migration adds unique index; new PaymentService code active. Verify in sandbox with MP sandbox duplicate webhooks:
- 1st webhook → 201/200, Transaction inserted
- 2nd webhook → 200, no-op, no refund
- Concurrent duplicate → one 200, `DbUpdateException` caught on loser
- Forced step-2 failure → `RollbackAsync`, reservation NOT confirmed, MP retries
- Email service throws → tickets still in DB, error logged.
Rollback: `dotnet ef database update AddReservationPurchaserEmail` (`UniqueTransactionMercadoPagoId` is later — actually drop unique index migration first via `database update` to previous migration name), **drop unique index BEFORE reverting PaymentService** (reverted code may insert duplicate MP IDs).

**Batch 6**: No migration. Breaking change to all auth consumers. Pre-deploy: confirm no external API consumers (mobile/SSR) — pre-launch app, accepted. Verify:
- Login response sets cookie with correct flags (integration test asserts `Set-Cookie` attrs)
- Authenticated request with cookie succeeds WITHOUT `Authorization` header
- `/auth/me` 200 with cookie, 401 without
- Logout deletes cookie
- CSRF middleware: POST without `X-CSRF-PROTECT` → 400; POST with header → passes
- Login rate: 11th/min → 429
- Reservation rate: 6th/min → 429
- All frontend Vitest green after cookie/auth migration.
Rollback: plain `git revert` — no migration to undo; localStorage JWT restored.

## Resolved Design Decisions

- [x] **Batch 7 FK migration**: `AuditLog.UserId` nullable (FK, `OnDelete(Restrict)`) + new `string UserIdentifier` column for non-user actors (System, MercadoPago). Existing `Guid.Empty` rows get `UserId = null` + `UserIdentifier = "System"` during migration. Confirmed by user.
- [x] **Batch 6 CSRF middleware**: Exclude `POST /webhook` (MP-backed, signature-validated, not browser-fetchable). Apply `X-CSRF-PROTECT` header requirement to all other POST/PUT/PATCH/DELETE routes. Confirmed by user.