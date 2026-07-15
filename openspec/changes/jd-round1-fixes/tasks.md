# Tasks: JD Round 1 Fixes — Clear Judgment Day Findings

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 1500–2000 (backend migrations + services/tests + frontend code + frontend tests) |
| 1500-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Single PR (size-exception) |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
1500-line budget risk: High

> **Note on budget**: The orchestrator set a 1500-line review budget. This change is expected to exceed it (the proposal itself flags the single-PR review burden as medium). The user explicitly chose a single PR at the end, so the recommended path is `size-exception` with full batch-by-batch verification to keep the single review manageable.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Single PR covering all 8 batches | PR 1 | size-exception; review is linear per batch; each batch is a green commit set |

## Implementation Notes

- **Backend**: Follow strict TDD (RED → GREEN → VERIFY). Each batch starts with failing tests before implementation.
- **Frontend**: Follow Vitest (write test first, then implementation, then verify green). The project `openspec/config.yaml` currently says no frontend test runner; the orchestrator explicitly instructed Vitest, so tasks assume Vitest is available.
- **Cross-batch files**: Apply multi-batch files in the order documented in `design.md` (e.g., `Program.cs` in B1, B4, B5, B6, B7; `ReservationController.cs` in B4, B5, B6, B7; etc.).
- **Migrations**: Four migrations total: `AddUserName` (B2), `AddCurrentlyReserved` (B3), `AddReservationPurchaserEmail` + `UniqueTransactionMercadoPagoId` (B4), `AddAuditLogUserFkAndTracking` (B7). Each migration task includes create, apply, verify, and rollback.
- **Verification gate**: After each batch, `dotnet test` must pass (~333 backend). After frontend batches, `pnpm vitest` must pass (~208 frontend). No batch N+1 until batch N is green.

## Phase 1: Batch 1 — Scaffold & Config (6 reqs)

- [x] **B1.1 RED** — Write `backend/Tests/ScaffoldRemovalTests.cs` asserting 404 for `/weatherforecast` and `/api/testauthorization/*`.
  - Files: `backend/Tests/ScaffoldRemovalTests.cs`
  - Depends: none
  - Acceptance: Tests fail before scaffold removal, pass after.

- [x] **B1.2 GREEN** — Delete `backend/Controllers/TestAuthorizationController.cs`; remove `WeatherForecast` record and `/weatherforecast` endpoint from `backend/Program.cs`.
  - Files: `backend/Controllers/TestAuthorizationController.cs`, `backend/Program.cs`
  - Depends: B1.1
  - Acceptance: `GET /weatherforecast` returns 404; `GET /api/testauthorization/*` returns 404.

- [x] **B1.3 RED** — Write `backend/Tests/ConfigValidationTests.cs` covering placeholder JWT rejection, `GetRequiredValue` missing/present, `int.TryParse` fallback, password boundary (7 rejected, 8 accepted), and stacktrace redaction.
  - Files: `backend/Tests/ConfigValidationTests.cs`
  - Depends: none
  - Acceptance: All new tests fail before implementation.

- [x] **B1.4 GREEN** — Add `GetRequiredValue` helper to `backend/Program.cs`; reject `Jwt:SecretKey` that starts with `YOUR_` or is < 32 chars; replace 3 inline config checks.
  - Files: `backend/Program.cs`, `backend/appsettings.json`, `backend/appsettings.json.template`
  - Depends: B1.3
  - Acceptance: App throws at startup for placeholder/missing values; valid config starts.

- [x] **B1.5 GREEN** — Make `AuthService` use `int.TryParse` for `Jwt:ExpirationMinutes` (fallback 1440), enforce password min 8; remove `BaseAddress` from `MercadoPagoClient` constructor and set via `AddHttpClient` delegate; redact `StackTrace` in `GlobalExceptionHandler`.
  - Files: `backend/Services/AuthService.cs`, `backend/Services/MercadoPagoClient.cs`, `backend/Program.cs`, `backend/Middleware/GlobalExceptionHandler.cs`
  - Depends: B1.4
  - Acceptance: ExpirationMinutes fallback works; password 7 chars rejected; HttpClient `BaseAddress` set in delegate; logs use structured `StackTrace` property.

