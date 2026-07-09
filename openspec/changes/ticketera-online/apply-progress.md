# Apply Progress: Ticketera Online MVP — Tasks 12.1-12.7 + Task 14

## Summary

Implemented Task 14 (email service with Resend integration) on top of the previously completed Task 12.1-12.6 payment-service slice. Task 12.7 remains deferred. The backend test suite now has **223 passing tests** (+12 new from Task 14), with the one pre-existing flaky `VerifyDatabaseSchema` test still failing due to live Supabase connectivity.

## Completed Tasks

- [x] 12. Implement payment service with Mercado Pago integration
  - [x] 12.1 Create IPaymentService interface and implementation
  - [x] 12.2 Implement webhook processing
  - [x] 12.3 Implement refund functionality
  - [x] 12.4 Create PaymentController with endpoints
  - [x] 12.5 Write property tests for payment processing
  - [x] 12.6 Fix purchaser DNI on ticket creation from payment webhook
  - [x] 12.6 remediation A — Remove PurchaserDNI from ReservationResponse (PII)
  - [x] 12.6 remediation C — Add tests for DNI validation branches
- [ ] 12.7 Guard purchaser DNI sentinel in payment webhook (deferred)

- [x] 14. Implement email service with Resend integration
  - [x] 14.1 Create IEmailService interface and implementation
  - [x] 14.2 Create email templates
  - [x] 14.3 Write property tests for email delivery

## Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Models/Reservation.cs` | Modified | Added `PurchaserDNI` (string, required) |
| `backend/Data/ApplicationDbContext.cs` | Modified | Configured `Reservation.PurchaserDNI` as `IsRequired().HasMaxLength(50)` |
| `backend/Migrations/20260707141857_AddReservationPurchaserDNI.cs` | Created | EF Core migration adding `PurchaserDNI` column with default `"00000000"` |
| `backend/Migrations/20260707141857_AddReservationPurchaserDNI.Designer.cs` | Created | Auto-generated migration designer snapshot |
| `backend/Migrations/ApplicationDbContextModelSnapshot.cs` | Modified | Snapshot updated with new column |
| `backend/Services/IReservationService.cs` | Modified | Added `purchaserDNI` parameter to `CreateReservationAsync`; added `PurchaserDNI` to `CreateReservationRequest`; removed from `ReservationResponse` |
| `backend/Services/ReservationService.cs` | Modified | Validates and stores `PurchaserDNI` on reservation creation |
| `backend/Controllers/ReservationController.cs` | Modified | Passes `request.PurchaserDNI` to service; no longer returns DNI in response body |
| `backend/Services/PaymentService.cs` | Modified | `ProcessApprovedPaymentAsync` now passes `reservation.PurchaserDNI` instead of `"00000000"` |
| `backend/Tests/ReservationServiceTests.cs` | Modified | Added DNI storage test; added empty/whitespace/null/over-50/exactly-50 validation tests |
| `backend/Tests/ReservationPropertyTests.cs` | Modified | Updated all `CreateReservationAsync` calls to pass a real DNI |
| `backend/Tests/ReservationControllerTests.cs` | Modified | Removed response DNI assertion; added 400 contract test for omitted DNI |
| `backend/Tests/ReservationExpirationServiceTests.cs` | Modified | Updated `CreateReservationAsync` call to pass DNI |
| `backend/Tests/PaymentPropertyTests.cs` | Modified | Reservation test data now carries a real DNI; added regression test proving webhook-created tickets carry the reservation DNI and are lookupable |
| `backend/Services/IEmailService.cs` | Created | `IEmailService` interface with `SendTicketEmailAsync` and `SendRefundNotificationAsync`; `EmailResult` DTO |
| `backend/Services/EmailService.cs` | Created | Resend-backed implementation with QR image embedding, retry logic, and structured logging |
| `backend/Services/IResendClient.cs` | Created | `IResendClient` abstraction plus `ResendEmailRequest`/`ResendEmailResponse` DTOs |
| `backend/Services/ResendClient.cs` | Created | HttpClient-based Resend API client |
| `backend/Services/ResendOptions.cs` | Created | Configuration options (ApiKey, FromEmail, MaxRetryAttempts, RetryDelayMilliseconds) |
| `backend/Services/Templates/TicketConfirmationTemplate.cs` | Created | HTML email template for ticket confirmations with embedded QR codes |
| `backend/Services/Templates/RefundNotificationTemplate.cs` | Created | HTML email template for refund notifications |
| `backend/Services/Templates/HtmlEncoder.cs` | Created | Shared minimal HTML entity encoder |
| `backend/Tests/EmailPropertyTests.cs` | Created | Property tests for Properties 22, 23, 24, 25, 40 |
| `backend/Program.cs` | Modified | Registered `ResendOptions`, `IResendClient`/`ResendClient`, and `IEmailService`/`EmailService` |
| `backend/appsettings.json` | Modified | Added `Resend:FromEmail` configuration key |
| `openspec/changes/ticketera-online/tasks.md` | Modified | Marked Tasks 14.1, 14.2, 14.3 complete |

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 12.1 | `Tests/PaymentPropertyTests.cs` | Unit | 188/188 (flaky excluded) | Written | Passed | 2 cases (valid + expired) | Clean |
| 12.2 | `Tests/PaymentPropertyTests.cs` | Unit | 188/188 (flaky excluded) | Written | Passed | 3 cases (approved, rejected, invalid signature) | Clean |
| 12.3 | `Tests/PaymentPropertyTests.cs` | Unit | 188/188 (flaky excluded) | Written | Passed | 1 case (refund logs transaction) | Clean |
| 12.4 | `Tests/PaymentControllerTests.cs` | Unit | 188/188 (flaky excluded) | Written | Passed | 5 cases (OK, 404, 400, valid webhook, invalid signature) | Clean |
| 12.5 | `Tests/PaymentPropertyTests.cs` | Unit | 188/188 (flaky excluded) | Written | Passed | FsCheck imports + multi-scenario facts | Clean |
| 12.6 | `Tests/ReservationServiceTests.cs`, `Tests/PaymentPropertyTests.cs` | Unit | 202/202 (flaky excluded) | Written | Passed | DNI storage + webhook→lookup regression | Clean |
| 12.6-A | `Tests/ReservationControllerTests.cs` | Unit | 204/204 (flaky excluded) | Tests adjusted first | Passed | N/A — removal of PII field | Clean |
| 12.6-C | `Tests/ReservationServiceTests.cs`, `Tests/ReservationControllerTests.cs` | Unit | 204/204 (flaky excluded) | Tests written first | Passed | 6 service cases (empty, whitespace, tab/newline, null, 51 chars, 50 chars) + 1 controller 400 case | Clean |
| 14.1 | `Tests/EmailPropertyTests.cs` | Unit | 211/211 (flaky excluded) | Written | Passed | Multi-ticket + single-ticket + per-ticket QR generation | Extracted `HtmlEncoder`, no Polly dependency |
| 14.2 | `Tests/EmailPropertyTests.cs` | Unit | 211/211 (flaky excluded) | Written | Passed | Ticket + refund templates with HTML escaping | Extracted `HtmlEncoder` |
| 14.3 | `Tests/EmailPropertyTests.cs` | Unit | 211/211 (flaky excluded) | Written | Passed | 12 cases covering Properties 22-25 and 40 | Clean |

