---
name: backend-security
description: "Trigger: security, auth, CSRF, JWT, HMAC, rate limit, PII, redaction, secret. Apply Ticketera's backend security conventions."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when touching auth, CSRF, tokens, rate limiting, logging, or any sensitive-data handling.

## Hard Rules

- Auth: JWT read from httpOnly cookie (`context.Request.Cookies["token"]`); validate issuer/audience/lifetime/signing key; `ClockSkew = Zero`.
- Mutating requests require the `X-CSRF-PROTECT` header (see `CsrfHeaderMiddleware`); webhook + login are exempt.
- Sign user-visible tokens with HMAC-SHA256 (`HmacHelper`): QR codes, reservation tokens.
- Rate-limit abuse paths (`Resend`, `Login`, `Reservations`); reject with 429.
- Never log PII/secrets: route strings through `LogRedactor`; use `RedactingConsoleFormatter`.
- Secrets via typed `IOptions<T>` + `appsettings.Development.json` (gitignored); never hardcode.
- Keep config guards fail-fast (placeholder JWT key, `@resend.dev` in Production).

## Decision Gates

| Concern | Mechanism |
|---------|-----------|
| Authenticated identity | JWT cookie + `TryGetUserId`/`TryGetUserRole` |
| CSRF on mutating endpoint | `X-CSRF-PROTECT` header |
| External callback (Mercado Pago) | signature validation + CSRF exemption |
| New abuse-prone endpoint | add a rate limiter policy |
| New secret/config | typed `IOptions<T>`, no hardcoding |

## Execution Steps

1. Identify the concern (auth / CSRF / token / rate / secret / PII).
2. Apply the matching mechanism from the table.
3. Add a test proving the security property.
4. Run `dotnet test`.

## Output Contract

Return the mechanism applied, files changed, and the test that proves it.

## References

- `backend/Middleware/CsrfHeaderMiddleware.cs`, `backend/Helpers/HmacHelper.cs`, `backend/Helpers/LogRedactor.cs`, `backend/Program.cs` (auth + rate limiting).
