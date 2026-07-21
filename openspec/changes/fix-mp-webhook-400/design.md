# Design: Fix MP Webhook 400 + Email Resilience

## Technical Approach

Replace the mismatched `WebhookPayload` model (IPaymentService.cs:61-66) with MP's real
`{action,type,data:{id}}` envelope, add a new `IMercadoPagoClient.GetPaymentByIdAsync`
(`GET /v1/payments/{id}`), and refactor `ProcessWebhookAsync` (PaymentService.cs:143-219) so
it extracts `data.id`, fetches the real status/external_reference, and reuses the existing
approval/failure/idempotency logic (`ProcessApprovedPaymentAsync` 221-324, `ConfirmPaymentAsync`
385-438). The controller always returns 200 to MP (no 400 path). Email failures
(PaymentService.cs:268-278) are promoted from log-only to log+queue into a new
`pending_email_send` Supabase table; a new admin-gated `POST /api/payments/emails/retry-pending`
endpoint replays them. A `Resend:FromName` config knob is added and a startup PROD GATE blocks
shipping with `tickets@resend.dev`.

## Architecture Decisions

| # | Decision | Choice | Alternatives | Rationale |
|---|----------|--------|--------------|----------|
| 1 | Envelope JSON mapping | Per-property `[JsonPropertyName]` on new DTOs | Global `JsonNamingPolicy.SnakeCaseLower` in `Program.cs` | Existing pipeline uses default options; per-property attrs are explicit, local, and cannot regress other endpoints. Matches the spec's MUST. |
| 2 | GetPaymentByIdAsync null vs throw | `null` on 404; `EnsureSuccessStatusCode` on other non-2xx | null on all non-2xx | Follows spec exactly; 404 = race/unknown id (logged warning + 200 ACK); 5xx/auth errors still throw so they surface in logs. Consistent with `GetPreferenceAsync` (MercadoPagoClient.cs:120-121) style. |
| 3 | Idempotency | Reuse existing `Transactions.MercadoPagoId` check (PaymentService.cs:224-232 + 407-413) | New dedup layer | Spec confirms both webhook and confirm paths converge on `ProcessApprovedPaymentAsync` with same MP id. Unique index already enforced (migration `20260715190343_UniqueTransactionMercadoPagoId`). |
| 4 | Migration approach | EF Core migration in `backend/Migrations/` + raw SQL via `migrationBuilder.Sql()` for RLS + indexes | Raw Supabase CLI migration | Repo convention is EF Core (`backend/Migrations/*.cs`); no `supabase/migrations/` dir exists. RLS cannot be expressed in EF fluent API → raw SQL block. |
| 5 | Table naming | `PendingEmailSend` entity mapped with `.ToTable("pending_email_send")` | EF default `PendingEmailSend` table | Spec fixes the table name; raw SQL RLS policy must reference exact name. |
| 6 | `ticket_ids` storage | `List<Guid>` + `.HasColumnType("uuid[]")` (Npgsql) | JSON column | Npgsql natively maps `List<Guid>` ↔ `uuid[]`; queryable & immutable. Matches spec's uuid[] requirement. |
| 7 | Retry endpoint placement | New action on `PaymentController` (`api/payments/emails/retry-pending`) | New controller | Keeps payment concern together; class already has `_paymentService` + audit wiring. Endpoint-level `[Authorize(Policy = "RequireAdminRole")]` inherits the Admin policy used by AdminController.cs:14. |
| 8 | PROD GATE location | Inline in `Program.cs` after `GetRequiredValue("Resend","FromEmail")` (Program.cs:60-62) | Hosted service / options validation | Mirrors the existing JWT secret guard (Program.cs:92-94) — fail fast at startup with clear message. No DI lifecycles to manage. |
| 9 | FromName composition | `EmailService` builds `From = $"{FromName} <{FromEmail}>"` when FromName set | New `Sender` object in `ResendEmailRequest` | Minimal blast radius; Resend accepts RFC-5322 `"Name <addr>"` in `from`. One property added. |
| 10 | Webhook response contract | Always `200 OK` to MP for any non-fatal input; catch-all wraps body | Propagate 400/500 | 400 → MP retries forever (the bug). Spec scenario "Missing data.id → 200 ACK". Only `Unauthorized` for bad signature is still returned, but spec recommends ACK there too — see Open Questions. |

## Data Flow

### Webhook flow

