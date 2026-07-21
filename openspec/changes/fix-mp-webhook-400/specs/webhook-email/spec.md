# Spec: Webhook + Email Resilience — `fix-mp-webhook-400`

## MODIFIED Requirements

### Req: Webhook payload model

`WebhookPayload` MUST match MP's real notification envelope: `{action, type, data:{id}}`. `WebhookData.Id` (string) is the canonical payment id. Deserialization MUST use `JsonNamingPolicy.SnakeCaseLower` or `[JsonPropertyName]` for snake_case fields.

(Previously: expected top-level `PaymentId`, `ExternalReference`, `Status` — a shape MP never sends.)

**Scenario: Real MP envelope → accepted**
- GIVEN MP posts `{"action":"payment.updated","type":"payment","data":{"id":"123456789"}}`
- WHEN deserialized in `PaymentController.Webhook`
- THEN `payload.Data.Id == "123456789"` and no exception thrown

**Scenario: Missing data.id → 200 ACK, no crash**
- GIVEN MP posts `{"action":"payment.updated","type":"payment"}` (no `data.id`)
- WHEN the controller resolves `payload.Data?.Id` as null
- THEN returns `200 OK {status:"ignored"}` with logged warning (no 400)

### Req: Payment status via API fetch

`ProcessWebhookAsync` MUST call `GetPaymentByIdAsync(dataId)` to retrieve `status` and `external_reference` from `GET /v1/payments/{id}`.

(Previously: read `payload.Status`/`ExternalReference` — both always empty from MP.)

**Scenario: Approved → tickets + email + 200**
- GIVEN `GetPaymentByIdAsync("pay_001")` returns `status="approved"`, `external_reference="<guid>"`
- WHEN webhook processes
- THEN `ProcessApprovedPaymentAsync` creates tickets and a `Transaction` with `MercadoPagoId="pay_001"`
- AND `SendTicketEmailAsync` is called
- AND controller returns `200 OK {paymentId:"pay_001"}`

**Scenario: Duplicate webhook → idempotent 200**
- GIVEN `Transaction` with `MercadoPagoId="pay_001"` already exists
- WHEN a second webhook for `data.id="pay_001"` arrives
- THEN `ProcessApprovedPaymentAsync` detects duplicate → returns `{Success=true}` with no new rows
- AND controller returns `200 OK`

**Scenario: Rejected status → failed path + 200**
- GIVEN `GetPaymentByIdAsync("pay_002")` returns `status="rejected"`
- WHEN webhook processes
- THEN `ProcessFailedPaymentAsync` runs; controller returns `200 OK {status:"failed"}`

### Req: Email failure → log + queue

After DB commit, when `SendTicketEmailAsync` throws, MUST log `Error` with reservation/payment/email AND insert into `pending_email_send` (`status='pending'`, `attempts=0`). Webhook MUST still return 200.

(Previously: `PaymentService.cs:268-278` logged only; no retry record.)

**Scenario: Email throws → logged + queued + 200**
- GIVEN `SendTicketEmailAsync` throws `SmtpException` after successful payment commit
- WHEN catch block executes
- THEN `_logger.LogError` fires with `{ReservationId}`, `{paymentId}`, recipient
- AND a `pending_email_send` row is inserted with `status='pending'`, `ticket_ids=[...]`
- AND webhook returns `200 OK`

## ADDED Requirements

### Req: GetPaymentByIdAsync

`IMercadoPagoClient` MUST expose `GetPaymentByIdAsync(string paymentId, CT)` → `GET /v1/payments/{id}` (Bearer auth). Returns `MercadoPagoPaymentDetail?` — `null` on 404, throw on other non-2xx.

**Scenario: Found → detail returned**
- GIVEN MP payment `"123"` exists; WHEN called; THEN returns `Id="123"`, `Status`, `ExternalReference`, `TransactionAmount`

**Scenario: 404 → null**
- GIVEN payment `"nonexistent"` returns 404; WHEN called; THEN `null` (no exception)

### Req: Email retry queue

`pending_email_send` Supabase table: `id` (uuid PK), `reservation_id` (FK → Reservations), `payment_id` (text), `recipient_email` (text), `ticket_ids` (uuid[]), `last_error` (text), `attempts` (int, 0), `max_attempts` (int, 5), `status` (text, 'pending'), `last_attempt_at` (timestamptz), `created_at`, `updated_at`. RLS: service role only.

**Scenario: Failure enqueued**
- GIVEN `QueueEmailRetryAsync(reservationId, paymentId, email, ticketIds, "SmtpException")`
- THEN row inserted with `status='pending'`, `attempts=0`, `last_error='SmtpException'`

### Req: Manual retry endpoint

`POST /api/payments/emails/retry-pending` (admin-gated) MUST query `status='pending' AND attempts < max_attempts`, re-send per row, update status. Returns `{attempted, sent, failed, exhausted}`.

**Scenario: Admin retry → email re-sent**
- GIVEN a `pending` row; WHEN admin calls the endpoint
- THEN `SendTicketEmailAsync` is invoked with stored ticket ids
- AND on success row updated to `status='sent'`

**Scenario: Exhaustion → marked exhausted**
- GIVEN row at `attempts=4, max_attempts=5` and send fails again
- WHEN retry runs; THEN `attempts`→5, `status`→`'exhausted'`

### Req: Resend from-email config

`appsettings.json` MUST have `Resend:FromEmail` and `Resend:FromName`. Prod MUST NOT ship with `tickets@resend.dev`.

**Scenario: Dev → sandbox accepted**
- GIVEN `Resend:FromEmail = "tickets@resend.dev"` in `appsettings.Development.json`
- WHEN email service resolves sender; THEN uses sandbox address

**Scenario: Prod gate → blocked**
- GIVEN `ASPNETCORE_ENVIRONMENT = Production` AND `Resend:FromEmail = "tickets@resend.dev"`
- THEN startup MUST fail with clear error

## REMOVED Requirements

### Req: Old WebhookPayload (PaymentId/ExternalReference/Status top-level)
(Reason: MP never sends these fields at the top level. Model mismatch caused every webhook to return 400.)

### Req: BadRequest (400) on empty PaymentId for webhook path
(Reason: 400 makes MP retry indefinitely. Replaced by 200 ACK with logged warning — stops retries, preserves debuggability.)
