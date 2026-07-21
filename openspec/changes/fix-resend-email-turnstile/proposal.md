# Proposal: Fix Resend Tickets Email Flow + Integrate Cloudflare Turnstile

## Intent

The "Reenviar entradas" flow in `TicketLookup` ships dead code. `ResendTicketsByEmailAsync` finds tickets, then runs a `Task.Run` that only logs — `IEmailService` is never injected, no email sent. The frontend "captcha" is a checkbox setting `captchaToken = 'placeholder'`, purely cosmetic. This change makes resend actually send grouped emails and replaces the fake captcha with Cloudflare Turnstile (invisible mode).

## Scope

### In Scope
- Inject `IEmailService` into `TicketService` (constructor + `Program.cs` DI).
- Implement `ResendTicketsByEmailAsync`: group tickets by `Event`, call `SendTicketEmailAsync` once per event reusing `TicketConfirmationTemplate` with subject "Reenvío de tus entradas para {EventName}".
- Add `ITurnstileVerificationService` + impl that POSTs `token + remoteip` to `siteverify` with 5s timeout.
- `ResendTickets` validates Turnstile first; returns `400` on invalid token, `204` on accepted (anti-enumeration).
- Frontend: replace checkbox with invisible Turnstile widget, send real `cf-turnstile-response` token.
- Config: backend `Turnstile:SiteKey`/`SecretKey`, frontend `VITE_TURNSTILE_SITE_KEY`, turnstile script tag.
- Keep existing `[EnableRateLimiting("Resend")]`.

### Out of Scope
- Purchase confirmation, refund flow, other email triggers (unchanged).
- Frontend Vitest setup; Turnstile widget smoke-tested manually only.
- New domain events; backend service `Task<bool>` → `Task` relax only (bool is anti-enumeration artifact).

## Capabilities

### New Capabilities
- `bot-protection-turnstile`: Cloudflare Turnstile — server-side siteverify, frontend invisible widget, config contract, failure semantics, per-endpoint enforcement points.

### Modified Capabilities
- `tickets`: ticket resend SHALL group by event, send one email per event via `IEmailService` reusing `TicketConfirmationTemplate` with resend subject. Ticket lookup endpoint SHALL require valid Turnstile token.

## Approach

1. **Backend infra**: `TurnstileVerificationService` (Scoped) + DI; `Turnstile` config section.
2. **Wire EmailService**: inject into `TicketService`; group tickets by `Event.Id`; replace log-only `Task.Run` with per-group `SendTicketEmailAsync`.
3. **Controller**: validate Turnstile token before service call.
4. **Frontend**: `@marside/react-turnstile` invisible widget; pass token as `captchaToken`.
5. **Commits** (skill `work-unit-commits`): one work unit per layer (Turnstile service+tests, EmailService wiring+tests, controller gate, frontend widget). **PR** (skill `branch-pr`): `single-pr-default`; `size:exception` label if > 2000 lines.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Services/TicketService.cs` | Modified | Inject `IEmailService`; grouped email send |
| `backend/Services/TurnstileVerificationService.cs` (new) | New | siteverify wrapper |
| `backend/Controllers/TicketController.cs` | Modified | Turnstile gate before resend |
| `backend/Program.cs`, `appsettings*.json`, user-secrets | Modified | Register services + `Turnstile:SiteKey`/`SecretKey` |
| `frontend/src/pages/TicketLookup.jsx`, `.env`, `index.html` | Modified | Invisible widget, real token, site key env, script tag |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Turnstile outage blocks resend | Low | 5s timeout on siteverify; rate-limit caps abuse |
| N events → N emails (Resend bill) | Med | Documented user decision (per-event grouping) |
| No FE test runner for widget | Med | Manual smoke; harness recorded N/A in tasks |
| Template semantic reuse | Low | Only EventDetails+tickets passed; subject override |

## Rollback Plan

Revert the single PR. DI registrations clean; `ResendTicketsByEmailAsync` returns to log-only (current broken-but-non-crashing behavior). Frontend revert restores checkbox (also broken, not worse). Remove `Turnstile` config keys. No DB migration, no data loss.

## Dependencies

- Cloudflare Turnstile site key + secret key (user must provision). No new backend packages (HttpClient via IHttpClientFactory).

## Success Criteria

- [ ] 2 events → 2 emails (one per event), each with that event's QR codes, subject "Reenvío de tus entradas para {EventName}"; 1 event → 1 email.
- [ ] Missing/invalid Turnstile token → `400`, no email sent.
- [ ] `dotnet test` passes — existing + new tests for grouping, turnstile verify, controller gate.
- [ ] `[EnableRateLimiting("Resend")]` still triggers under sustained load.
- [ ] Single PR opened; `size:exception` label if changed lines > 2000.