## Test Summary

- **Total tests passing**: 223
- **Baseline passing (before Task 14)**: 211
- **Net new passing**: +12
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: `HtmlEncoder.Escape`, template renderers

## Deviations from Design

1. Added `Guid reservationId` parameter to `InitiateRefundAsync` so the refund transaction can be associated with the correct reservation. The design.md interface omits this parameter, but the `Transaction` entity requires a non-null `ReservationId`. (Carried forward from Task 12.1-12.5.)
2. Webhook payload model is simplified (`PaymentId`, `ExternalReference`, `Status`) rather than fetching full payment details from Mercado Pago. This matches the design.md HMAC signature validation example. (Carried forward from Task 12.1-12.5.)
3. No DNI backfill script was added — the migration uses a non-null default `"00000000"` for existing rows, which is acceptable for fresh dev DBs per the task scope.
4. Email template rendering uses static C# template classes rather than Razor or resource files. This matches the project's minimal pattern and keeps the implementation testable without additional dependencies. (Justified divergence — design.md did not prescribe a template engine.)

## Issues Found

- The pre-existing `VerifyDatabaseSchema` test fails because the live Supabase tenant/user is not reachable from this environment. Not addressed per instructions.
- Existing reservations created before Task 12.6 will have `PurchaserDNI = "00000000"` after migration; any webhook processed against them would still produce placeholder-DNI tickets. Deferred as Task 12.7.

## Notable Discoveries

- **Resend SDK choice**: No official Resend NuGet package is referenced in the project. Implemented a thin `IResendClient`/`ResendClient` HttpClient wrapper to avoid adding a new dependency and to keep the service fully mockable in tests.
- **Retry policy**: Implemented in-memory exponential backoff retry inside `EmailService` rather than introducing Polly (not in `.csproj`). Retry count and base delay are configurable via `ResendOptions`.
- **Template rendering**: Static HTML string builders in `Services/Templates/` with a shared `HtmlEncoder` for escaping. QR codes are embedded as `data:image/png;base64,...` `<img>` tags generated by `ITicketService.GenerateQRCodeImage`.
- **Culture handling**: Currency and dates are rendered with `CultureInfo.InvariantCulture` in templates to avoid locale-dependent formatting in email bodies. Tests assert invariant formats.

## Commits

No commits made in this batch. The orchestrator owns commit and PR after re-verification.

Recommended work-unit commits (per `work-unit-commits` skill):
1. `feat(email): add Resend client abstraction and configuration`
   - `IResendClient`, `ResendClient`, `ResendEmailRequest`/`Response`, `ResendOptions`, `Program.cs` + `appsettings.json` registration
2. `feat(email): add email service with ticket and refund templates`
   - `IEmailService`, `EmailService`, `EmailResult`, `TicketConfirmationTemplate`, `RefundNotificationTemplate`, `HtmlEncoder`
3. `test(email): add property tests for email delivery`
   - `EmailPropertyTests.cs` covering Properties 22-25 and 40

## Verification

- `dotnet test` backend result: 223 passing, 1 pre-existing flaky failure (`VerifyDatabaseSchema`).
- Regression test `Property15_ApprovedWebhook_TicketsCarryReservationDNIAndAreLookupable` (from Task 12.6) still passes.
- New email property tests all pass (12/12).

## Next Recommended Phase

`sdd-verify` for the full Task 14 slice, then proceed to Task 15 (metrics service) or Task 12.7 hardening.

---

## Task 14 4R-fix: merge-blocking review findings

### Completed Fixes

- [x] R4-B1 HTML-escaping regression tests
- [x] R4-B2 Edge-case tests
- [x] R2-B1 ResendOptions startup validation
- [x] R3-W1 Dead imports cleanup

### Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Tests/EmailPropertyTests.cs` | Modified | Added 4 property tests covering HTML escaping and edge cases. |
| `backend/Program.cs` | Modified | Added fail-fast validation for `ResendOptions.ApiKey` and `ResendOptions.FromEmail`. |
| `backend/Services/EmailService.cs` | Modified | Removed unused `using System.Globalization;` and `using System.Text;`. |

### TDD Cycle Evidence

| Fix | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-----|-----------|-------|------------|-----|-------|-------------|----------|
| R4-B1 | `Tests/EmailPropertyTests.cs` | Unit | 12/12 | Written | Passed (impl already escaped) | 2 cases (ticket email + refund reason) | Clean |
| R4-B2 | `Tests/EmailPropertyTests.cs` | Unit | 12/12 | Written | Passed (defensive branches already existed) | 2 cases (empty list + null `TicketType`) | Clean |
| R2-B1 | N/A | N/A | N/A | N/A | Build passes | N/A | Clean |
| R3-W1 | N/A | N/A | N/A | N/A | Build passes | N/A | Clean |

### Test Summary

- **Total tests passing**: 227
- **Baseline before 4R-fix**: 223
- **Net new passing**: +4
- **Pre-existing flaky failure**: `VerifyDatabaseSchema` (Supabase tenant/user not reachable from this environment)

### Deviations from Design

None — implementation matches design; only tests and startup validation were added.

### Issues Found

- One full-suite run showed a transient failure in `QRCodePropertyTests.Property21_SignatureVerification_RejectsTamperedData`; re-running the test in isolation and the full suite again passed. This appears to be a pre-existing flaky/intermittent test, not caused by the 4R-fix changes.
- `VerifyDatabaseSchema` continues to fail due to live Supabase connectivity, as documented previously.

### Verification

- `dotnet test --filter FullyQualifiedName~EmailPropertyTests`: 16/16 passing.
- `dotnet test --verbosity normal`: 227 passing, 1 pre-existing flaky failure.

### Commits

- `fix(email): cubre tests de HTML escaping, edgecases y valida ResendOptions al arranque`

---

## Task 15: Metrics service for organizer dashboard

### Completed Tasks

- [x] 15.1 Create IMetricsService interface and implementation
- [x] 15.2 Create MetricsController with endpoints
- [x] 15.3 Write property tests for metrics calculations

### Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Services/IMetricsService.cs` | Created | `IMetricsService` interface with `GetEventMetricsAsync` and `GetOrganizerMetricsAsync`; `EventMetrics` DTO with `Id`, `EventId`, `EventName`, `EventDate`, `TicketsSold`, `TotalRevenue`, `RemainingInventory`, `TicketsScanned`. |
| `backend/Services/MetricsService.cs` | Created | EF Core-backed implementation calculating tickets sold, revenue, remaining inventory (total quantity − sold − active non-expired reservations), and scanned tickets in real time. |
| `backend/Controllers/MetricsController.cs` | Created | `GET /api/metrics/events/{id}` with `[Authorize(Policy = "EventOwnership")]`; `GET /api/metrics/organizer` with `[Authorize(Policy = "RequireOrganizadorRole")]`. |
| `backend/Program.cs` | Modified | Registered `IMetricsService`/`MetricsService` as scoped. |
| `backend/Tests/MetricsPropertyTests.cs` | Created | Property tests for Properties 33-37 plus edge cases (no events, no sales, expired reservations, multiple ticket types, non-existent event). |
| `backend/Tests/MetricsControllerTests.cs` | Created | Controller unit tests for OK, 404, 401, 500, Admin role access, and organizer metrics list. |
| `openspec/changes/ticketera-online/tasks.md` | Modified | Marked Tasks 15.1, 15.2, 15.3 complete. |

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 15.1/15.3 | `Tests/MetricsPropertyTests.cs` | Unit | 228/228 (flaky excluded) | Written | Passed | 10 cases (Properties 33-37 + edge cases) | Clean |
| 15.2 | `Tests/MetricsControllerTests.cs` | Unit | 238/238 (flaky excluded) | Written | Passed | 7 cases (OK, 404, 401, 500, Admin, list, no user) | Clean |

