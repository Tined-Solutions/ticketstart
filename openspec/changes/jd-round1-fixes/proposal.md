# Proposal: JD Round 1 Fixes — Clear Judgment Day Findings

## Intent

Clear every actionable finding from Judgment Day Round 1 (blind dual adversarial review of the ticketera-online codebase, 75 findings documented in `openspec/changes/ticketera-online/tasks.md` lines 790-988). Goal: reach **0 open items** for final judge review. Addresses 8 confirmed CRITICAL, 9 suspect CRITICAL (S10 dismissed as false positive), 33 WARNING, and a curated subset of 24 SUGGESTION fixes — no new product features, only security/integrity/quality fixes.

## Scope

### In Scope
- **Batch 1**: Scaffold cleanup (WeatherForecast, TestAuthorizationController) + config hardening (JWT placeholder, ExpirationMinutes parse, HttpClient BaseAddress, stacktrace logging, password min unify, GetRequiredValue helper).
- **Batch 2**: Remove public registration; admin-only `POST /api/admin/users`; add `Name` to User; migrate auth tests; dedupe email validation.
- **Batch 3**: Atomic stock via `ExecuteUpdateAsync` + `CurrentlyReserved` on TicketType; drop `Include(Tickets)`; `async Task` + `PeriodicTimer` in expiration service.
- **Batch 4**: Full email flow (`PurchaserEmail` end-to-end); idempotency via unique `Transaction.MercadoPagoId`; raw-bytes webhook signature; DB-transaction-wrapped confirmation.
- **Batch 5**: Info-only public lookup (no QR); `POST /api/tickets/resend` with rate limit + CAPTCHA placeholder; remove `GET /api/reservations/{id}`; QR timestamp window validation.
- **Batch 6**: JWT → httpOnly cookie; `/auth/me`; logout endpoint; correct API client baseURL; rate limits on login + reservation creation.
- **Batch 7**: Metrics N×5 → single `GroupBy`; audit log pagination + FK; out-of-band audit failures; `TryGetUserRole` fix; reservation token nonce/timestamp; redact PII; persist IP/User-Agent.
- **Batch 8**: Extract shared formatters; RoleGuard 403 page; EventForm validation; Modal focus trap; ToastProvider `useRef`; StaffScan GUID validation; ErrorBoundary; accessibility fixes.

### Out of Scope
- **Batch 9 (remaining SUGGESTIONS)**: excluded — deferred to reach styles faster.
- **CAPTCHA integration**: placeholder + rate limit only (`TODO` comment); no Cloudflare Turnstile wiring now.
- **New product features, styling/theming work, JWT key rotation, Testcontainers migration, QR image caching**, structured-logging regex rework.

## Capabilities

> `openspec/specs/` is empty — all capabilities below are NEW specs (no delta specs against pre-existing main specs).

### New Capabilities
- `scaffold-config`: Batch 1 — scaffold removal + config validation helpers + startup checks.
- `user-management`: Batch 2 — admin-only user creation/role assignment; `Name` on User; no public registration.
- `reservation-stock`: Batch 3 — atomic stock with `CurrentlyReserved`; precalculated availability; expiration service hardening.
- `payment-pipeline`: Batch 4 — idempotent webhooks, atomic confirmation, full `PurchaserEmail` → ticket → email flow.
- `ticket-lookup`: Batch 5 — info-only public lookup; rate-limited resend; QR timestamp window; reservation endpoint removal.
- `auth-session`: Batch 6 — httpOnly cookie auth; `/auth/me`; logout; login + reservation rate limiting.
- `audit-data-integrity`: Batch 7 — metrics consolidation, audit FK/pagination, PII redaction, IP/UA capture, reservation-token hardening.
- `frontend-quality`: Batch 8 — shared utils, RoleGuard 403, ErrorBoundary, accessibility, StaffScan hardening.

### Modified Capabilities
- None (no existing specs to delta against).