- [x] **B1.6 VERIFY** — Run `dotnet test`; ensure all B1 tests pass before proceeding to B2.
  - Depends: B1.2, B1.5
  - Acceptance: `dotnet test` green.

## Phase 2: Batch 2 — User Management (6 reqs) **Migration: AddUserName**

- [x] **B2.1 MIGRATION** — Add `Name` property to `User` model; configure `HasMaxLength(200)` in `ApplicationDbContext`; create EF migration `AddUserName` adding nullable `Name` column; apply and verify; test rollback.
   - Files: `backend/Models/User.cs`, `backend/Data/ApplicationDbContext.cs`, `backend/Migrations/<ts>_AddUserName.cs`
   - Depends: B1.6
   - Acceptance: Migration applies; existing rows have `Name` null; rollback drops column.

- [x] **B2.2 RED** — Update `backend/Tests/AuthenticationPropertyTests.cs` to remove public-register tests; add FsCheck tests for admin-only `POST /api/admin/users` (valid roles, non-admin 403, anon 401).
   - Files: `backend/Tests/AuthenticationPropertyTests.cs`, `backend/Tests/AdminControllerTests.cs`
   - Depends: B2.1
   - Acceptance: New tests fail before implementation.

- [x] **B2.3 GREEN** — Add `CreateUserAsync(name, email, password, role)` to `IAuthService`/`AuthService`; add shared `ValidateEmail`; remove `RegisterAsync` from `AuthService` and `POST /auth/register` from `AuthController`; create `POST /api/admin/users` in `AdminController` with `[Authorize(Policy="RequireAdminRole")]`.
   - Files: `backend/Services/IAuthService.cs`, `backend/Services/AuthService.cs`, `backend/Controllers/AuthController.cs`, `backend/Controllers/AdminController.cs`
   - Depends: B2.2
   - Acceptance: `POST /auth/register` returns 404; admin can create user with role; non-admin 403; anon 401; email validation shared.

- [x] **B2.4 RED** — Frontend tests: assert `/register` route renders 404; add admin user creation form test.
   - Files: `frontend/src/App.test.jsx` (new), `frontend/src/pages/AdminPanel.test.jsx` (extended)
   - Depends: B2.2
   - Acceptance: Tests fail before changes.

- [x] **B2.5 GREEN** — Delete `frontend/src/pages/Register.jsx` and `Register.test.jsx`; remove `/register` route from `frontend/src/App.jsx`; add admin user creation form to `frontend/src/pages/AdminPanel.jsx`.
   - Files: `frontend/src/pages/Register.jsx`, `frontend/src/App.jsx`, `frontend/src/pages/AdminPanel.jsx`
   - Depends: B2.4
   - Acceptance: `/register` shows 404; AdminPanel can create users.

- [x] **B2.6 VERIFY** — Run `dotnet test` and `pnpm vitest`; apply migration; verify all B2 tests pass.
   - Depends: B2.3, B2.5
   - Acceptance: `dotnet test` 379/379 green; `pnpm vitest` 206/206 green; migration applied.

## Phase 3: Batch 3 — Reservation Stock (5 reqs) **Migration: AddCurrentlyReserved**

- [x] **B3.1 MIGRATION** — Add `CurrentlyReserved` int (default 0) to `TicketType`; configure `IsRequired().HasDefaultValue(0)`; create migration `AddCurrentlyReserved` with **no backfill, reset to 0**; apply and verify; test rollback.
   - Files: `backend/Models/TicketType.cs`, `backend/Data/ApplicationDbContext.cs`, `backend/Migrations/<ts>_AddCurrentlyReserved.cs`
   - Depends: B2.6
   - Acceptance: Column exists with default 0; rollback drops column.

- [x] **B3.2 RED** — Write/update `ReservationServiceTests.cs` and `ReservationPropertyTests.cs` for: ExecuteUpdateAsync atomic reservation, concurrent 1-stock test (exactly one winner), FsCheck invariant `CurrentlyReserved + SoldCount <= Quantity`.
   - Files: `backend/Tests/ReservationStockTests.cs`
   - Depends: B3.1
   - Acceptance: Tests fail before implementation.