### Test Summary

- **Total tests passing**: 245
- **Baseline passing (before Task 15)**: 228
- **Net new passing**: +17
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: `CalculateMetricsAsync` is deterministic but depends on DbContext queries

### Deviations from Design

1. `EventMetrics` DTO includes additional fields (`Id`, `EventName`, `EventDate`) beyond the design.md shape (`EventId`, `TotalTicketsSold`, `TotalRevenue`, `RemainingInventory`, `TicketsScanned`). This matches the task specification for Task 15.1 and the organizer endpoint response example in design.md, which includes `eventName`.
2. Remaining inventory calculation subtracts active non-expired reservations (`Status == Active && ExpiresAt > UtcNow`) rather than just `Status == Active`. This aligns with the existing `ReservationService` definition of "active" and prevents expired reservations from incorrectly reducing inventory.

### Issues Found

- The pre-existing `VerifyDatabaseSchema` test did not fail in this batch (the full suite passed cleanly), but it remains dependent on live Supabase connectivity and may flake in environments without access.

### Verification

- `dotnet test --filter FullyQualifiedName~MetricsPropertyTests`: 10/10 passing.
- `dotnet test --filter FullyQualifiedName~MetricsControllerTests`: 7/7 passing.
- `dotnet test` full suite: 245 passing, 0 failed.

### Commits

No commits made in this batch. The orchestrator owns commit and PR after re-verification.

Recommended work-unit commits (per `work-unit-commits` skill):
1. `feat(metrics): add IMetricsService interface and EventMetrics DTO`
   - `backend/Services/IMetricsService.cs`
2. `feat(metrics): implement real-time metrics calculations`
   - `backend/Services/MetricsService.cs`
3. `feat(metrics): add authorized metrics endpoints`
   - `backend/Controllers/MetricsController.cs`, `backend/Program.cs` registration
4. `test(metrics): add property and controller tests`
   - `backend/Tests/MetricsPropertyTests.cs`, `backend/Tests/MetricsControllerTests.cs`

### Next Recommended Phase

`sdd-verify` for the full Task 15 slice.

---

## Task 16: Admin endpoints and audit logging

### Completed Tasks

- [x] 16.1 Create AdminController with system-wide endpoints
- [x] 16.2 Implement audit logging for admin actions
- [x] 16.3 Write property tests for admin capabilities

### Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Controllers/AdminController.cs` | Created | `AdminController` with `GET /api/admin/users` and `GET /api/admin/events`, both protected by `[Authorize(Policy = "RequireAdminRole")]`. Logs audit entries for each view action. |
| `backend/Services/IAdminService.cs` | Created | `IAdminService` interface plus `UserSummary` and `EventSummary` DTOs. |
| `backend/Services/AdminService.cs` | Created | EF Core-backed implementation returning all users and all events regardless of ownership; `UserSummary.PasswordHash` is explicitly nulled. |
| `backend/Services/IAuditLogService.cs` | Created | `IAuditLogService` interface with `LogActionAsync`, `GetAllLogsAsync`, and `GetLogsForUserAsync`; `AuditLogEntry` DTO. |
| `backend/Services/AuditLogService.cs` | Created | EF Core-backed audit log writer/reader. |
| `backend/Models/AuditLog.cs` | Created | `AuditLog` entity: `Id`, `UserId`, `ActionType`, `ResourceType`, `ResourceId`, `Details`, `Timestamp`. |
| `backend/Data/ApplicationDbContext.cs` | Modified | Added `DbSet<AuditLog>` and entity configuration with indexes on `UserId`, `ActionType`, and `Timestamp`. |
| `backend/Migrations/20260708230158_AddAuditLog.cs` | Created | EF Core migration creating the `AuditLogs` table with required indexes. |
| `backend/Migrations/20260708230158_AddAuditLog.Designer.cs` | Created | Auto-generated migration designer snapshot. |
| `backend/Migrations/ApplicationDbContextModelSnapshot.cs` | Modified | Snapshot updated with `AuditLog` entity. |
| `backend/Program.cs` | Modified | Registered `IAdminService`/`AdminService` and `IAuditLogService`/`AuditLogService` as scoped. |
| `backend/Tests/AdminPropertyTests.cs` | Created | Property tests for Properties 42 (admin access to all events) and 43 (audit logging) plus user-list and password-hash security cases. |
| `backend/Tests/AdminControllerTests.cs` | Created | Controller unit tests for OK, 401, and 500 paths for both admin endpoints, including audit-log verification. |
| `openspec/changes/ticketera-online/tasks.md` | Modified | Marked Tasks 16.1, 16.2, 16.3 complete. |

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 16.1/16.3 | `Tests/AdminPropertyTests.cs` | Unit | 245/245 | Written | Passed | 3 cases (all events, no events, ownership metadata) | Extracted `CreateEvent` helper |
| 16.1/16.3 | `Tests/AdminControllerTests.cs` | Unit | 245/245 | Written | Passed | 5 cases (users OK, events OK, events 401, both 500, audit verify) | Clean |
| 16.2/16.3 | `Tests/AdminPropertyTests.cs` | Unit | 245/245 | Written | Passed | 4 cases (view log, multiple actions, different admins, user list without password hash) | Clean |

### Test Summary

- **Total tests passing**: 258
- **Baseline passing (before Task 16)**: 245
- **Net new passing**: +13
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: `MapToEntry` (audit log mapping)

### Deviations from Design

1. `UserSummary` includes a nullable `PasswordHash` property that is always set to `null` by `AdminService`. This lets the test explicitly verify the password hash is not exposed, while keeping the DTO serializable. A stricter alternative would be to omit the property entirely; the current approach is a deliberate trade-off for testability.
2. Admin modify/delete actions on events are authorized through the existing `EventOwnership` policy (which allows Admin), but audit logging is currently emitted only for the new `AdminController` view endpoints. The audit service interface supports any action type, so extending coverage to `EventService` admin paths is a future slice if required.
3. `AuditLog.ResourceId` is nullable to support collection-level actions such as "view all users" or "view all events" where no single resource ID exists.

### Issues Found

- None. Full suite passed cleanly; `VerifyDatabaseSchema` did not fail in this batch.

### Verification

- `dotnet test --filter FullyQualifiedName~AdminPropertyTests`: 8/8 passing.
- `dotnet test --filter FullyQualifiedName~AdminControllerTests`: 5/5 passing.
- `dotnet test` full suite: 258 passing, 0 failed.

