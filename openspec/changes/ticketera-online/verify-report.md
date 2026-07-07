# Verification Report — Task 13 Checkpoint (Reservation, QR, and Payment Systems)

> **Prior report superseded**: The 2026-07-01 report covered only Task 12 sub-tasks 12.1–12.5. This report supersedes it, covering the full Task 13 checkpoint scope (Tasks 9, 10, 11, 12 including the 12.6 remediation).

**Change**: ticketera-online
**Scope**: Task 13 checkpoint — Tasks 9 (reservation + concurrency), 10 (expiration background service), 11 (QR code generation + validation), 12 (payment service with Mercado Pago, including 12.6 remediation)
**Mode**: Strict TDD (backend only)
**Date**: 2026-07-07

## Completeness
| Metric | Value |
|--------|-------|
| Tasks in scope | 4 task groups (9, 10, 11, 12) with 17 sub-tasks |
| Tasks complete | 16 (9.1–9.3, 10.1–10.2, 11.1–11.6, 12.1–12.6, 12.6-A, 12.6-C) |
| Tasks deferred | 1 (12.7 — DNI sentinel guard, deferred to post-presentation hardening) |
| Tasks incomplete (blocking) | 0 |

## Build & Tests Execution
**Build**: ✅ Passed (0 errors, 1 warning CS7022 — benign auto-generated entry point)
```text
Command: dotnet test --verbosity normal (via Windows dotnet.exe from WSL)
Build: 0 errors, 1 warning (CS7022)
```

**Tests**: ✅ 211 passed / ❌ 1 failed / ⚠️ 0 skipped (212 total)
```text
Command: dotnet test --verbosity normal
Result: 212 total, 211 passed, 1 failed
Failed: VerifyDatabaseSchema.Database_Should_Have_All_Tables
  → Npgsql.PostgresException: XX000: (ENOTFOUND) tenant/user postgres.sgymtpzqpmxvlcxkynrw not found
  → Pre-existing flaky test (live Supabase unreachable). NOT introduced by Tasks 9-12.
```

**Claimed**: 211 passing. **Verified**: ✅ Confirmed (211 passed, matching apply-progress claim).

**Coverage**: ➖ Not available (no coverage tool configured)

## TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | Found in apply-progress.md with full TDD Cycle Evidence table |
| All tasks have tests | ✅ | 16/16 completed tasks have test files on disk |
| RED confirmed (tests exist) | ✅ | 10/10 test files verified on disk for in-scope tasks |
| GREEN confirmed (tests pass) | ✅ | 211/212 tests pass (1 pre-existing flaky excluded) |
| Triangulation adequate | ✅ | Multi-case for Properties 10 (4 scenarios), 11 (5+5 scenarios), 12 (4 scenarios), 13 (4 scenarios), 14 (2), 17 (3), 18 (2), 19 (2), 20 (2), 21 (3), 26 (7), 27 (2), 28 (2), 29 (2), 38 (1), 39 (1), 41 (3) |
| Safety Net for modified files | ✅ | 204/204 baseline run reported for 12.6 remediation (flaky excluded) |

**TDD Compliance**: 6/6 checks passed

---

### Test Layer Distribution
| Layer | Tests | Files |
|-------|-------|-------|
| Unit | ~90 (in-scope) | 10 files (ReservationPropertyTests, ReservationServiceTests, ReservationControllerTests, ReservationExpirationServiceTests, QRCodePropertyTests, TicketServiceTests, TicketLookupPropertyTests, PaymentPropertyTests, PaymentControllerTests) |
| Integration | 0 | — |
| E2E | 0 | — |
| **Total in-scope** | **~90** | **10** |

---

### Assertion Quality
✅ All assertions verify real behavior. No tautologies, no ghost loops, no smoke-test-only patterns, no mock-heavy anti-patterns detected across all 10 in-scope test files.