- [x] **B3.3 GREEN** — Replace `BeginTransaction + SumAsync + CountAsync + retry` in `ReservationService.CreateReservationAsync` with conditional `ExecuteUpdateAsync` on `TicketType.CurrentlyReserved`; check `rowsAffected == 0` → insufficient stock. Provider-aware: atomic for relational (PG/SQLite), transactional fallback for InMemory.
   - Files: `backend/Services/ReservationService.cs`
   - Depends: B3.2
   - Acceptance: Atomic reservation works; no oversell; 0 rows returns stock error.

- [x] **B3.4 RED** — Update `ReservationExpirationServiceTests.cs` for `async Task` signature, `PeriodicTimer` cancellation, and exception handling.
   - Files: `backend/Tests/ReservationExpirationServiceTests.cs`
   - Depends: B3.1
   - Acceptance: Tests fail before implementation.

- [x] **B3.5 GREEN** — Rewrite `ReservationExpirationService` to use `async Task ExecuteAsync` with `PeriodicTimer(TimeSpan.FromMinutes(1))`; per-reservation `ExecuteUpdateAsync` to decrement `CurrentlyReserved` (clamped to 0 with `Math.Max(0, ...)`); catch/log exceptions. Keep legacy Timer path for backward compat.
   - Files: `backend/Services/ReservationExpirationService.cs`
   - Depends: B3.4
   - Acceptance: Graceful shutdown; no process crash; expired reservations release stock.

- [x] **B3.6 RED** — Update `EventServiceTests.cs` to assert availability computed from `CurrentlyReserved`, not ticket count.
   - Files: `backend/Tests/EventServiceTests.cs`, `backend/Tests/EventManagementPropertyTests.cs`
   - Depends: B3.1
   - Acceptance: Tests fail before implementation.

- [x] **B3.7 GREEN** — Remove `.Include(e => e.Tickets)` from `EventService.GetEventByIdAsync` and `GetAllPublishedEventsAsync`; compute availability as `Quantity - CurrentlyReserved` in `MapToEventWithAvailability`.
   - Files: `backend/Services/EventService.cs`
   - Depends: B3.6
   - Acceptance: No `Include(Tickets)`; availability O(1) and correct.

- [x] **B3.8 VERIFY** — Run `dotnet test`; apply migration; confirm all B3 tests pass.
   - Depends: B3.3, B3.5, B3.7
   - Acceptance: `dotnet test` green; migration applied. 391/391 tests pass.

## Phase 4: Batch 4 — Payment Pipeline (5 reqs) **Migrations: AddReservationPurchaserEmail + UniqueTransactionMercadoPagoId**

- [x] **B4.1 MIGRATION** — Add `PurchaserEmail` to `Reservation`; change `Transaction.MercadoPagoId` index to unique; create and apply migrations `AddReservationPurchaserEmail` + `UniqueTransactionMercadoPagoId`; test rollback.
   - Files: `backend/Models/Reservation.cs`, `backend/Data/ApplicationDbContext.cs`, `backend/Migrations/<ts>_AddReservationPurchaserEmail.cs`, `backend/Migrations/<ts>_UniqueTransactionMercadoPagoId.cs`
   - Depends: B3.8
   - Acceptance: `PurchaserEmail` column nullable; unique index on `MercadoPagoId`; rollback drops both.

- [x] **B4.2 RED** — Update `ReservationControllerTests.cs` and `PaymentPropertyTests.cs` for: PurchaserEmail persisted, email mismatch 400, idempotency (duplicate → 200), concurrent duplicate handling, atomic rollback on step-2 failure, raw-bytes HMAC signature, email failure does not rollback.
   - Files: `backend/Tests/ReservationControllerTests.cs`, `backend/Tests/PaymentPropertyTests.cs`, `backend/Tests/PaymentControllerTests.cs`
   - Depends: B4.1
   - Acceptance: All tests fail before implementation.