### Commits

No commits made in this batch. The orchestrator owns commit and PR after re-verification.

Recommended work-unit commits (per `work-unit-commits` skill):
1. `feat(admin): add audit log entity and EF migration`
   - `backend/Models/AuditLog.cs`, `backend/Data/ApplicationDbContext.cs`, migration files, snapshot update
2. `feat(admin): add audit log service`
   - `backend/Services/IAuditLogService.cs`, `backend/Services/AuditLogService.cs`
3. `feat(admin): add admin service and controller with system-wide endpoints`
   - `backend/Services/IAdminService.cs`, `backend/Services/AdminService.cs`, `backend/Controllers/AdminController.cs`, `backend/Program.cs` registration
4. `test(admin): add property and controller tests for admin capabilities`
   - `backend/Tests/AdminPropertyTests.cs`, `backend/Tests/AdminControllerTests.cs`

### Next Recommended Phase

`sdd-verify` for the full Task 16 slice.

---

## Task 16.4: Harden admin endpoints and audit coverage (post-4R review)

### Completed Tasks

- [x] 16.4 Harden admin endpoints and audit coverage (post-4R review)
  - Introduce `AuditActionType` and `AuditResourceType` enums with EF Core string conversions.
  - Add `AuditLogContext`, best-effort audit logging with `ILogger`, and deterministic log ordering (`Timestamp desc, Id desc`).
  - Paginate `GET /api/admin/users` and `GET /api/admin/events` with a hard 200-row cap.
  - Add `GET /api/admin/audit-logs` with optional `userId` filter.
  - Create `TicketeraControllerBase` for shared `TryGetUserId` helper and remove duplicated controller code.
  - Wire audit logging into `EventController` admin update/delete paths.
  - Update and expand `AdminControllerTests`, `EventControllerTests`, and `AdminPropertyTests` for new behavior and FsCheck v3 API.

### Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Models/AuditLog.cs` | Modified | Added `AuditActionType` and `AuditResourceType` enums; `AuditLog` uses enums for `ActionType`/`ResourceType`. |
| `backend/Data/ApplicationDbContext.cs` | Modified | Added `.HasConversion<string>()` for audit enums and `MaxLength` constraints. |
| `backend/Services/IAuditLogService.cs` | Modified | Replaced `AuditLogEntry` DTO with `AuditLogContext` record; added `GetAllLogsAsync`/`GetLogsForUserAsync` using enums. |
| `backend/Services/AuditLogService.cs` | Modified | Added `ILogger<AuditLogService>`; catches audit persistence exceptions and logs warnings; orders by `Timestamp desc, Id desc`. |
| `backend/Services/PagedResult.cs` | Created | Generic paginated result shape (`Items`, `Total`, `Page`, `PageSize`). |
| `backend/Services/IAdminService.cs` | Modified | `GetAllUsersAsync`/`GetAllEventsAsync` now return `PagedResult<T>` and accept `page`/`pageSize`. |
| `backend/Services/AdminService.cs` | Modified | Implements pagination with a hard 200-row cap; `UserSummary` no longer exposes `PasswordHash`. |
| `backend/Controllers/TicketeraControllerBase.cs` | Created | Shared controller base with `TryGetUserId` helper and common `Problem` helpers. |
| `backend/Controllers/AdminController.cs` | Modified | Uses paginated service results; adds `GET /api/admin/audit-logs` with optional `userId` filter. |
| `backend/Controllers/EventController.cs` | Modified | Inherits from `TicketeraControllerBase`; audits admin update/delete actions with best-effort failure handling. |
| `backend/Controllers/MetricsController.cs` | Modified | Inherits from `TicketeraControllerBase`; removed duplicated `TryGetUserId`. |
| `backend/Tests/AdminControllerTests.cs` | Modified | Updated for paginated DTOs; added audit-failure, audit-logs endpoint, and JSON password-hash contract tests. |
| `backend/Tests/EventControllerTests.cs` | Created | Unit tests for admin update/delete audit logging and audit-failure paths. |
| `backend/Tests/AdminPropertyTests.cs` | Modified | Rewritten using FsCheck v3 Fluent API (`GenStatic` alias + `GenLinq` query syntax); added edge-case facts and deterministic ordering test. |
| `openspec/changes/ticketera-online/tasks.md` | Modified | Added and marked Task 16.4 complete. |
| `openspec/changes/ticketera-online/design.md` | Modified | Added audit-write atomicity note to Admin Panel Component. |

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 16.4 | `Tests/AdminPropertyTests.cs` | Unit | 273/273 | Written (compile fix) | Passed | FsCheck v3 Fluent API + edge cases | `GenLinq` query-syntax helpers |
| 16.4 | `Tests/AdminControllerTests.cs` | Unit | 273/273 | Written | Passed | Pagination, audit-logs endpoint, audit failure | Clean |
| 16.4 | `Tests/EventControllerTests.cs` | Unit | 273/273 | Written | Passed | Admin update/delete audit + failure paths | Clean |

### Test Summary

- **Total tests passing**: 273
- **Baseline passing (before Task 16.4)**: 258
- **Net new passing**: +15
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: `GuidGen`, `SafeStringGen`, `BuildScenario`

### Deviations from Design

1. Admin audit logging for modify/delete is emitted from the controller layer (`EventController`) rather than inside `EventService`. This preserves the existing service API and keeps the audit write best-effort; a future slice could move it into the service/UoW if full atomicity is required.
2. Pagination uses a hard 200-row cap on `pageSize` instead of configurable max; this is a deliberate guard against accidental large result sets.

### Issues Found

- `AdminPropertyTests.cs` initially failed to compile against FsCheck v3 because the original code used the F# module API (`map`, `bind`, `forAll`, etc.) and lowercase identifiers. Resolved by switching to the FsCheck.Fluent API and adding small LINQ-query helper extension methods (`GenLinq`).

### Verification

- `dotnet test backend/TicketeraOnline.Api.csproj --no-build`: 273 passing, 0 failed, 0 skipped.

### Commits

No commits made in this batch. The orchestrator owns commit and PR after re-verification.

Recommended work-unit commits (per `work-unit-commits` skill):
1. `refactor(admin): introduce shared TicketeraControllerBase and paginated admin DTOs`
   - `backend/Controllers/TicketeraControllerBase.cs`, `backend/Services/PagedResult.cs`, `backend/Services/IAdminService.cs`, `backend/Services/AdminService.cs`, `backend/Controllers/AdminController.cs`
2. `feat(admin): add audit enums, context, and retrieval endpoint`
   - `backend/Models/AuditLog.cs`, `backend/Data/ApplicationDbContext.cs`, `backend/Services/IAuditLogService.cs`, `backend/Services/AuditLogService.cs`, `backend/Controllers/AdminController.cs`
3. `feat(events): wire best-effort audit logging into admin update/delete`
   - `backend/Controllers/EventController.cs`
4. `test(admin): harden admin and event controller tests for audit and pagination`
   - `backend/Tests/AdminControllerTests.cs`, `backend/Tests/EventControllerTests.cs`, `backend/Tests/AdminPropertyTests.cs`

### Next Recommended Phase

`sdd-verify` for the full Task 16 slice (including 16.4 hardening).

