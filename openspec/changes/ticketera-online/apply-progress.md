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