- [x] **B4.3 GREEN** — Add `PurchaserEmail` + `ConfirmEmail` to `CreateReservationRequest`; update `ReservationService.CreateReservationAsync` to validate email match; store `PurchaserEmail` on reservation.
   - Files: `backend/Services/IReservationService.cs`, `backend/Services/ReservationService.cs`, `backend/Controllers/ReservationController.cs`, `backend/Models/Reservation.cs` (record contract)
   - Depends: B4.2
   - Acceptance: Email mismatch returns 400; persisted email equals input.

- [x] **B4.4 GREEN** — Update `TicketService.CreateTicketsAsync` to use `reservation.PurchaserEmail`; update `PaymentService.ValidateWebhookSignature` to accept `byte[] rawBody`; update `PaymentController` to read raw bytes and pass to validator.
   - Files: `backend/Services/TicketService.cs`, `backend/Services/IPaymentService.cs`, `backend/Services/PaymentService.cs`, `backend/Controllers/PaymentController.cs`
   - Depends: B4.3
   - Acceptance: Tickets use purchaser email; signature validates raw bytes; tampered body rejected.

- [x] **B4.5 GREEN** — Reorder `PaymentService.ProcessApprovedPaymentAsync`: (1) find existing `Transaction` by `MercadoPagoId` → 200; (2) wrap confirm + tickets + insert in `BeginTransactionAsync/CommitAsync`; catch `DbUpdateException` for unique violation → 200; (3) call `SendTicketEmailAsync` AFTER commit with try/catch log-only.
   - Files: `backend/Services/PaymentService.cs`, `backend/Program.cs` (WebhookSecret validation)
   - Depends: B4.4
   - Acceptance: Idempotency works; atomic rollback on failure; email failure keeps tickets.

- [x] **B4.6 GREEN** — Frontend: Update `Checkout.jsx` with double email input, paste-blocked confirm; update `CheckoutReturn.jsx` with truthful email-sent copy.
   - Files: `frontend/src/pages/Checkout.jsx`, `frontend/src/pages/CheckoutReturn.jsx`
   - Depends: B4.3
   - Acceptance: Paste blocked on confirm; mismatched emails caught; UI copy truthful.

- [x] **B4.7 VERIFY** — Run `dotnet test` and `pnpm vitest`.
   - Depends: B4.5, B4.6
   - Acceptance: `dotnet test` 400/400 green; `pnpm vitest` 211/211 green.

## Phase 5: Batch 5 — Ticket Lookup (4 reqs)

- [x] **B5.1 RED** — Update `TicketLookupPropertyTests.cs` for: response excludes QR fields, resend rate limit (4th → 429), generic response regardless of email existence, QR timestamp window boundaries.
   - Files: `backend/Tests/TicketControllerTests.cs`, `backend/Tests/TicketServiceTests.cs`
   - Depends: B4.7
   - Acceptance: Tests fail before implementation.

- [x] **B5.2 GREEN** — Modify `TicketController`: `GET /api/tickets/lookup` returns info-only DTO (no `qrCodeData`/`qrSrc`); remove `GET /api/reservations/{id}` from `ReservationController`; add `POST /api/tickets/resend` with `[EnableRateLimiting("Resend")]` accepting `{email, captchaToken}` and returning generic message.
   - Files: `backend/Controllers/TicketController.cs`, `backend/Controllers/ReservationController.cs`, `backend/Services/ITicketService.cs`, `backend/Services/IEmailService.cs` (if needed)
   - Depends: B5.1
   - Acceptance: Lookup no QR; removed endpoint 404; resend generic response.

- [x] **B5.3 GREEN** — Update `TicketService.VerifyQRCodeSignature` to validate timestamp window (`purchaseDate <= ts <= event.EndDate+24h` and `ts <= now`); add `HmacHelper` to extract timestamp from QR payload; add `ResendTicketsByEmailAsync`.
   - Files: `backend/Services/TicketService.cs`, `backend/Helpers/HmacHelper.cs`
   - Depends: B5.2
   - Acceptance: QR outside window rejected; inside window accepted; resend queues email.

