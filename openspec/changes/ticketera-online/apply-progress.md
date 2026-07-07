# Apply Progress: Ticketera Online MVP — Tasks 12.1-12.7

## Summary

Implemented Task 12.6 (fix purchaser DNI on ticket creation from payment webhook) on top of the previously completed Task 12.1-12.5 payment-service slice, then remediated post-4R-review items A (CRITICAL PII) and C (TDD validation gap). Item B was deferred as new Task 12.7. The backend test suite now has **211 passing tests** (+7 new from the remediation), with the one pre-existing flaky `VerifyDatabaseSchema` test still failing due to live Supabase connectivity.

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
| `openspec/changes/ticketera-online/tasks.md` | Modified | Task 12.6 remains complete; added deferred Task 12.7 |

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

## Test Summary

- **Total tests passing**: 211
- **Baseline passing (before remediation)**: 204
- **Net new passing**: +7
- **Layers used**: Unit (all)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: None

## Deviations from Design

1. Added `Guid reservationId` parameter to `InitiateRefundAsync` so the refund transaction can be associated with the correct reservation. The design.md interface omits this parameter, but the `Transaction` entity requires a non-null `ReservationId`. (Carried forward from Task 12.1-12.5.)
2. Webhook payload model is simplified (`PaymentId`, `ExternalReference`, `Status`) rather than fetching full payment details from Mercado Pago. This matches the design.md HMAC signature validation example. (Carried forward from Task 12.1-12.5.)
3. No DNI backfill script was added — the migration uses a non-null default `"00000000"` for existing rows, which is acceptable for fresh dev DBs per the task scope.

## Issues Found

- The pre-existing `VerifyDatabaseSchema` test fails because the live Supabase tenant/user is not reachable from this environment. Not addressed per instructions.
- Existing reservations created before this change will have `PurchaserDNI = "00000000"` after migration; any webhook processed against them would still produce placeholder-DNI tickets. Deferred as Task 12.7.

## Commits

No commits made in this batch. The orchestrator owns commit and PR after re-verification.

## Verification

- `dotnet test` backend result: 211 passing, 1 pre-existing flaky failure (`VerifyDatabaseSchema`).
- Regression test `Property15_ApprovedWebhook_TicketsCarryReservationDNIAndAreLookupable` confirms tickets created via approved-payment webhook carry the reservation's real DNI and are returned by `TicketService.LookupTicketsAsync`.

## Next Recommended Phase

`sdd-verify` for the full Task 12 slice, then proceed to Task 13 (checkpoint) or Task 12.7 hardening.