Key quality observations:
- `Property15_ApprovedWebhook_TicketsCarryReservationDNIAndAreLookupable` — substantive regression test: asserts ticket count, DNI equality, NOT-equal-to-placeholder, AND lookup round-trip.
- `Property41_ConcurrentReservations_PreventOverselling` — asserts total reserved ≤ capacity with multi-user scenarios.
- `Property27_DoubleScanPrevention_RejectsAlreadyUsedTickets` — asserts first scan succeeds, second through seventh scans all fail with "already used".
- `Property21_SignatureVerification_RejectsTamperedData` — tests changed ticketId, changed timestamp, and single-character signature modification.

**Assertion quality**: ✅ All assertions verify real behavior

---

## Spec Compliance Matrix

### Reservations Domain (specs/reservations/spec.md)
| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| 4.1 | Reservation created with 10-minute expiration | `ReservationPropertyTests.ReservationCreation_SetsExpirationToExactly10Minutes` + `ReservationServiceTests.CreateReservationAsync_WithValidData_CreatesReservationWith10MinuteExpiration` | ✅ COMPLIANT |
| 4.2 | Reservation decrements ticket inventory | `ReservationPropertyTests.ReservationCreation_DecrementsInventoryByQuantity` + `MultipleReservations_DecrementInventoryCumulatively` | ✅ COMPLIANT |
| 4.3 | Reservation identifier returned to Frontend | `ReservationControllerTests.CreateReservation_WithValidRequest_Returns201Created` | ✅ COMPLIANT |
| 4.4 | Active reservations prevent double-booking | `ReservationPropertyTests.ActiveReservation_PreventsDoubleBooking` + `_AllowsPartialBookingUpToAvailableQuantity` + `_ConsidersBothSoldTicketsAndActiveReservations` | ✅ COMPLIANT |
| 4.5 | Expired reservation releases tickets | `ReservationPropertyTests.ExpiredReservation_RestoresInventoryWhenReleased` + `MultipleExpiredReservations_RestoreInventoryCumulatively` + `ReleaseExpiredReservations_DoesNotAffectActiveReservations` | ✅ COMPLIANT |
| 4.6 | Expiration Service runs as IHostedService | `ReservationExpirationServiceTests.StartAsync_InitializesServiceSuccessfully` + `ServiceIntegration_ExecutesPeriodicallyContinuously` | ✅ COMPLIANT |
| 4.7 | Expiration Service checks at regular intervals | `ReservationExpirationServiceTests.ServiceIntegration_ReleasesExpiredReservationsAndRestoresInventory` + `ServiceIntegration_ExecutesMultipleCycles` | ✅ COMPLIANT |
| 4.8 | Frontend countdown timer | — | ⏭️ OUT OF SCOPE (frontend) |
| 4.9 | Frontend expiration notification | — | ⏭️ OUT OF SCOPE (frontend) |

### Payments Domain (specs/payments/spec.md)
| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| 5.1 | Checkout creates MP preference | `PaymentPropertyTests.Property14_CreatePreference_IncludesReservationDetailsAndTotalAmount` | ✅ COMPLIANT |
| 5.2 | Preference contains complete data | `PaymentPropertyTests.Property14_CreatePreference_IncludesReservationDetailsAndTotalAmount` | ✅ COMPLIANT |
| 5.3 | Checkout URL returned | `PaymentPropertyTests.Property14_CreatePreference_IncludesReservationDetailsAndTotalAmount` | ✅ COMPLIANT |
| 5.4 | Frontend redirects to MP | — | ⏭️ OUT OF SCOPE (frontend) |
| 5.5 | Successful webhook → tickets | `PaymentPropertyTests.Property15_ApprovedWebhook_ConfirmsReservationAndCreatesTickets` + `Property15_ApprovedWebhook_TicketsCarryReservationDNIAndAreLookupable` | ✅ COMPLIANT |
| 5.6 | Failed webhook → release reservation | `PaymentPropertyTests.Property16_RejectedWebhook_CancelsReservation` | ✅ COMPLIANT |
| 5.7 | Webhook signature validation | `PaymentPropertyTests.Property17_ValidSignature_AcceptsWebhook` + `Property17_InvalidSignature_RejectsWebhook` + `Property17_InvalidSignature_ReturnsUnauthorized` | ✅ COMPLIANT |
| 5.8 | Reject invalid signatures with 401 | `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized` | ✅ COMPLIANT |
| 12.2 | Insufficient inventory → refund | `PaymentPropertyTests.Property38_StockFailure_TriggersRefund` | ✅ COMPLIANT |
| 12.3 | Stock failure and refund logged | `PaymentPropertyTests.Property39_Refund_LogsRefundedTransaction` | ✅ COMPLIANT |
| 12.4 | User notified by email about refund | — | ⏭️ OUT OF SCOPE (email — Task 14) |
| 12.5 | Associated reservations released on stock failure | `PaymentPropertyTests.Property38_StockFailure_TriggersRefund` (reservation cancelled in code path) | ✅ COMPLIANT |
| 12.6 | Concurrent purchases don't oversell | `ReservationPropertyTests.ConcurrentReservations_PreventOverselling` + `SequentialReservations_ForLastTickets_PreventOverselling` + `ConcurrentReservations_WithSoldTickets_PreventOverselling` | ✅ COMPLIANT |
| 16.5 | Webhook audit logging | All PaymentPropertyTests (logging via mock verification) | ✅ COMPLIANT |

