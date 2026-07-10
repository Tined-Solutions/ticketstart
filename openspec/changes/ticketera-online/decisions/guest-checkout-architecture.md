# Guest Checkout Architecture Decision

## Status

Accepted — implemented as part of the IDOR fix for `POST /api/payments/create-preference`.

## Context

The `POST /api/payments/create-preference` endpoint is `[AllowAnonymous]` to support guest checkout. It originally accepted only `{ reservationId }`, which made it vulnerable to IDOR: anyone who knew a reservation GUID could create a payment preference for it.

This document records the architectural decision behind the fix and the known debt that remains for post-presentation hardening.

## Decision

Buyers (compradores) are **NOT users**. They do not register or log in. Only Staff, Admin, and Organizador roles have accounts.

The guest checkout flow works as follows:

1. The buyer selects a ticket type on the event detail page.
2. The frontend creates a **single reservation** via `POST /api/reservations`.
3. The backend returns a `ReservationResponse` that includes an HMAC-SHA256 `token` signing the reservation identifier.
4. When the buyer clicks "Pagar con Mercado Pago", the frontend sends `{ reservationId, token }` to `POST /api/payments/create-preference`.
5. The backend validates the token before creating the Mercado Pago preference.
6. If the token is missing or invalid, the backend returns `401 Unauthorized`.

### Token implementation

- Algorithm: HMAC-SHA256.
- Input: reservation identifier (`Guid`).
- Secret: dedicated `Reservation:TokenSecretKey` configuration value (separate from `QRCode:HmacSecretKey`).
- Helper: `TicketeraOnline.Api.Helpers.HmacHelper` is shared by QR code signing, webhook signature validation, and reservation token generation.
- Token is generated in `ReservationService.GenerateReservationToken` and returned in `ReservationResponse.Token`.
- Token is validated in `PaymentService.CreatePaymentPreferenceAsync` before the reservation is loaded.

## Rationale

This is an MVP for presentation. Compradores are transient: they buy tickets and leave. Requiring account creation would add friction and is not necessary for the core purchase flow.

The User model, JWT authentication, and role system remain intact. When the product evolves to support comprador accounts (loyalty, purchase history, refunds), the authentication base is already in place.

## Consequences

### Positive

- Guest checkout is possible without user accounts.
- The IDOR vulnerability is closed: possession of a reservation GUID alone is no longer sufficient to create a preference.
- The existing auth stack is untouched.

### Negative / Debt

- Anonymous endpoints are not rate-limited. This must be addressed post-presentation.
- Audit attribution for guest actions is limited. The webhook currently falls back to `guest@ticketera.com` when no user is associated with the reservation.
- The purchaser email collected in the frontend checkout form is not persisted on the reservation entity; the backend only stores `PurchaserDNI`. Email is derived from `User?.Email`, which is null for guests.
- `back_urls` are not configured in the Mercado Pago preference, so the return behavior depends on Mercado Pago account defaults.
- A real Supabase credential is present in git history and must be rotated plus removed with `git filter-repo`.

## Future Evolution

### Buyer accounts

When comprador accounts are introduced, the flow can evolve incrementally:

- Authenticated buyers continue using their JWT for ownership checks.
- The HMAC token can be replaced or augmented with JWT-based ownership validation.
- Existing reservations created with HMAC tokens remain valid until they expire.

### Multi-ticket-type purchases

The current implementation follows **Opción D**: a single purchase is limited to one ticket type and therefore creates a single reservation and a single Mercado Pago preference.

Two alternative evolution paths were evaluated and are documented in the Engram memory with topic key `architecture/checkout-multi-ticket-type`:

- **Opción B**: allow multiple reservations (one per ticket type) and create one Mercado Pago preference that aggregates them. This requires linking preferences to multiple reservations and updating inventory/release logic.
- **Opción C**: introduce a `ReservationItems` schema where one reservation can contain multiple ticket types. This is the most normalized model but requires larger schema and flow changes.

Opción D was chosen to minimize scope for the presentation while keeping both B and C available as future migrations.

## Related Files

- `backend/Services/IReservationService.cs` — `ReservationResponse.Token`, `GenerateReservationToken`.
- `backend/Services/ReservationService.cs` — token generation.
- `backend/Services/IPaymentService.cs` — `CreatePaymentPreferenceRequest.Token`, updated service signature.
- `backend/Services/PaymentService.cs` — token validation.
- `backend/Controllers/ReservationController.cs` — returns token in `ReservationResponse`.
- `backend/Controllers/PaymentController.cs` — enforces token presence and maps `UnauthorizedAccessException` to 401.
- `backend/Helpers/HmacHelper.cs` — shared HMAC-SHA256 helper.
- `backend/Services/ReservationTokenOptions.cs` — typed configuration options.
- `backend/appsettings.json` — `Reservation:TokenSecretKey`.
- `frontend/src/pages/Checkout.jsx` — captures token and sends it with `create-preference`.

## References

- Engram memory: `sdd/ticketera-online/apply-progress`
- Engram memory: `architecture/guest-checkout`
- Engram memory: `architecture/checkout-multi-ticket-type`