## Approach

8 ordered batches, each verified green before the next: **1 → 2 → 3 → 4 → 5 → 6 → 7 → 8**. Parallel pairs: 2||3 (after 1); 7||8 (after their dependencies). Migrations: **Batch 2** (Name on Users), **Batch 3** (CurrentlyReserved on TicketType — reset to 0, no backfill; active reservations self-reconcile on expiry), **Batch 4** (unique index `Transaction.MercadoPagoId`), **Batch 7** (FK `AuditLog.UserId` → Users). Risk: B1/B8 low, B2/B5/B7 low-med, B3/B4/B6 high (breaking/atomicity). Backend strict TDD (xUnit + FsCheck); frontend Vitest; all tests pass after each batch; single PR at completion.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Program.cs` | Modified | Startup validation, rate limiters, JWT-cookie bearer, scaffold removal |
| `backend/Controllers/*` | Modified | Auth, Reservation, Payment, Ticket, Admin controllers |
| `backend/Services/*` | Modified | Auth, Reservation, Payment, Ticket, Event, Metrics, AuditLog services |
| `backend/Models/{TicketType,Transaction,User,AuditLog}.cs` | Modified | New fields + constraints |
| `backend/Data/ApplicationDbContext.cs` | Modified | Unique index, FK, CurrentlyReserved |
| `backend/Middleware/GlobalExceptionHandler.cs` | Modified | StackTrace redaction |
| `backend/Authorization/EventOwnershipHandler.cs` | Modified | Parameter name from requirement |
| `backend/Tests/*` | Modified | Migrated tests + new TDD coverage |
| `frontend/src/api/client.js` | Modified | Cookie-based auth, baseURL fix |
| `frontend/src/context/AuthProvider.jsx` | Modified | `/auth/me` on mount |
| `frontend/src/pages/*` | Modified | Register removal, TicketLookup info-only, RoleGuard 403, etc. |
| `frontend/src/components/*` | Modified | EventForm, Modal, ToastProvider, Card, ErrorBoundary |
| `frontend/src/lib/{format,apiError}.js` | New | Shared utilities |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Stock migration reset loses in-flight reservations | Low | Accepted by user; 10-min expiry auto-reconciles |
| httpOnly cookie breaks existing mobile/SSR clients | Low | Breaking change accepted; this is a pre-launch app |
| Batch 4 atomicity regression delays webhook processing | Med | DB transaction + email out-of-band; MP retries on 500 |
| Batch 6 CSRF exposure from cookie auth | Med | SameSite=Lax + custom header check on mutating routes |
| Large single-PR review burden (~1500 lines budget) | Med | Ordered batches enable linear review; chained PR option reserved |

## Rollback Plan

Per-batch git revert commits (each batch = self-contained green commit set). DB migrations: bring app offline, `dotnet ef database update <prev-migration>` for the batch migration, then revert code. For Batch 3 `CurrentlyReserved`: column can be dropped safely (no backfill data lost). For Batch 4 unique index: drop index before reverting PaymentService. Feature flag not used (pre-launch); full revert restores localStorage JWT, public registration, QR lookup.

## Dependencies

- Postgres/Supabase connectivity for integration tests.
- Resend email service for Batch 4 email-flow verification.
- Mercado Pago sandbox for Batch 4 webhook signature validation.

## Success Criteria

- [ ] All 8 confirmed CRITICAL (JD-C1..C8) fixed and verified.
- [ ] All actionable suspect CRITICAL (JD-S1..S9, S10 dismissed) fixed.
- [ ] All 33 WARNING (minus rejected/covered) fixed.
- [ ] Curated SUGGESTIONS (Batches 1-8 only) fixed.
- [ ] `dotnet test` green (~333 backend) after each batch and overall.
- [ ] `pnpm vitest` green (~208 frontend) after Frontend batches.
- [ ] Single PR; 0 open JD Round 1 items remaining for final judge review.