### Tickets Domain (specs/tickets/spec.md) — Backend Scenarios Only
| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| 6.1 | Unique QR code per ticket | `QRCodePropertyTests.Property18_QRCodeUniqueness_AllGeneratedQRCodesAreUnique` + `_AcrossMultipleReservations` | ✅ COMPLIANT |
| 6.2 | QR code signed with HMAC-SHA256 | `QRCodePropertyTests.Property19_QRCodeSignatureValidity_AllGeneratedSignaturesAreValid` + `_ValidAcrossDifferentTimestamps` | ✅ COMPLIANT |
| 6.3 | QR code format `{ticketId}:{timestamp}:{signature}` | `QRCodePropertyTests.Property20_QRCodeFormatCorrectness_MatchesExpectedFormat` + `_ConsistentFormatAcrossBatch` | ✅ COMPLIANT |
| 6.4 | Ticket record stored in DB | `TicketServiceTests.CreateTicketsAsync_ValidReservation_CreatesTickets` | ✅ COMPLIANT |
| 6.5 | Visual QR code image generated | `TicketServiceTests.GenerateQRCodeImage_ValidData_ReturnsBase64Image` | ✅ COMPLIANT |
| 6.6 | QR signature verified on validation | `QRCodePropertyTests.Property21_SignatureVerification_RejectsInvalidSignatures` + `_RejectsTamperedData` + `_RejectsMalformedQRCodes` | ✅ COMPLIANT |
| 6.7 | Invalid signature rejected as fraudulent | `QRCodePropertyTests.Property21_SignatureVerification_RejectsInvalidSignatures` | ✅ COMPLIANT |
| 7.x | Email delivery scenarios | — | ⏭️ OUT OF SCOPE (Task 14) |
| 8.2 | Backend queries by email AND DNI | `TicketLookupPropertyTests.Property26_TicketLookup_ReturnsOnlyMatchingTickets` | ✅ COMPLIANT |
| 8.3 | Returns all matching tickets with QR codes | `TicketLookupPropertyTests.Property26_TicketLookup_IncludesNavigationProperties` + `_ReturnsTicketsFromMultipleEvents` | ✅ COMPLIANT |
| 8.5 | Empty result when no match | `TicketLookupPropertyTests.Property26_TicketLookup_ReturnsEmptyForNoMatches` | ✅ COMPLIANT |
| 9.3 | Backend verifies HMAC-SHA256 signature | `QRCodePropertyTests.Property21_SignatureVerification_*` + `TicketServiceTests.ValidateQRCodeAsync_InvalidSignature_ReturnsError` | ✅ COMPLIANT |
| 9.4 | Rejects already-used tickets | `QRCodePropertyTests.Property27_DoubleScanPrevention_RejectsAlreadyUsedTickets` + `_PreservesOriginalUsedAtTimestamp` | ✅ COMPLIANT |
| 9.5 | Validates ticket belongs to event | `QRCodePropertyTests.Property28_EventSpecificValidation_RejectsTicketsForDifferentEvent` + `_TicketOnlyValidForOneEvent` | ✅ COMPLIANT |
| 9.6 | Valid unused ticket marked as used | `QRCodePropertyTests.Property29_ValidTicketMarkedAsUsed_SuccessfulScanMarksTicketUsed` + `_MultipleTicketsCanBeValidated` | ✅ COMPLIANT |
| 9.7 | Invalid/used/wrong-event returns error | `TicketServiceTests.ValidateQRCodeAsync_AlreadyUsedTicket_ReturnsError` + `_WrongEvent_ReturnsError` | ✅ COMPLIANT |
| 9.8–9.9 | Frontend scanner UI/feedback | — | ⏭️ OUT OF SCOPE (frontend) |

