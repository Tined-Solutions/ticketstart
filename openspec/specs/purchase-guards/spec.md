# Purchase Guards — Block Expired Event Transactions

**Requirements covered**: EHE-004, EHE-005, EHE-011

## Purpose

Defense-in-depth purchase guards MUST prevent buyers from creating reservations or payment preferences for expired events, while confirmed payments (webhook path) MUST remain unaffected so tickets are still issued for already-approved transactions.

## Requirements

### Requirement: EHE-004 — Reservation guard rejects expired events

`ReservationService.CreateReservationTransactionalAsync` MUST check `event.IsExpired(DateTime.UtcNow)` before creating a reservation. If expired, the service MUST reject with a `ProblemDetails` response (409 Conflict, type `"event-expired"`, title `"Event has already started"`). No reservation row MUST be persisted.

#### Scenario: Reservation for expired event rejected

- GIVEN an event with `Date < DateTime.UtcNow`
- WHEN `POST /api/reservations` is called with that event's ID
- THEN the response is 409 with `ProblemDetails` (type `"event-expired"`)
- AND no reservation row is created

#### Scenario: Reservation for active event succeeds

- GIVEN an event with `Date > DateTime.UtcNow`
- WHEN `POST /api/reservations` is called
- THEN the reservation is created and 201 is returned

#### Scenario: Race — reservation created just before expiry, next attempt rejected

- GIVEN a reservation created at 13:59 for an event starting at 14:00
- WHEN a second reservation attempt occurs at 14:01
- THEN the second attempt is rejected with 409
- AND the first reservation (already persisted) remains valid

### Requirement: EHE-005 — Payment preference guard rejects expired events

`PaymentService.CreatePaymentPreferenceAsync` MUST check `event.IsExpired(DateTime.UtcNow)` before creating a payment preference. If expired, MUST reject with `ProblemDetails` (409 Conflict, type `"event-expired"`). This is defense-in-depth for the race where a reservation exists but the event expires before payment.

#### Scenario: Payment preference for expired event rejected

- GIVEN an event with `Date < DateTime.UtcNow` and an existing valid reservation
- WHEN `POST /api/payments/preference` is called
- THEN the response is 409 with `ProblemDetails` (type `"event-expired"`)
- AND no payment preference is created

#### Scenario: Payment preference for active event succeeds

- GIVEN an active event with a valid reservation
- WHEN `POST /api/payments/preference` is called
- THEN the preference is created and 201 is returned

#### Scenario: Race — reservation at 13:59, payment preference at 14:01

- GIVEN a reservation created at 13:59 for an event starting at 14:00
- WHEN `POST /api/payments/preference` is called at 14:01
- THEN the payment preference is rejected with 409

### Requirement: EHE-011 — ProcessApprovedPaymentAsync remains unchanged

`ProcessApprovedPaymentAsync` MUST NOT check event expiry. A payment already confirmed by the payment provider for a now-expired event SHALL still produce tickets. This preserves the webhook contract and prevents data loss for in-flight transactions.

#### Scenario: Approved payment for expired event still produces tickets

- GIVEN a payment approved by Mercado Pago for an event whose `Date` has since passed
- WHEN `ProcessApprovedPaymentAsync` executes
- THEN tickets are created and the email is sent as normal
- AND no expiry check is performed

#### Scenario: No regression on webhook path

- GIVEN the existing webhook test suite
- WHEN `dotnet test` runs after this change
- THEN all webhook and payment-processing tests remain green

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EHE-004 | reservation-expired-rejected, reservation-active-succeeds, race-reservation-before-expiry |
| EHE-005 | payment-preference-expired-rejected, payment-preference-active-succeeds, race-payment-after-expiry |
| EHE-011 | approved-payment-expired-event-produces-tickets, webhook-no-regression |