---

## Task 16.5 — Pagination cap regression test

### Completed Tasks

- [x] 16.5 Add regression tests for the AdminService 200-row pagination cap

### Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Tests/AdminPropertyTests.cs` | Modified | Added `GetAllUsers_PageSizeOver200_IsCappedTo200` and `GetAllEvents_PageSizeOver200_IsCappedTo200`. |
| `openspec/changes/ticketera-online/verify-report-task16.md` | Modified | Marked WARNING #1 as RESOLVED; updated test counts and execution evidence. |
| `openspec/changes/ticketera-online/apply-progress.md` | Modified | Documented this micro-fix. |

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 16.5 | `Tests/AdminPropertyTests.cs` | Unit | 273/273 | Written (regression test for existing cap) | Passed (cap already implemented) | 2 cases (users + events) | None needed |

### Test Summary

- **Total tests passing**: 275
- **Baseline passing (before Task 16.5)**: 273
- **Net new passing**: +2
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: None

### Deviations from Design

None — implementation matches design; only missing regression tests were added.

### Issues Found

None.

### Verification

- `dotnet test --filter "FullyQualifiedName~PageSizeOver200"`: 2/2 passing.
- `dotnet test` full suite: 275 passing, 0 failed, 0 skipped.

### Commits

- `test(admin): cubre cap de 200 filas en paginacion de AdminService`

### Next Recommended Phase

Task 17 — implement global error handling and structured logging.

---

## Task 17: Global error handling and structured logging

### Completed Tasks

- [x] 17.1 Create global exception handler (`IExceptionHandler`)
- [x] 17.2 Configure structured logging infrastructure
- [x] 17.3 Write property tests for error handling (Properties 44-51)

### Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Middleware/GlobalExceptionHandler.cs` | Created | `IExceptionHandler` implementation mapping exceptions to HTTP status codes, writing `ProblemDetails` responses, and emitting structured logs with redacted paths/messages. |
| `backend/Helpers/LogRedactor.cs` | Created | Defensive redaction helper for query strings and free-form messages; whitelists sensitive keys. |
| `backend/Models/Exceptions.cs` | Created | `ForbiddenException` for explicit 403 mapping in the global handler. |
| `backend/Models/AuditLog.cs` | Modified | Added `AuditActionType.ProcessWebhook`, `AuditActionType.ValidateQr`, `AuditResourceType.Payment`, `AuditResourceType.Ticket`. |
| `backend/Controllers/PaymentController.cs` | Modified | Inherits `TicketeraControllerBase`; injects `IAuditLogService`; logs a best-effort audit entry for every processed webhook. |
| `backend/Controllers/TicketController.cs` | Modified | Inherits `TicketeraControllerBase`; injects `IAuditLogService`; logs a best-effort audit entry for every QR validation. |
| `backend/Program.cs` | Modified | Registers `GlobalExceptionHandler`, `ProblemDetails`, and configures built-in structured logging levels. |
| `backend/Tests/PaymentControllerTests.cs` | Modified | Updated constructor to supply `IAuditLogService` mock. |
| `backend/Tests/ErrorHandlingPropertyTests.cs` | Created | FsCheck v3 property tests covering Properties 44-51 plus test doubles (`CollectingLogger`, `FakeAuditLogService`, `TestDbException`). |

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 17.1 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written (handler didn't exist) | Passed | 6 exception types + redaction | Extracted `LogRedactor` |
| 17.2 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | Structured-field assertions | None needed |
| 17.3/Prop 44 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | 100 DbException iterations | None needed |
| 17.3/Prop 45 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | 100 iterations | None needed |
| 17.3/Prop 46 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | 100 iterations | None needed |
| 17.3/Prop 47 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | 7 status-code scenarios × 100 | None needed |
| 17.3/Prop 48 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | 100 sensitive-message iterations | None needed |
| 17.3/Prop 49 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | 100 webhook scenarios | None needed |
| 17.3/Prop 50 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | 100 QR validation scenarios | None needed |
| 17.3/Prop 51 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 275/275 | Written | Passed | 100 sensitive-query iterations | Fixed redaction of path+query |

### Test Summary

- **Total tests passing**: 283
- **Baseline passing (before Task 17)**: 275
- **Net new passing**: +8
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: `LogRedactor.RedactQueryString`, `LogRedactor.RedactMessage`

### Deviations from Design

1. The global exception handler returns `ProblemDetails` (standard ASP.NET Core) instead of the anonymous `error { code, message }` shape shown in `design.md` line 1285-1292. This keeps the API consistent with `AddProblemDetails()` and gives clients `status`, `title`, `detail`, and `instance`.
2. `InvalidOperationException` is mapped to HTTP 500 rather than 409, because the existing codebase uses `InvalidOperationException` for many non-conflict scenarios (expired reservations, missing config, etc.). Only `DbUpdateConcurrencyException` maps to 409.
3. Built-in `Microsoft.Extensions.Logging` is used instead of Serilog. No extra sinks are required for stdout-only backend logging, and message templates already produce structured fields.

### Issues Found

- Initial `Property51` test exposed that passing `path + queryString` to `LogRedactor.RedactQueryString` caused the path to be mis-parsed as a query key, leaking the secret. Fixed by separating path and query redaction in `GlobalExceptionHandler`.
- No pre-existing flaky failures in this batch; `VerifyDatabaseSchema` passed in the full-suite run.

### Verification

- `dotnet test --filter FullyQualifiedName~ErrorHandlingPropertyTests`: 8/8 passing.
- `dotnet test` full suite: 283 passing, 0 failed, 0 skipped.

### Commits

- `feat(logging): implementa IExceptionHandler, logging estructurado y property tests 44-51`

### Next Recommended Phase

`sdd-verify` for Task 17; the orchestrator will run 4R review first.

---

## Task 17.4: Harden error handling and logging (post-4R review)

### Completed Fixes

- [x] R1-1 Global redacting console formatter protecting all `_logger.*` call sites.
- [x] R1-2 DNI hashed in `TicketController` lookup logs; PII keys added to `LogRedactor`.
- [x] R1-3 Complete `LogRedactor.SensitiveKeys` denylist + regex failover for Bearer/JWT/long secrets.
- [x] R1-4 Drop raw `{Error}` from webhook warning log.
- [x] R4-1 `GlobalExceptionHandler` self-protection catch + `OperationCanceledException` → 499 / Information.
- [x] R4-2 Webhook auth failure → 401; processing failure → 200 OK with opaque failed status.
- [x] R4-3 Audit-write-failure variants for Properties 49 and 50; inner try/catch around audit logger call.
- [x] R3-1 Property 51 driven from real `SensitiveKeys`; negative property for non-sensitive keys.
- [x] R3-2 Property 47 converted to parameterized `[Theory]` against spec matrix.
- [x] R3-3 `StackTrace` key asserted in Property 46.

### Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Helpers/LogRedactor.cs` | Modified | Expanded `SensitiveKeys` denylist (signature, x-signature, bearer, pan, cardholder, external_reference, qr_code_data, qrdata, refresh-token, email, dni, phone, document, documentnumber, document_number); removed duplicate `refresh_token`; added regex failover for Bearer tokens, JWT prefixes, and long secret-like strings; added `HashIdentifier` helper. |
| `backend/Helpers/RedactingConsoleFormatter.cs` | Created | Global console formatter that pipes every emitted message through `LogRedactor.RedactMessage` before stdout. |
| `backend/Program.cs` | Modified | Registers the redacting console formatter and configures console logging to use it. |
| `backend/Controllers/TicketController.cs` | Modified | Hashes DNI before logging in lookup request and error paths; added `Helpers` using. |
| `backend/Controllers/PaymentController.cs` | Modified | Drops raw `{Error}` from webhook warning log; distinguishes auth failures (401) from processing failures (200 with opaque status); wraps audit catch logger call in inner try/catch. |
| `backend/Middleware/GlobalExceptionHandler.cs` | Modified | Wraps `TryHandleAsync` body in self-protection catch writing hardcoded 500 JSON; special-cases `OperationCanceledException` as 499 with Information log. |
| `backend/Services/IPaymentService.cs` | Modified | Added `WebhookFailureType` enum and `FailureType` property to `WebhookResult`. |
| `backend/Services/PaymentService.cs` | Modified | Sets `FailureType = Authentication` for signature failures and `Processing` for other failures. |
| `backend/Tests/LogRedactorTests.cs` | Created | Unit tests for denylist, regex failover, DNI hashing, and the redacting console formatter. |
| `backend/Tests/ErrorHandlingPropertyTests.cs` | Modified | Property 47 converted to `[Theory]`; added 499/self-protection tests; Property 46 asserts `StackTrace`; Property 51 uses real `SensitiveKeys` plus negative cases; added audit-failure variants for Properties 49 and 50. |
| `backend/Tests/PaymentControllerTests.cs` | Modified | Updated invalid-signature mock with `FailureType = Authentication`; added processing-failure 200 test. |
| `openspec/changes/ticketera-online/tasks.md` | Modified | Added and marked Task 17.4 complete. |

### TDD Cycle Evidence

| Fix | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-----|-----------|-------|------------|-----|-------|-------------|----------|
| R1-1 | `Tests/LogRedactorTests.cs` | Unit | 283/283 | Written | Passed | Formatter + message cases | Clean |
| R1-2 | `Tests/LogRedactorTests.cs` | Unit | 283/283 | Written | Passed | Stable hash + different inputs | Clean |
| R1-3 | `Tests/LogRedactorTests.cs` | Unit | 283/283 | Written | Passed | 18 sensitive keys + regex cases + negative cases | Clean |
| R1-4 | `Tests/PaymentControllerTests.cs` | Unit | 6/6 | Written | Passed | Auth vs processing failure | Clean |
| R4-1 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 8/8 | Written | Passed | 499 + throwing logger self-protection | Clean |
| R4-2 | `Tests/PaymentControllerTests.cs` | Unit | 6/6 | Written | Passed | 401, 200 failed, 200 success | Clean |
| R4-3 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 8/8 | Written | Passed | Payment webhook + QR validation audit failure | Clean |
| R3-1 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 8/8 | Written | Passed | All sensitive keys + non-sensitive negative | Clean |
| R3-2 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 8/8 | Written | Passed | 7 exception × status-code mappings | Clean |
| R3-3 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 8/8 | Written | Passed | StackTrace key presence | Clean |

### Test Summary

- **Total tests passing**: 328
- **Baseline passing (before Task 17.4)**: 283
- **Net new passing**: +45
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: `LogRedactor.HashIdentifier`, regex redaction helpers

### Deviations from Design

None for this slice — all changes align with the 4R merge-blocking findings.

### Deviations acknowledged (NOT in 17.4 slice)

- **R4-5 EF Core `EnableRetryOnFailure` / resilience pipeline**: Deferred to Task 30 (integration tests). The current `AddDbContext` already calls `EnableRetryOnFailure` for transient Npgsql failures; a dedicated integration test slice will validate end-to-end resilience.
- **R4-6 Sentry/OpenTelemetry**: Deferred to Task 30 (integration tests) / post-MVP observability slice. No external telemetry SDKs are added in this hardening.
- **R4-4 Audit idempotency key / duplicate-call protection**: Deferred to Task 30 (integration tests). The existing single-call audit behavior is correct for this slice.
- **R2 advisory items** (TryLogAuditAsync hoist, ApiErrorCodes, doc comment "whitelist" wording): Explicitly excluded per scope — only merge-blocking findings were addressed.

### Issues Found

- None. Full suite passed cleanly.

### Verification

- `dotnet test --filter FullyQualifiedName~LogRedactorTests`: 31/31 passing.
- `dotnet test --filter FullyQualifiedName~ErrorHandlingPropertyTests`: 21/21 passing.
- `dotnet test --filter FullyQualifiedName~PaymentControllerTests`: 6/6 passing.
- `dotnet test` full suite: 328 passing, 0 failed, 0 skipped.

### Commits

- `fix(logging): endurece redaction, webhook 2xx, self-protection handler y property tests 17.4`

### Next Recommended Phase

**⚠ HANDOFF — Task 17.4 re-review surfaced NEW merge-blocking findings. DO NOT proceed to `sdd-verify` yet.**

A fresh-context 4R re-review of commit `fa2533c` returned:
- R1 Risk: **MERGE_BLOCKING** (R1-2 PARTIAL — email still leaks)
- R4 Resilience: **MERGE_BLOCKING** (R4-1 PARTIAL — handler self-protection incomplete)
- R2 Readability: ADVISORY (no blockers, deferred debt grew)
- R3 Reliability: ADVISORY (R3-3 PARTIAL — exception object not captured by logger)

A follow-up micro-slice **Task 17.4.1** is required before re-running 4R. The next session should:
1. Run `sdd-apply` for Task 17.4.1 with the 4 CRITICAL fixes listed below.
2. Re-run focused 4R re-review (R1 + R4 lenses minimum) on the new commit.
3. Only after PASS, run `sdd-verify` for the whole Task 17 + 17.4 + 17.4.1 slice.
4. Then continue to Task 18 (backend checkpoint), frontend 19-29, integration 30, docs 31, final checkpoint 32, and finally open the single PR.

---

## Task 17.4.1 — Pending micro-slice (4 CRITICAL fixes from re-review)

### Must-fix CRITICAL findings (4)

1. **Email leak in TicketController logs (R1-NF-1)**
   - Location: `backend/Controllers/TicketController.cs:45` and `:88`
   - Symptom: Template `"Ticket lookup request for email {Email} and DNI {DniHash}"` interpolates `email` RAW. The global `RedactingConsoleFormatter` only matches `key=value` / `key:value` shapes; structured-template interpolation `"email user@example.com"` is NOT redacted. Verified end-to-end.
   - Fix: Mirror the DNI fix — hash `email` via `LogRedactor.HashIdentifier(email)` and rename placeholder to `{EmailHash}`. Alternatively add an email-domain regex failover to `RedactMessage`. Hashing is the more robust choice (no regex false positives on legitimate traffic).
   - Test: Add a property that runs a real controller emission through `RedactingConsoleFormatter` (not just `key=value` form) — the existing `RedactingConsoleFormatter_RedactsMessageBeforeWriting` uses `token=super-secret-token-123` which does NOT exercise the inline-interpolation path. Add a test method like `RedactingConsoleFormatter_RedactsInlineEmailInRenderedMessage`.

2. **`OperationCanceledException` path throws on already-cancelled token (R4-N-1)**
   - Location: `backend/Middleware/GlobalExceptionHandler.cs:37-65` (the 499 branch)
   - Symptom: The 499 branch logs `Information` then FALLS THROUGH to `Response.StatusCode = 499; ... await WriteAsJsonAsync(problemDetails, cancellationToken)`. When `OperationCanceledException` fired, the `cancellationToken` is almost certainly already cancelled, so `WriteAsJsonAsync` throws `OperationCanceledException` → lands in outer catch → tries to write 500 fallback → also throws (client gone). The exception propagates out of `TryHandleAsync`, defeating the special-case.
   - Fix: `return true` immediately after the `Information` log in the 499 branch — DO NOT write a response body. The client already disconnected; the 499 is just a metric/status marker.
   - Test: Add a test that throws `OperationCanceledException` with `CancellationToken.Cancelled` and asserts `TryHandleAsync` returns `true` without invoking `WriteAsJsonAsync` (or without throwing).

3. **Self-protection catch missing `Response.HasStarted` guard (R4-N-2)**
   - Location: `backend/Middleware/GlobalExceptionHandler.cs:69-75` (the catch block)
   - Symptom: The catch writes raw bytes regardless of response state. If the try body already started writing (e.g. `WriteAsJsonAsync` flushed headers before the JSON serializer blew up, or a controller wrote then threw), `Response.StatusCode =` throws `InvalidOperationException` and self-protection is defeated.
   - Fix: At the top of the catch, add `if (httpContext.Response.HasStarted) return true;` BEFORE setting StatusCode/ContentType/WriteAsync. The pipeline will handle the partial response.
   - Test: Add a test that mocks a started response (or pre-writes to Response.Body) and asserts the catch returns `true` without attempting to write.

4. **Logger uses string-template overload — exception object never captured (R3-NF-2)**
   - Location: `backend/Middleware/GlobalExceptionHandler.cs:42-50`
   - Symptom: `_logger.LogError("...{StackTrace}", exception.GetType().Name, ..., exception.StackTrace)` uses the `LogError(string, params object[])` overload. The exception object itself is never passed to the logging pipeline. Structured-log sinks (Serilog, OTel, ELK, App Insights) receive zero `Exception` field — only the pre-stringified `StackTrace` inside the message template. `CollectingLogger<T>.LogEntry.Exception` is `null` for every entry, which is why Property 46 cannot assert `entry.Exception != null`.
   - Fix: Change the call to the `LogError(Exception exception, string message, params object[] args)` overload — pass `exception` as the FIRST positional argument before the template. Keep the same structured fields in the template (`ExceptionType`, `Method`, `Path`, `CorrelationId`, `ErrorCode`, `StackTrace`) but now the exception object flows to structured sinks too.
   - Test: Add `Assert.NotNull(entry.Exception)` to Property 46 (`Property46_ErrorLoggingFormat_AllRequiredFieldsPresent`). Today the test silently accepts the contract violation.

### Recommended non-blocking findings (include if time permits)

5. **`RedactingConsoleFormatter.Write` has no self-protection try/catch** (R1-NF-3, R4-mirror) — if `logEntry.Formatter(state, exception)` throws (object ToString throw) or `RedactMessage` throws on pathological input, the exception propagates out of the `_logger.X` call site. The handler's outer try/catch DOES cover some of these (via the new self-protection catch), but other call sites outside the handler are unprotected. Wrap `Write` body in `try { ... } catch { /* swallow — logging must never fail the request */ }`.

6. **`RedactLongSecretLikeStrings` over-redacts 33+ char base64** (R3-NF-3) — `LogRedactor.cs:131-134` regex `\b[A-Za-z0-9+/]{33,}={0,2}\b` catches legitimate base64 payloads (QR data, blob IDs, encoded reservation blobs). Add a `LogRedactorTests` `[Theory]` with 40+ char legitimate base64 strings to assert survival OR refine the regex.

7. **End-to-end formatter test missing Bearer/JWT-in-free-form-text path** (R3-NF-4) — Property 51 only exercises `RedactQueryString`. The Bearer/JWT/long-secret regex paths in `RedactMessage` are tested only by synthetic `LogRedactorTests` against literal strings. No test pipes a real captured log entry (e.g. an `InvalidOperationException` whose `Message` contains `Bearer abc...` or `eyJ...`) through `RedactingConsoleFormatter` to confirm end-to-end redaction. Add a test that constructs a real `LogEntry<object>` with a Bearer/JWT body in the rendered message and asserts the formatter output is redacted.

### Deferred findings (acknowledged, not in 17.4.1 scope)

8. **R2 advisory items** (TryLogAuditAsync hoist to `TicketeraControllerBase`, `ApiErrorCodes` static catalogue, `ProblemDetails.Title` RFC 7807 misuse) — kept deferred per user choice. Note: the magic-string debt GREW in 17.4 with new codes `"PROCESSING_FAILED"`, `"INTERNAL_ERROR"`, `"failed"`. A near-term slice `17.5 — ApiErrorCodes catalogue + ProblemDetailsFactory` is recommended to consolidate before frontend depends on the inline strings.
9. **R4-5 EF Core resilience / R4-6 Sentry-OpenTelemetry / R4-4 audit idempotency** — all routed to Task 30 (integration tests).
10. **Supabase DB password in `backend/appsettings.json:10-11`** (R1-NF-2 from re-review) — pre-existing leaked credential in git history (NOT introduced by Task 17). The user acknowledged and chose to triage separately in a future session. **Read later in this file for the dedicated handoff note.**

---

## Security Triage — Deferred (Supabase credential leak)

THIS IS ORTHOGONAL TO TASK 17 LOGGING — track separately as a security incident.

- **Issue**: `backend/appsettings.json` lines 10-11 commit a real Supabase DB pooler hostname + project-ref username (`postgres.sgymtpzqpmxvlcxkynrw`) + cleartext password `BocaJunior14135010` to git history. Present since at least commit `5fd1826` (Task 17 initial apply), likely older.
- **Other secrets in `appsettings.json`** (`Jwt.SecretKey`, `CloudflareR2.*`, `MercadoPago.*`, `Resend.ApiKey`, `QRCode.HmacSecretKey`) are placeholder templates — fine.
- **Required remediation** (in priority order, NOT for this SDD change — open a dedicated `security/credential-rotation` change):
  1. Rotate the Supabase DB password immediately via Supabase dashboard.
  2. Move connection strings to `dotnet user-secrets` (dev), environment variables (staging/prod), or a secrets manager (AWS Secrets Manager / Azure Key Vault / HashiCorp Vault).
  3. Replace the committed credentials in `appsettings.json` with placeholder templates like the other keys.
  4. Scrub git history with `git filter-repo --invert-paths --path backend/appsettings.json` (or targeted `filter-repo --replace-text`) — coordinate with maintainers before rewriting history; force-push required.
  5. Add `appsettings.Production.json` / `appsettings.Development.json` to `.gitignore` if present, and add a CI check that greps for `password=` patterns in `appsettings*.json`.
- **Severity**: HIGH. The redaction work in Task 17.4 is moot while the live credential sits in repo history.
- **Why deferred this session**: User explicitly chose "Lo dejamos para después" at session close. A future session should pick this up as priority 1 before any public exposure of the repo.

---

## Session close state

- Branch: `dev`
- HEAD: `fa2533c` (`fix(logging): endurece redaction, webhook 2xx, self-protection handler y property tests 17.4`)
- Backend tests: **328 passing / 0 failing / 0 skipped**
- OpenSDD artifacts: hybrid mode (Engram + files under `openspec/changes/ticketera-online/`)
- Pipeline status: Task 17 in progress — apply done, hardening done, re-review done, 17.4.1 micro-slice PENDING, then 4R re-review, then `sdd-verify`, then Task 18+.

### Where to pick up

1. Run `sdd-apply` for Task 17.4.1 with the 4 CRITICAL fixes above. Strict TDD mode active; baseline 328 tests.
2. After the micro-slice commits, re-run a focused 4R re-review (R1 + R4 lenses minimum) on the new commit.
3. If PASS, run `sdd-verify` for Task 17 (covers 17.1, 17.2, 17.3, 17.4, 17.4.1).
4. If 4R re-review surfaces NEW blockers, address in 17.4.2 (or fold into 17.4.1 if still in the same slice) before verify.
5. Only after `sdd-verify` PASS: continue to Task 18 (backend checkpoint completeness), then frontend 19-29, integration 30, docs 31, final checkpoint 32, and open the single PR.

### Pending test warning (inherited from Task 16)

- `verify-report-task16.md` WARNING #1 was RESOLVED in commit `4696381` (Task 16.5 added pagination 200-cap regression tests). No outstanding warnings from Task 16.

---

## Task 17.4.1 — Completed

### Completed Fixes

- [x] R1-NF-1 Email leak in `TicketController` lookup/error logs.
- [x] R4-N-1 `OperationCanceledException` path no longer falls through to `WriteAsJsonAsync` on a cancelled token.
- [x] R4-N-2 Self-protection catch now returns early when `Response.HasStarted`.
- [x] R3-NF-2 `GlobalExceptionHandler` passes the exception object to `LogError` so structured sinks receive it.
- [x] R1-NF-3 `RedactingConsoleFormatter.Write` swallows formatter/redactor exceptions so logging never fails the request.
- [x] R3-NF-4 End-to-end formatter test for Bearer/JWT in free-form rendered messages.
- [ ] R3-NF-3 Base64 over-redaction in `RedactLongSecretLikeStrings` — deferred to 17.4.2.

### Files Changed

| File | Action | Description |
|------|--------|-------------|
| `backend/Controllers/TicketController.cs` | Modified | Hashes email via `LogRedactor.HashIdentifier(email)` in lookup request and error log templates; placeholders renamed to `{EmailHash}`. |
| `backend/Middleware/GlobalExceptionHandler.cs` | Modified | `OperationCanceledException` branch sets 499 and returns true before writing body; self-protection catch returns true when `Response.HasStarted`; error log uses `LogError(Exception, ...)` overload. |
| `backend/Helpers/RedactingConsoleFormatter.cs` | Modified | Wrapped `Write` body in try/catch to prevent logging failures from propagating. |
| `backend/Tests/LogRedactorTests.cs` | Modified | Added `RedactingConsoleFormatter_RedactsInlineEmailInRenderedMessage`, `RedactingConsoleFormatter_RedactsBearerTokenInFreeFormMessage`, and `RedactingConsoleFormatter_SwallowsFormatterException`. |
| `backend/Tests/ErrorHandlingPropertyTests.cs` | Modified | Added `Property47d_OperationCanceled_WithCancelledToken_ReturnsTrueWithoutWriting`, `Property47e_HandlerSelfProtection_ResponseAlreadyStarted_ReturnsTrueWithoutWriting`, and asserted `entry.Exception != null` in `Property46_Exception_LogsStructuredFields`. |
| `openspec/changes/ticketera-online/tasks.md` | Modified | Added and marked Task 17.4.1 complete; noted deferred R3-NF-3. |

### TDD Cycle Evidence

| Fix | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-----|-----------|-------|------------|-----|-------|-------------|----------|
| R1-NF-1 | `Tests/LogRedactorTests.cs` | Unit | 328/328 | Written | Passed | Inline email + hash verification | Extracted `CollectingLogger<T>` helper |
| R4-N-1 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 329/329 | Written | Passed | Cancelled token + body-empty assertion | Clean |
| R4-N-2 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 330/330 | Written | Passed | Custom `StartedResponseFeature` simulating sent headers | Clean |
| R3-NF-2 | `Tests/ErrorHandlingPropertyTests.cs` | Unit | 331/331 | Modified first | Passed | Property 46 asserts exception object | Clean |
| R1-NF-3 | `Tests/LogRedactorTests.cs` | Unit | 331/331 | Written | Passed | Throwing formatter | Clean |
| R3-NF-4 | `Tests/LogRedactorTests.cs` | Unit | 332/332 | Written | Passed | Bearer JWT in free-form message | Clean |

### Test Summary

- **Total tests passing**: 333
- **Baseline passing (before Task 17.4.1)**: 328
- **Net new passing**: +5
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: None

### Deviations from Design

- None for this slice — all changes align with the 4R merge-blocking findings.

### Issues Found

- `DefaultHttpContext` built from a bare `FeatureCollection` requires both `IHttpRequestFeature` and `IHttpResponseBodyFeature` to avoid `NullReferenceException` when tests read `Response.Body`; resolved with `HttpRequestFeature` + `StreamResponseBodyFeature`.

### Verification

- `dotnet test --filter FullyQualifiedName~LogRedactorTests`: 34/34 passing.
- `dotnet test --filter FullyQualifiedName~ErrorHandlingPropertyTests`: 23/23 passing.
- `dotnet test` full suite: 333 passing, 0 failed, 0 skipped.

### Commits

- `fix(logging): hashea email en TicketController lookup logs (R1-NF-1)`
- `fix(handler): evita escribir body cuando OperationCanceledException usa token cancelado (R4-N-1)`
- `fix(handler): protege self-protection catch con guarda Response.HasStarted (R4-N-2)`
- `fix(handler): pasa objeto Exception a LogError para sinks estructurados (R3-NF-2)`
- `fix(logging): self-protection en RedactingConsoleFormatter y test end-to-end Bearer/JWT (R1-NF-3, R3-NF-4)`

### Next Recommended Phase

Focused 4R re-review (R1 + R4 lenses minimum) on the new `HEAD` commit. If PASS, run `sdd-verify` for the whole Task 17 + 17.4 + 17.4.1 slice. If new blockers surface, address in 17.4.2 before verify.

### Deferred to 17.4.2 / Future

- **R3-NF-3**: `RedactLongSecretLikeStrings` over-redacts 33+ char base64 (QR data, blob IDs, encoded reservation blobs). Add a `LogRedactorTests` `[Theory]` with 40+ char legitimate base64 strings and refine the regex if it fails.
- **R2 advisory debt** and **Supabase credential leak** remain tracked separately.