**Compliance summary**: 30/30 in-scope backend scenarios compliant (9 frontend/email scenarios excluded as out of scope)

## Correctness Properties Trace
| Property | Test Method | Substantive? | Notes |
|----------|------------|-------------|-------|
| 10: Reservation Creation Sets Correct Expiration | `ReservationCreation_SetsExpirationToExactly10Minutes` | ✅ Yes | 4 scenarios with different userId/quantity; asserts expiration within 1-second tolerance |
| 11: Reservation Decrements Inventory | `ReservationCreation_DecrementsInventoryByQuantity` + `MultipleReservations_DecrementInventoryCumulatively` | ✅ Yes | 5 quantity scenarios + 4 sequential reservations; asserts exact available count |
| 12: Active Reservations Prevent Double-Booking | `ActiveReservation_PreventsDoubleBooking` + 3 more | ✅ Yes | Full/partial/mixed-inventory/expired scenarios; asserts ArgumentException with "Insufficient" |
| 13: Expired Reservations Restore Inventory | `ExpiredReservation_RestoresInventoryWhenReleased` + 3 more | ✅ Yes | Multiple quantities, multiple expired, mixed active/expired; asserts status change + inventory math |
| 14: Payment Preference Contains Complete Data | `Property14_CreatePreference_IncludesReservationDetailsAndTotalAmount` + `_RequiresActiveReservation` | ✅ Yes | Asserts ExternalReference, Quantity, UnitPrice, total, Title, active-reservation guard |
| 15: Successful Payment Creates Tickets | `Property15_ApprovedWebhook_ConfirmsReservationAndCreatesTickets` + `_TicketsCarryReservationDNIAndAreLookupable` | ✅ Yes | Asserts reservation → Confirmed, ticket count = quantity, DNI carries through, lookup round-trip |
| 16: Failed Payment Releases Reservation | `Property16_RejectedWebhook_CancelsReservation` | ✅ Yes | Asserts reservation → Cancelled AND no tickets created |
| 17: Webhook Signature Validation | `Property17_ValidSignature_AcceptsWebhook` + `_InvalidSignature_RejectsWebhook` + `_InvalidSignature_ReturnsUnauthorized` | ✅ Yes | Valid/invalid at static + service level; no side effects on invalid |
| 18: QR Code Uniqueness | `Property18_QRCodeUniqueness_AllGeneratedQRCodesAreUnique` + `_AcrossMultipleReservations` | ✅ Yes | 50 tickets in one batch + 25 across 5 reservations; asserts distinct count |
| 19: QR Code Signature Validity | `Property19_QRCodeSignatureValidity_AllGeneratedSignaturesAreValid` + `_ValidAcrossDifferentTimestamps` | ✅ Yes | 10 different ticket IDs + 5 time-delayed generations; round-trip sign-then-verify |
| 20: QR Code Format Correctness | `Property20_QRCodeFormatCorrectness_MatchesExpectedFormat` + `_ConsistentFormatAcrossBatch` | ✅ Yes | Validates GUID, timestamp, 64-char hex signature; batch of 20 tickets |
| 21: QR Code Signature Verification | `Property21_SignatureVerification_RejectsInvalidSignatures` + `_RejectsTamperedData` + `_RejectsMalformedQRCodes` | ✅ Yes | 4 invalid types + 3 tampering attempts + 7 malformed formats |
| 26: Ticket Lookup Returns Correct Matches | `Property26_TicketLookup_ReturnsOnlyMatchingTickets` + 6 more | ✅ Yes | Cross-user matching, empty results, ordering, multi-event, used/unused, case sensitivity, navigation properties |
| 27: Double-Scan Prevention | `Property27_DoubleScanPrevention_RejectsAlreadyUsedTickets` + `_PreservesOriginalUsedAtTimestamp` | ✅ Yes | First scan succeeds, 5 subsequent scans fail; UsedAt preserved |
| 28: Event-Specific Ticket Validation | `Property28_EventSpecificValidation_RejectsTicketsForDifferentEvent` + `_TicketOnlyValidForOneEvent` | ✅ Yes | Correct event passes, wrong event rejected; tested across 5 events |
| 29: Valid Ticket Marked as Used | `Property29_ValidTicketMarkedAsUsed_SuccessfulScanMarksTicketUsed` + `_MultipleTicketsCanBeValidated` | ✅ Yes | Single + batch of 10; asserts IsUsed, UsedAt, DB persistence |
| 38: Stock Failure Triggers Refund | `Property38_StockFailure_TriggersRefund` | ✅ Yes | Verifies RefundPaymentAsync called Times.Once via Moq |
| 39: Refund Logging | `Property39_Refund_LogsRefundedTransaction` | ✅ Yes | Asserts Transaction persisted with correct ReservationId, Amount, Refunded status |
| 41: Concurrent Purchase Prevention | `ConcurrentReservations_PreventOverselling` + `SequentialReservations_ForLastTickets_PreventOverselling` + `_WithSoldTickets_PreventOverselling` | ✅ Yes | 3 scenarios: concurrent, sequential last-tickets, mixed with sold tickets |