- [x] **B5.4 GREEN** — Add rate limiter policy `"Resend"` (FixedWindow 3/hour keyed by email) in `Program.cs`.
   - Files: `backend/Program.cs`
   - Depends: B5.2
   - Acceptance: 4th resend within window returns 429.

- [x] **B5.5 GREEN** — Frontend: Update `TicketLookup.jsx` to info-only card (no print/download/QR); add resend form with email + CAPTCHA placeholder.
   - Files: `frontend/src/pages/TicketLookup.jsx`
   - Depends: B5.2
   - Acceptance: No QR display; resend form visible; generic message shown.

- [x] **B5.6 VERIFY** — Run `dotnet test` and `pnpm vitest` for B5.
   - Depends: B5.3, B5.4, B5.5
   - Acceptance: `dotnet test` 409/409 green; `pnpm vitest` 218/218 green.

## Phase 6: Batch 6 — Auth Session (6 reqs) **No migration**

- [x] **B6.1 RED** — Update `AuthenticationPropertyTests.cs` and create `AuthCookieTests.cs` for: login sets cookie with `HttpOnly;Secure;SameSite=Lax`, `/auth/me` 200/401, logout clears cookie, CSRF middleware rejects missing `X-CSRF-PROTECT` header, login rate limit 11th → 429, reservation rate limit 6th → 429.
   - Files: `backend/Tests/AuthCookieTests.cs`, `backend/Tests/AuthenticationPropertyTests.cs`
   - Depends: B5.6
   - Acceptance: All tests fail before implementation.

- [x] **B6.2 GREEN** — Update `AuthController`: login sets httpOnly cookie; add `GET /auth/me` returning `{id,email,name,role}`; add `POST /auth/logout` deleting cookie.
   - Files: `backend/Controllers/AuthController.cs`
   - Depends: B6.1
   - Acceptance: Cookie attributes correct; `/auth/me` works; logout clears cookie.

- [x] **B6.3 GREEN** — Update `Program.cs`: `AddJwtBearer` `OnMessageReceived` reads `token` from cookie; add `AddRateLimiter` policies `"Login"` (SlidingWindow 10/min/IP) and `"Reservations"` (FixedWindow 5/min/IP); add `app.UseRateLimiter()`; register `CsrfHeaderMiddleware`.
   - Files: `backend/Program.cs`
   - Depends: B6.1
   - Acceptance: Bearer from cookie; rate limits return 429; middleware pipeline correct.

- [x] **B6.4 GREEN** — Create `backend/Middleware/CsrfHeaderMiddleware.cs` requiring `X-CSRF-PROTECT` header on POST/PUT/PATCH/DELETE (except `POST /webhook`); allow GET/OPTIONS.
   - Files: `backend/Middleware/CsrfHeaderMiddleware.cs`
   - Depends: B6.3
   - Acceptance: Missing header → 400; webhook exempt; other mutating routes pass with header.

- [x] **B6.5 GREEN** — Add `[EnableRateLimiting("Login")]` to `AuthController.Login` and `[EnableRateLimiting("Reservations")]` to `ReservationController.CreateReservation`.
   - Files: `backend/Controllers/AuthController.cs`, `backend/Controllers/ReservationController.cs`
   - Depends: B6.3
   - Acceptance: 11th login and 6th reservation return 429.

- [x] **B6.6 GREEN** — Frontend: Rewrite `api/client.js` to remove localStorage, add `withCredentials: true`, set `X-CSRF-PROTECT` header on mutating requests; rewrite `AuthProvider` to call `GET /auth/me` on mount and after login; logout calls `POST /auth/logout`.
   - Files: `frontend/src/api/client.js`, `frontend/src/context/AuthProvider.jsx`
   - Depends: B6.2
   - Acceptance: No localStorage token operations; `/auth/me` called; CSRF header on mutating requests.