```
MP ──POST {action,type,data:{id}}──▶ PaymentController.Webhook()
   │  (rawBody for HMAC)
   ├─ deserialize MercadoPagoWebhookEnvelope
   ├─ if Data?.Id null → log warn, return 200 {status:"ignored"}
   ├─ PaymentService.ProcessWebhookAsync(envelope, signature, rawBody)
   │     ├─ if signature present → HmacHelper.ValidateWebhookSignature (existing)
   │     ├─ mp = IMercadoPagoClient.GetPaymentByIdAsync(Data.Id)   ← NEW
   │     │     null → log warn, return WebhookResult{Success=true, ignored}
   │     ├─ Guid.TryParse(mp.ExternalReference) → reservationId
   │     ├─ load reservation (Include TicketType, User)
   │     └─ mp.Status == "approved" ? ProcessApprovedPaymentAsync (existing)
   │                                   : ProcessFailedPaymentAsync (existing)
   └─ controller: always 200 OK {paymentId} (catch-all → 200 + log)
```

### Email failure → queue → retry flow

```
ProcessApprovedPaymentAsync (PaymentService.cs:268-278 catch block)
   ├─ _logger.LogError  (existing)
   ├─ QueueEmailRetryAsync(reservationId, paymentId, email, ticketIds, ex.Message)  ← NEW
   │     └─ INSERT pending_email_send (status='pending', attempts=0)
   └─ return WebhookResult{Success=true, PaymentId}   ← still 200 to MP

Admin ──POST /api/payments/emails/retry-pending (X-CSRF-PROTECT + JWT cookie)──▶
   ├─ PaymentService.RetryPendingEmailsAsync()  ← NEW
   │     SELECT * FROM pending_email_send
   │       WHERE status='pending' AND attempts < max_attempts
   │       ORDER BY created_at
   │     foreach row:
   │       try: SendTicketEmailAsync(row.recipient_email, load Tickets by row.ticket_ids)
   │             → row.status='sent', row.attempts++
   │       catch ex: row.attempts++, row.last_error=ex.Message, row.last_attempt_at=now
   │             if attempts == max_attempts → row.status='exhausted'
   └─ return RetryPendingEmailsResponse {attempted, sent, failed, exhausted}
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `backend/Services/IPaymentService.cs` | Modify | Remove `WebhookPayload` (61-66). Change `ProcessWebhookAsync` signature to accept `MercadoPagoWebhookEnvelope`. Add `QueueEmailRetryAsync`, `RetryPendingEmailsAsync`. Add new types: `MercadoPagoWebhookEnvelope`, `MercadoPagoWebhookData`, `RetryPendingEmailsResponse`. Keep `WebhookResult`/`WebhookFailureType`/`RefundResult`/`PaymentPreference`. |
| `backend/Services/IMercadoPagoClient.cs` | Modify | Add `GetPaymentByIdAsync(string paymentId, CT)` + `MercadoPagoPaymentDetail` class. |
| `backend/Services/MercadoPagoClient.cs` | Modify | Implement `GetPaymentByIdAsync` → `GET v1/payments/{id}`; null on 404, `EnsureSuccessStatusCode` otherwise; reuse existing Bearer auth (28) and parse style (124-130). |
| `backend/Services/PaymentService.cs` | Modify | Refactor `ProcessWebhookAsync` (143-219) to envelope + fetch. Extend email catch (268-278) with `QueueEmailRetryAsync`. Add `QueueEmailRetryAsync` + `RetryPendingEmailsAsync` implementations. |
| `backend/Services/IEmailService.cs` | Modify (doc only) | No signature change; note in XML doc it is now called from PaymentService (webhook) AND `RetryPendingEmailsAsync` (retry path). |
| `backend/Services/ResendOptions.cs` | Modify | Add `public string FromName { get; set; } = string.Empty;` |
| `backend/Services/EmailService.cs` | Modify | Compose `From` in all three `Send*Async` (54-60, 103-109, 138-144): `var from = string.IsNullOrWhiteSpace(_options.FromName) ? _options.FromEmail : $"{_options.FromName} <{_options.FromEmail}>";` |
| `backend/Controllers/PaymentController.cs` | Modify | `Webhook()` (94-159) deserializes `MercadoPagoWebhookEnvelope`, removes 400 branch (120-124), catch-all returns 200. Add new action `RetryPendingEmails` with `[HttpPost("emails/retry-pending")] [Authorize(Policy = "RequireAdminRole")]`. |
| `backend/Data/ApplicationDbContext.cs` | Modify | Add `DbSet<PendingEmailSend> PendingEmailSends`. Configure entity: PK `Id`, FK → `Reservations.Id` `OnDelete(Cascade)`, `.ToTable("pending_email_send")`, `ticket_ids` `.HasColumnType("uuid[]")`, indexes on `Status` + `CreatedAt`. |
| `backend/Models/PendingEmailSend.cs` | Create | EF entity (see Contracts). |
| `backend/Migrations/<ts>_AddPendingEmailSend.cs` | Create | EF migration: `CreateTable` + raw SQL via `migrationBuilder.Sql()` for RLS + `CREATE INDEX idx_pending_email_send_status_created_at` + `ALTER TABLE ... ENABLE ROW LEVEL SECURITY` + `DROP POLICY` fallback. |
| `backend/Program.cs` | Modify | After `resendFromEmail = GetRequiredValue(resendSettings, "FromEmail")` (Program.cs:62) add PROD GATE: `if (builder.Environment.IsProduction() && resendFromEmail.Equals("tickets@resend.dev", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Resend:FromEmail must not be sandbox value 'tickets@resend.dev' in Production");` Bind `Resend:FromName` (optional). |
| `backend/appsettings.json` | Modify | Add `"FromName": ""` under `Resend`. |
| `backend/appsettings.Development.json` | Modify | Add `"FromName": "Ticketera Dev"` under `Resend` (dev only). |
| `backend/Tests/PaymentServiceWebhookTests.cs` | Create | RED tests: real envelope accepted, missing data.id → 200 ignored, approved via fetch → tickets + email + queue on email failure, duplicate → idempotent 200, rejected status, GetPaymentByIdAsync 404 → null. |
| `backend/Tests/PendingEmailRetryTests.cs` | Create | RED tests: queue insert fields, retry sends + marks sent, exhaustion at max_attempts, per-row try/catch isolation. |
| `backend/Tests/ConfigValidationProdGateTests.cs` | Create | RED test: prod + `tickets@resend.dev` → throws; dev → accepts. |

## Interfaces / Contracts

```csharp
// IMercadoPagoClient.cs
public interface IMercadoPagoClient
{
    // existing members unchanged
    Task<MercadoPagoPreferenceResponse> CreatePreferenceAsync(MercadoPagoPreferenceRequest request, CancellationToken cancellationToken = default);
    Task<MercadoPagoRefundResponse> RefundPaymentAsync(string paymentId, decimal amount, CancellationToken cancellationToken = default);
    Task<MercadoPagoPreferenceDetail?> GetPreferenceAsync(string preferenceId, CancellationToken cancellationToken = default);
    Task<List<MercadoPagoPaymentInfo>> SearchPaymentsByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default);

    /// NEW: GET /v1/payments/{id}. null on 404, throws on other non-2xx.
    Task<MercadoPagoPaymentDetail?> GetPaymentByIdAsync(string paymentId, CancellationToken cancellationToken = default);
}

/// Response from GET /v1/payments/{id}. Fields per MP payments API.
public class MercadoPagoPaymentDetail
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;           // approved|rejected|pending|...
    public string ExternalReference { get; set; } = string.Empty; // reservation guid
    public decimal TransactionAmount { get; set; }
    public string? CurrencyId { get; set; }
    public string? DateApproved { get; set; }                     // ISO 8601 string from MP
    public string? PaymentMethodId { get; set; }
    public string? PayerEmail { get; set; }
}
```

```csharp
// IPaymentService.cs — envelope DTOs (snake_case via [JsonPropertyName])
public class MercadoPagoWebhookEnvelope
{
    [JsonPropertyName("action")]        public string Action { get; set; } = string.Empty;
    [JsonPropertyName("api_version")]   public string? ApiVersion { get; set; }
    [JsonPropertyName("data")]          public MercadoPagoWebhookData? Data { get; set; }
    [JsonPropertyName("date_created")]  public string? DateCreated { get; set; }
    [JsonPropertyName("id")]            public string? Id { get; set; }       // webhook id, not payment id
    [JsonPropertyName("live_mode")]     public bool? LiveMode { get; set; }
    [JsonPropertyName("type")]          public string? Type { get; set; }
    [JsonPropertyName("user_id")]       public string? UserId { get; set; }
}

public class MercadoPagoWebhookData
{
    [JsonPropertyName("id")]            public string? Id { get; set; }       // ← canonical MP payment id
}

/// NOTE: legacy WebhookPayload (IPaymentService.cs:61-66) is DELETED.

public interface IPaymentService
{
    Task<PaymentPreference> CreatePaymentPreferenceAsync(Guid reservationId, string token); // unchanged
    Task<WebhookResult> ProcessWebhookAsync(MercadoPagoWebhookEnvelope payload, string signature, byte[]? rawBody = null); // changed sig
    Task<RefundResult> InitiateRefundAsync(string mercadoPagoId, decimal amount, Guid reservationId); // unchanged
    Task<WebhookResult> ConfirmPaymentAsync(string preferenceId); // unchanged

    /// NEW: persist a queued email retry. Idempotent insert (one row per failure).
    Task QueueEmailRetryAsync(Guid reservationId, string paymentId, string recipientEmail, Guid[] ticketIds, string errorMessage);

    /// NEW: replay pending rows. Returns summary counts.
    Task<RetryPendingEmailsResponse> RetryPendingEmailsAsync();
}

public class RetryPendingEmailsResponse
{
    public int Attempted { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Exhausted { get; set; }
}
```

```csharp
// Models/PendingEmailSend.cs
public class PendingEmailSend
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public List<Guid> TicketIds { get; set; } = new();  // mapped to uuid[]
    public string? LastError { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public string Status { get; set; } = "pending";    // pending|sent|exhausted
    public DateTime? LastAttemptAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Reservation Reservation { get; set; } = null!;
}
```

```csharp
// PaymentController.cs — updated Webhook + new retry endpoint
[HttpPost("webhook")]
[AllowAnonymous]
public async Task<IActionResult> Webhook([FromHeader(Name = "x-signature")] string? signature = null)
{
    MercadoPagoWebhookEnvelope envelope;
    byte[] rawBody;
    try
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var bodyString = await reader.ReadToEndAsync();
        rawBody = Encoding.UTF8.GetBytes(bodyString);
        Request.Body.Position = 0;
        envelope = JsonSerializer.Deserialize<MercadoPagoWebhookEnvelope>(bodyString) ?? new();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to read webhook body — ACKing 200");
        return Ok(new { status = "ignored" });
    }

    if (string.IsNullOrEmpty(envelope.Data?.Id))
    {
        _logger.LogWarning("Webhook received without data.id — ACKing 200. Action={Action}", envelope.Action);
        return Ok(new { status = "ignored" });
    }

    try
    {
        var result = await _paymentService.ProcessWebhookAsync(envelope, signature ?? string.Empty, rawBody);
        // audit log unchanged
        return Ok(new { paymentId = result.PaymentId, status = result.Success ? "ok" : "failed" });
    }
    catch (Exception ex)
    {
        // Catch-all: NEVER surface non-200 to MP (400 caused infinite retries)
        _logger.LogError(ex, "Unexpected error processing webhook for payment {PaymentId} — ACKing 200", envelope.Data.Id);
        return Ok(new { status = "error_acknowledged" });
    }
}

[HttpPost("emails/retry-pending")]
[Authorize(Policy = "RequireAdminRole")]
public async Task<IActionResult> RetryPendingEmails()
{
    try
    {
        var summary = await _paymentService.RetryPendingEmailsAsync();
        await TryLogAuditAsync(new AuditLogContext(
            UserId: null, Action: AuditActionType.ProcessWebhook, Resource: AuditResourceType.Payment,
            ResourceId: null, Details: $"Email retry: attempted={summary.Attempted} sent={summary.Sent}",
            UserIdentifier: "Admin"));
        return Ok(summary);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error running pending email retry");
        return StatusCode(500, new { error = "Email retry failed" });
    }
}
```

### Supabase DDL (emitted by the EF migration + raw SQL)

```sql
-- EF CreateTable generates the columns; raw SQL below adds RLS + composite index.
-- Naming follows existing EF convention (no snake_case policy) EXCEPT .ToTable override.

CREATE TABLE "pending_email_send" (
    "Id"               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "ReservationId"    uuid NOT NULL REFERENCES "Reservations"("Id") ON DELETE CASCADE,
    "PaymentId"        text NOT NULL,
    "RecipientEmail"   text NOT NULL,
    "TicketIds"        uuid[] NOT NULL DEFAULT '{}',
    "LastError"        text NULL,
    "Attempts"         integer NOT NULL DEFAULT 0,
    "MaxAttempts"      integer NOT NULL DEFAULT 5,
    "Status"           text NOT NULL DEFAULT 'pending',
    "LastAttemptAt"    timestamptz NULL,
    "CreatedAt"        timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt"        timestamptz NOT NULL DEFAULT now()
);

-- Retry query shape: WHERE status='pending' AND attempts < max_attempts ORDER BY created_at
CREATE INDEX ix_pending_email_send_status_created_at
    ON "pending_email_send" ("Status", "CreatedAt");

-- RLS: service role only (web app uses service-role key via connection string)
ALTER TABLE "pending_email_send" ENABLE ROW LEVEL SECURITY;
CREATE POLICY pending_email_send_service_role_all
    ON "pending_email_send"
    FOR ALL
    TO service_role
    USING (true) WITH CHECK (true);
-- (anon/authenticated get nothing — no SELECT/INSERT/UPDATE policy for them)
```

> **A note on naming**: the rest of the schema uses EF-default PascalCase column
> names (confirmed via `Users`, `Events`, `Reservations` mappings in
> ApplicationDbContext.cs:32-179). For consistency we keep PascalCase columns
> here too; only the table name is snake-cased to match the spec/RLS policy.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit (RED first) | `MercadoPagoWebhookEnvelope` deserializes real MP body incl. `data.id` | xUnit + `JsonSerializer.Deserialize` with a fixture body from MP docs |
| Unit | `GetPaymentByIdAsync` returns `null` on 404, throws on 500 | Moq `HttpMessageHandler` stub |
| Unit | `ProcessWebhookAsync` approved path → calls `ProcessApprovedPaymentAsync`, no new idempotency code | Moq `IMercadoPagoClient` + `ITicketService` + in-memory `DbContext` |
| Unit | `ProcessWebhookAsync` missing `data.id` → `WebhookResult{Success=true, ignored}` | same harness |
| Unit | Email throws → `QueueEmailRetryAsync` inserts `pending_email_send` row, returns `Success=true` | in-memory `DbContext` assertion on `PendingEmailSends` DbSet |
| Unit | Duplicate webhook (`Transactions.MercadoPagoId` exists) → idempotent 200, no new rows | seed `Transaction` then re-run |
| Unit | `RetryPendingEmailsAsync`: pending row → sent; failing row → attempts++; attempts==max → `exhausted`; per-row try/catch isolation | in-memory `DbContext` + Moq `IEmailService` throwing for N rows |
| Unit | PROD GATE: `ASPNETCORE_ENVIRONMENT=Production` + `Resend:FromEmail=tickets@resend.dev` → startup throws; Dev → OK | `WebApplicationFactory<Program>` with env override (existing pattern in `DatabaseConfigurationTests.cs`/`ConfigValidationTests.cs`) |
| Integration | `POST /api/payments/webhook` with real body returns 200 (incl. malformed & missing data.id) | `WebApplicationFactory<Program>` + `HttpClient` |
| Integration | `POST /api/payments/emails/retry-pending` requires Admin cookie → 401 anonymous, 200 admin | existing `AuthCookieTests.cs` pattern |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary. All new endpoints are standard ASP.NET Core MVC actions on the existing `PaymentController`. No `Process.Start`, no shell, no dynamic routing.

## Migration / Rollout

- **EF migration**: `dotnet ef migrations add AddPendingEmailSend` in `backend/` (follows `20260717163535_AddRowVersionDefault` convention). Hand-edit the generated `Up` to append the `migrationBuilder.Sql(...)` RLS + index blocks above (EF fluent cannot express RLS policies). The `Down` drops indexes + policy then the table.
- **Apply**: `dotnet ef database update` against the `MigrationConnection` (Port 5432) — same pattern the existing migrations use (ApplicationDbContext.cs:9-10).
- **Backward compatibility**: `WebhookPayload` deletion is breaking for any external caller reading the API doc, but MP is the only caller and never actually populated the old shape (all 400s). No feature flag needed.
- **Phased rollout**: ship webhook+retry as one change. The retry table has zero rows on day 1; the endpoint is inert until the first email failure. `Resend:FromName` defaults to `""` so existing deployments keep the bare-address behavior.
- **Rollback**: revert migration (`dotnet ef database update <prev>`) drops the table; revert code restores the old `WebhookPayload`. No data loss elsewhere — `pending_email_send` is a pure retry log.

## Open Questions

- [ ] Should a bad webhook signature still return 401 (current behavior, PaymentController.cs:140-144) or 200 ACK? Spec leans 200 (MP retries on 4xx). **Decision pending**: keep 401 for now — it is not the reported bug and changing it needs a security review; revisit in a follow-up.
- [ ] Unique index on `pending_email_send(reservation_id)` to prevent duplicate queue rows on repeated email failures for the same reservation? Spec is silent. Current design allows multiple pending rows (one per failure event); the retry path picks oldest first. Open for team input.
- [ ] Should the retry endpoint also be throttled by the existing `Resend` fixed-window limiter (Program.cs:176-182)? Admin-triggered; likely **should not** share the buyer-facing limiter. Decision: leave outside any named limiter (no `[EnableRateLimiting]` attribute) — admin is trusted.