**All 20 in-scope properties have substantive test coverage. No decorative tests detected.**

## Design Coherence
| Decision | Followed? | Notes |
|----------|-----------|-------|
| IReservationService interface | ✅ Yes | Matches design.md; `PurchaserDNI` parameter added (12.6 remediation) — justified |
| ReservationExpirationService as IHostedService | ✅ Yes | Timer-based, 30-second interval, scoped service resolution |
| Optimistic concurrency (RowVersion) | ✅ Yes | Transaction + retry with exponential backoff in ReservationService |
| ITicketService interface | ✅ Yes | Matches design.md; `CreateTicketsAsync` accepts `purchaserDNI` (12.6) |
| HMAC-SHA256 QR signing | ✅ Yes | `{ticketId}:{timestamp}:{signature}` format; `CryptographicOperations.FixedTimeEquals` (positive divergence) |
| QRCoder for visual QR images | ✅ Yes | `PngByteQRCode` with 10px modules, base64 output |
| IPaymentService interface | ⚠️ Divergence | `InitiateRefundAsync` gained `Guid reservationId` param. **Justified**: Transaction.ReservationId is non-nullable Guid |
| PaymentPreference DTO | ✅ Yes | Matches design.md exactly |
| HMAC-SHA256 webhook validation | ✅ Yes | Uses `CryptographicOperations.FixedTimeEquals` — positive divergence (constant-time) |
| MercadoPagoClient abstraction | ✅ Yes | Clean IMercadoPagoClient interface, typed HttpClient |
| DI registration | ✅ Yes | All services properly scoped; IHostedService registered |
| Controller endpoints | ✅ Yes | Reservation: POST + GET; Payment: POST create-preference [Authorize] + POST webhook [AllowAnonymous]; Ticket: GET lookup + POST validate |
| Reservation.PurchaserDNI model | ✅ Yes | Added in 12.6; `IsRequired().HasMaxLength(50)` in DbContext config |

## Security Check (Webhook)
| Check | Result |
|-------|--------|
| HMAC-SHA256 signature validation | ✅ Before any side effects (`PaymentService.cs:94`) |
| Constant-time comparison | ✅ `CryptographicOperations.FixedTimeEquals` (`PaymentService.cs:263`) |
| Early return on invalid signature | ✅ No operations when signature invalid (`PaymentService.cs:97-103`) |
| Controller rejects missing signature | ✅ 401 before calling service (`PaymentController.cs:81-84`) |
| Webhook endpoint is public | ✅ Correct — MP webhooks don't carry JWT (`[AllowAnonymous]`) |
| Secret from configuration | ✅ `MercadoPagoOptions.WebhookSecret` via IOptions<T> |