- [x] **B6.7 GREEN** — Create/update `AuthTestHelper` to authenticate via cookie + CSRF header; migrate all backend auth-using tests to use the helper.
   - Files: `backend/Tests/AuthCookieTests.cs` (inline helpers), `backend/Tests/AdminUserCreationIntegrationTests.cs`
   - Depends: B6.4
   - Acceptance: All integration tests use cookie + CSRF header.

- [x] **B6.8 GREEN** — Migrate frontend tests mocking `localStorage.getItem("token")` to mock `/auth/me` (MSW).
   - Files: all frontend tests touching auth (zero changes needed — tests already mock useAuth/apiClient)
   - Depends: B6.6
   - Acceptance: No localStorage mocks; cookie-aware test harness.

- [x] **B6.9 VERIFY** — Run `dotnet test` and `pnpm vitest` for B6.
   - Depends: B6.5, B6.7, B6.8
   - Acceptance: `dotnet test` 422/422 green; `pnpm vitest` 218/218 green.

## Phase 7: Batch 7 — Audit & Data Integrity (10 reqs) **Migration: AddAuditLogUserFkAndTracking**

- [ ] **B7.1 MIGRATION** — Add `IpAddress` and `UserAgent` to `AuditLog`; make `UserId` nullable; add FK `AuditLog.UserId → Users.Id` with `OnDelete(Restrict)`; add `UserIdentifier` string column; cleanse existing `Guid.Empty` rows to `UserId = null` + `UserIdentifier = "System"`; create migration `AddAuditLogUserFkAndTracking`; apply and verify; test rollback.
  - Files: `backend/Models/AuditLog.cs`, `backend/Data/ApplicationDbContext.cs`, `backend/Migrations/<ts>_AddAuditLogUserFkAndTracking.cs`
  - Depends: B6.9
  - Acceptance: FK applied; nullable `UserId`; `UserIdentifier` populated; rollback drops FK/columns.

- [ ] **B7.2 RED** — Update `MetricsPropertyTests.cs` and `MetricsControllerTests.cs` to assert single `GroupBy` query (query count = 1 regardless of event count).
  - Files: `backend/Tests/MetricsPropertyTests.cs`, `backend/Tests/MetricsControllerTests.cs`
  - Depends: B6.9
  - Acceptance: Tests fail before implementation.

- [ ] **B7.3 GREEN** — Consolidate `MetricsService.GetOrganizerMetricsAsync` to a single `GroupBy(eventId)` projection returning all aggregates.
  - Files: `backend/Services/MetricsService.cs`
  - Depends: B7.2
  - Acceptance: One round-trip; no per-event loops.

- [ ] **B7.4 RED** — Update tests for: `AdminService.GetAllLogsAsync` pagination, FK constraint + restrict, out-of-band audit failure, `TryGetUserRole` false on parse failure, webhook "System" identifier, reservation token expiry, PII redaction, IP/UA capture, EventOwnershipHandler parameter.
  - Files: `backend/Tests/AdminPropertyTests.cs`, `backend/Tests/AuditLogTests.cs`, `backend/Tests/EventControllerTests.cs`, `backend/Tests/TicketLookupPropertyTests.cs`, `backend/Tests/ReservationPropertyTests.cs`
  - Depends: B7.1
  - Acceptance: All tests fail before implementation.

- [ ] **B7.5 GREEN** — Update `AdminService.GetAllLogsAsync(int page, int pageSize = 50)` to return `PagedResult<AuditLogDto>`; update `AuditLogService` to wrap writes in try/catch (out-of-band failure), capture IP/UA from `IHttpContextAccessor`.
  - Files: `backend/Services/AdminService.cs`, `backend/Services/AuditLogService.cs`, `backend/Models/AuditLogDto.cs` (if new)
  - Depends: B7.4
  - Acceptance: Pagination correct; audit failure doesn't break primary op; IP/UA captured.

- [ ] **B7.6 GREEN** — Update controllers to populate `AuditLog` with `UserIdentifier` ("System" for webhooks); capture `ClientIp` and `UserAgent` on guest reservation creation.
  - Files: `backend/Controllers/PaymentController.cs`, `backend/Controllers/ReservationController.cs`, `backend/Models/Reservation.cs` (optional ClientIp/UA)
  - Depends: B7.5
  - Acceptance: Webhook audit uses "System"; reservation stores IP/UA.

