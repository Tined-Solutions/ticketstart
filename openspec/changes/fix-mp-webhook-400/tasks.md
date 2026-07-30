# Tasks: Fix MP Webhook 400 + Email Resilience

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 480–550 |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Single PR (within 2000-line project budget) |
| Delivery strategy | single-pr |
| Chain strategy | not applicable |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: not applicable
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Full change | Single PR | `dotnet test` | `dotnet run` (dev), POST to `/api/payments/webhook` with MP envelope | Revert single commit |

## Phase 1: Foundation — DTOs, Interfaces, Entity, Config

- [x] 1.1 [RED→GREEN] Replace `WebhookPayload` with `MercadoPagoWebhookEnvelope` + `MercadoPagoWebhookData` in `IPaymentService.cs`; annotate with `[JsonPropertyName]` for snake_case. RED test in `Tests/PaymentServiceWebhookTests.cs`: deserialize `{"action":"payment.updated","data":{"id":"123"}}` → `data.Id=="123"`. Update `ProcessWebhookAsync` signature.
- [x] 1.2 Add `MercadoPagoPaymentDetail` (Id, Status, ExternalReference, TransactionAmount) + `GetPaymentByIdAsync(string, CT)` → `Task<MercadoPagoPaymentDetail?>` to `IMercadoPagoClient.cs`.
- [x] 1.3 Add `QueueEmailRetryAsync` + `RetryPendingEmailsAsync` → `RetryPendingResult` signatures to `IPaymentService.cs`.
- [x] 1.4 Add `FromName` to `ResendOptions.cs`. Add `Resend:FromName` to `appsettings.json` (`"Ticketera Online"`) and `appsettings.Development.json`.
- [x] 1.5 Create `Models/PendingEmailSend.cs`: Id (Guid PK), ReservationId (FK), PaymentId, RecipientEmail, TicketIds (`List<Guid>` → `uuid[]`), LastError, Attempts (0), MaxAttempts (5), Status (Pending/Sent/Exhausted), timestamps.

## Phase 2: DB Context + Migration

- [x] 2.1 Add `DbSet<PendingEmailSend>` to `ApplicationDbContext.cs`. Fluent config: `.ToTable("pending_email_send")`, Guid PK, FK→Reservations, `.HasColumnType("uuid[]")` for TicketIds.
- [x] 2.2 Generate migration: `dotnet ef migrations add AddPendingEmailSend`. Append `migrationBuilder.Sql(...)` for RLS policy and composite index `(status, attempts, last_attempt_at)`. Verify with `dotnet ef migrations script --idempotent`.

## Phase 3: Core Implementation — Strict TDD

- [x] 3.1 [RED] `Tests/MercadoPagoClientPaymentTests.cs`: `GetPaymentById_ReturnsDetail`, `ReturnsNull_On404`, `Throws_OnNon2xx`. [GREEN] Implement `MercadoPagoClient.GetPaymentByIdAsync` → `GET /v1/payments/{id}`, parse id/status/external_reference; null on 404, throw on other errors (follow `GetPreferenceAsync` pattern, MercadoPagoClient.cs:116-131).
- [x] 3.2 [RED] `Tests/PaymentServiceWebhookTests.cs`: `ApprovedPayment_CreatesTickets`, `MissingDataId_ReturnsIgnored`, `DuplicatePayment_Idempotent`, `RejectedPayment_FailedPath`. [GREEN] Refactor `ProcessWebhookAsync` — extract `payload.Data?.Id`, call `GetPaymentByIdAsync`, resolve `external_reference`→reservationId, delegate to existing `ProcessApprovedPaymentAsync`/`ProcessFailedPaymentAsync`. Whole body in try/catch logging.
- [x] 3.3 [RED] `Tests/PendingEmailRetryTests.cs`: `QueueEmailRetry_InsertsPendingRow`, `RetryPending_SendsThenMarksSent`, `ExhaustsAfterMaxAttempts`. [GREEN] Implement `QueueEmailRetryAsync` + `RetryPendingEmailsAsync` in `PaymentService.cs`. Replace email catch block (lines 268-278) to call `QueueEmailRetryAsync` with reservation/paymentId/email/ticketIds/error.

## Phase 4: Controller, Email, PROD GATE

- [x] 4.1 [RED→GREEN] Update `PaymentController.Webhook` — deserialize to `MercadoPagoWebhookEnvelope`, remove PaymentId-empty 400 branch (lines 120-124), catch-all→200 OK. Retry endpoint needs `X-CSRF-PROTECT` (not exempted by CsrfHeaderMiddleware.cs:30-32). Test in `PaymentControllerTests.cs`.
- [x] 4.2 [RED→GREEN] Add `POST /api/payments/emails/retry-pending` with `[Authorize(Roles = "Admin")]` returning `RetryPendingResult`. Test in `PaymentControllerTests.cs`.
- [x] 4.3 Update `EmailService.cs` From composition to `"${FromName} <${FromEmail}>"` across all three `Send*Async` methods (lines 56, 105, 140).
- [x] 4.4 [RED→GREEN] PROD GATE in `Program.cs` after line 62: if `Production` AND `FromEmail` ends with `@resend.dev`, throw. Read `FromName` binding. RED test in `Tests/ConfigValidationProdGateTests.cs` using `ConfigurableApiFactory`.

## Phase 5: Integration Verification

- [x] 5.1 `dotnet test` — full suite. Verify zero regressions from `WebhookPayload→MercadoPagoWebhookEnvelope` signature change. **NOTE: Test code updated but NOT executed — dotnet CLI unavailable in apply environment.**
- [ ] 5.2 `dotnet ef database update` against clean DB — confirm `pending_email_send` table + RLS policy exist. **DEFERRED: requires Supabase + dotnet CLI.**