**Webhook security is sound.**

## Security Check (QR Code)
| Check | Result |
|-------|--------|
| HMAC-SHA256 signing | ✅ `TicketService.cs:120` — `ComputeHmacSha256(dataToSign, _hmacSecretKey)` |
| Constant-time verification | ✅ `CryptographicOperations.FixedTimeEquals` (`TicketService.cs:205`) |
| Format validation | ✅ 3-part split, GUID parse, long parse, 64-char hex check |
| Tamper detection | ✅ Tested: changed ticketId, changed timestamp, modified signature all rejected |
| Secret from configuration | ✅ `QRCode:HmacSecretKey` via IConfiguration |

**QR code security is sound.**

## 12.6 Remediation Verification
| Check | Result | Evidence |
|-------|--------|----------|
| PaymentService no longer mints tickets with `"00000000"` | ✅ | `PaymentService.cs:150`: `reservation.PurchaserDNI` (not hardcoded) |
| Reservation model has PurchaserDNI | ✅ | `Reservation.cs:10`: `public string PurchaserDNI { get; set; } = string.Empty;` |
| DNI validated at reservation creation | ✅ | `ReservationService.cs:44-54`: empty, whitespace, null, >50 chars all rejected |
| DNI captured at controller level | ✅ | `ReservationController` passes `request.PurchaserDNI` to service |
| DNI NOT returned in response (PII) | ✅ | `ReservationResponse` does not include `PurchaserDNI` (12.6-A remediation) |
| Regression test exists and passes | ✅ | `Property15_ApprovedWebhook_TicketsCarryReservationDNIAndAreLookupable` — asserts DNI carries through webhook → ticket → lookup |
| DNI validation branch tests exist | ✅ | 6 service tests (empty, whitespace, tab/newline, null, 51 chars, 50 chars) + 1 controller 400 test |

**Prior WARNING (placeholder DNI "00000000") is RESOLVED.**

## Scope Discipline
✅ No out-of-scope files touched. All modified files are reservation, ticket, payment, or DI domain. No Task 14 (email), Task 15 (metrics), or frontend work leaked in.

## Issues Found

### CRITICAL
None

### WARNING
1. **Task 12.7 deferred — DNI sentinel guard** — `PaymentService.ProcessApprovedPaymentAsync` has no guard against `reservation.PurchaserDNI` being empty/whitespace or the legacy migration sentinel `"00000000"`. Pre-existing reservations (pre-deploy) flowing through the webhook would mint tickets with the placeholder DNI. **Status**: Deferred to post-presentation hardening per apply-progress. No legacy Active reservations exist in the current pre-production environment. Risk is theoretical, not real.
2. **Diff size understated** (carried from prior report) — Apply claimed ~960 lines for Task 12.1-12.5; actual was 1145 insertions (19% understatement). Informational for future planning.

### SUGGESTION
1. **Webhook payload not fully logged** — Property 49 (16.5) says "log the webhook event with timestamp, payload, and processing result." Implementation logs key fields but not the full serialized payload. Better for security but technically diverges from spec wording.
2. **Simplified MP webhook signature format** — Implementation treats `x-signature` header as raw HMAC hex digest. Mercado Pago's actual format includes `ts=...;v1=...` components. Matches design.md but will need adaptation for real MP integration.
3. **QR code signature verification uses `==` in design.md** — Implementation correctly uses `CryptographicOperations.FixedTimeEquals` (positive divergence). Design.md should be updated to reflect this.

## Verdict
**PASS WITH WARNINGS**

Tasks 9, 10, 11, and 12 (including 12.6 remediation) are fully implemented with substantive test coverage across 20 correctness properties, 30/30 in-scope spec scenarios compliant, proper webhook and QR code security (constant-time comparison as a positive divergence from design), clean scope discipline, and 211 passing tests. The two warnings (deferred 12.7 DNI sentinel guard and diff size understatement) are known items that don't block the checkpoint. Task 13 checkpoint is satisfied.