- [ ] **B7.7 GREEN** — Update `ReservationService` HMAC token format to `nonce:timestamp:signature`; add expiry validation; reject expired/tampered tokens.
  - Files: `backend/Services/ReservationService.cs`, `backend/Helpers/HmacHelper.cs`
  - Depends: B7.4
  - Acceptance: Expired token rejected; valid nonce/timestamp accepted.

- [ ] **B7.8 GREEN** — Update `TicketService` to use `LogRedactor.HashIdentifier` for email + DNI in logs; update `EventController.TryGetUserRole` to return `false` on parse failure.
  - Files: `backend/Services/TicketService.cs`, `backend/Controllers/EventController.cs`
  - Depends: B7.4
  - Acceptance: Logs contain no raw email/DNI; invalid role claim returns false.

- [ ] **B7.9 GREEN** — Update `EventOwnershipRequirement` to carry `RouteParameterName`; update `EventOwnershipHandler` to read `routeValues[requirement.RouteParameterName]`.
  - Files: `backend/Authorization/EventOwnershipRequirement.cs`, `backend/Authorization/EventOwnershipHandler.cs`, `backend/Controllers/EventController.cs` (policy usage)
  - Depends: B7.4
  - Acceptance: Works with `id` and `eventId` route parameters.

- [ ] **B7.10 VERIFY** — Run `dotnet test`; apply migration; confirm all B7 tests pass.
  - Depends: B7.3, B7.6, B7.7, B7.8, B7.9
  - Acceptance: `dotnet test` green; migration applied.

## Phase 8: Batch 8 — Frontend Quality (13 reqs)

- [ ] **B8.1 RED** — Write `frontend/src/lib/__tests__/format.test.js` and `apiError.test.js` for `formatEventDate`, `formatCurrency`, `getErrorMessage`.
  - Files: `frontend/src/lib/__tests__/format.test.js`, `frontend/src/lib/__tests__/apiError.test.js`
  - Depends: B7.10
  - Acceptance: Tests fail before implementation.

- [ ] **B8.2 GREEN** — Create `frontend/src/lib/format.js` with `formatEventDate` and `formatCurrency`; create `frontend/src/lib/apiError.js` with `getErrorMessage`; replace inline implementations in 7+ consuming files.
  - Files: `frontend/src/lib/format.js`, `frontend/src/lib/apiError.js`, 7+ consuming files
  - Depends: B8.1
  - Acceptance: All consumers import shared utils; no duplicated inline formatters.

- [ ] **B8.3 RED** — Write/update component tests: `RoleGuard` 403 render, `EventForm` undefined eventId + catch-block feedback, `Modal` dynamic focus trap, `ToastProvider` remount `nextId`, `StaffScan` GUID validation + sessionStorage, `OrganizerEventDetail` correct fetch URL, `ErrorBoundary` fallback, `Card` prop filter, `EventList` native button, `NotFound` home link.
  - Files: per-component test files under `frontend/src/components/__tests__/` and `frontend/src/pages/__tests__/`
  - Depends: B7.10
  - Acceptance: Tests fail before implementation.

- [ ] **B8.4 GREEN** — Update `RoleGuard.jsx` to render 403 page (no redirect); update `EventForm.jsx` to validate `eventId` before PUT and fix catch feedback to `error`/`warning`; remove explicit `Content-Type`.
  - Files: `frontend/src/components/RoleGuard.jsx`, `frontend/src/components/EventForm.jsx`
  - Depends: B8.3
  - Acceptance: 403 page shown; PUT blocked when eventId undefined; upload error shown as error.

- [ ] **B8.5 GREEN** — Update `Modal.jsx` to re-evaluate focusable nodes on each Tab; update `ToastProvider.jsx` to use `useRef` for `nextId`.
  - Files: `frontend/src/components/Modal.jsx`, `frontend/src/context/ToastProvider.jsx`
  - Depends: B8.3
  - Acceptance: Dynamic focusables included; nextId resets on remount.

- [ ] **B8.6 GREEN** — Update `StaffScan.jsx`: GUID regex validation before API call; `useRef` for scanner with cleanup; `sessionStorage` for scan history.
  - Files: `frontend/src/pages/StaffScan.jsx`
  - Depends: B8.3
  - Acceptance: Invalid GUID rejected; history persists across refresh.

- [ ] **B8.7 GREEN** — Update `OrganizerEventDetail.jsx` to use `GET /events/{id}/manage`; update `Card.jsx` to filter unknown props; update `EventList.jsx` to use native `<button>`; update `NotFound.jsx` to add home link.
  - Files: `frontend/src/pages/OrganizerEventDetail.jsx`, `frontend/src/components/Card.jsx`, `frontend/src/pages/EventList.jsx`, `frontend/src/pages/NotFound.jsx`
  - Depends: B8.3
  - Acceptance: Authenticated endpoint used; no arbitrary props on DOM; native buttons; home link present.

- [ ] **B8.8 GREEN** — Create `frontend/src/components/ErrorBoundary.jsx`; update `App.jsx` to wrap routes with `ErrorBoundary`.
  - Files: `frontend/src/components/ErrorBoundary.jsx`, `frontend/src/App.jsx`
  - Depends: B8.3
  - Acceptance: Throwing route caught by boundary; other routes functional.

- [ ] **B8.9 GREEN** — Update `frontend/src/components/__tests__/accessibility.test.jsx` to explicitly import `vi` from `vitest`.
  - Files: `frontend/src/components/__tests__/accessibility.test.jsx`
  - Depends: B8.3
  - Acceptance: `import { vi } from 'vitest'` present.

- [ ] **B8.10 VERIFY** — Run `pnpm vitest` and lint; confirm all B8 tests pass.
  - Depends: B8.2, B8.4, B8.5, B8.6, B8.7, B8.8, B8.9
  - Acceptance: `pnpm vitest` green; no new lint errors.

## Phase 9: Cross-Batch Verification & Cleanup

- [ ] **CB.1 VERIFY** — Run full backend test suite (`dotnet test`) with all four migrations applied; confirm ~333 tests green.
  - Depends: B1.6, B2.6, B3.8, B4.7, B5.6, B6.9, B7.10
  - Acceptance: `dotnet test` green.

- [ ] **CB.2 VERIFY** — Run full frontend test suite (`pnpm vitest`) after all frontend batches; confirm ~208 tests green.
  - Depends: B2.6, B4.7, B5.6, B6.9, B8.10
  - Acceptance: `pnpm vitest` green.

- [ ] **CB.3 VERIFY** — Run integration smoke test: login → create reservation → process payment webhook → confirm tickets → email sent → public lookup → resend.
  - Depends: B4.7, B5.6, B6.9, B7.10
  - Acceptance: End-to-end flow succeeds; no 500s; correct data in DB.

- [ ] **CB.4 DOCUMENT** — Update README/deployment notes for httpOnly cookie, migration sequence, and rollback plan; add `TODO` comment for Turnstile integration in `TicketController`/`TicketLookup.jsx`.
  - Files: `README.md` or `DEPLOYMENT.md`, `backend/Controllers/TicketController.cs`, `frontend/src/pages/TicketLookup.jsx`
  - Depends: B5.2, B6.6
  - Acceptance: Docs describe migrations, rollback, and cookie behavior; Turnstile TODO visible.

- [ ] **CB.5 ROLLBACK VERIFICATION** — For each of the 4 migrations, verify `dotnet ef database update <previous-migration>` succeeds and leaves schema in a valid state.
  - Depends: B2.1, B3.1, B4.1, B7.1
  - Acceptance: All rollback paths tested successfully.

- [ ] **CB.6 SINGLE PR PREP** — Rebase all batch commits into a single reviewable branch; verify final diff stays within the 1500-line budget as closely as possible; prepare PR description referencing all 55 requirements and 8 batches.
  - Depends: CB.1, CB.2, CB.3
  - Acceptance: Single PR ready; all tests green; no uncommitted migration files.
