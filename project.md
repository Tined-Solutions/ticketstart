# Ticketera Online — Project Context

Event ticketing MVP: ASP.NET Core backend + React/Vite frontend, Supabase (PostgreSQL) + Cloudflare R2 (images), JWT auth, Mercado Pago payments, HMAC-signed QR tickets, Resend email. Backend is feature-rich and test-covered; frontend is scaffolded only.

## Quick path

1. `cd backend && dotnet test` — run the suite. Expect **202 passing**, 1 pre-existing flaky (`VerifyDatabaseSchema` needs live Supabase, non-blocking).
2. `cd backend && dotnet run` — start the API. Swagger at `/swagger`.
3. `cd frontend && npm run dev` — start Vite (UI is scaffold-only, no app code yet).
4. SDD artifacts live in `openspec/changes/ticketera-online/` — `tasks.md` is the source of truth for what's done.

## Stack

| Layer | Tech | Testable? |
|-------|------|-----------|
| Backend | ASP.NET Core **net9.0**, EF Core (InMemory for tests) | Yes — xUnit + Moq + FsCheck + WebApplicationFactory |
| Frontend | React 19 + Vite 8 (plain JSX, no TS) | No — no test runner configured |
| DB | Supabase (PostgreSQL) | Live tenant needed for `VerifyDatabaseSchema` |
| Storage | Cloudflare R2 (event images) | — |
| Payments | Mercado Pago (preferences + webhooks) | Mocked in tests |
| Auth | JWT | Unit-tested |
| Email | Resend | Not yet implemented |

## Repo layout

```
backend/        ASP.NET Core API (Models, Services, Controllers, Data, Tests)
frontend/       Vite + React scaffold (no app code yet)
openspec/       SDD artifacts — config.yaml + changes/ticketera-online/
.atl/           Skill registry
```

## Conventions

| Topic | Convention |
|-------|-----------|
| Service pattern | `IXxx` interface + `XxxService` impl in `backend/Services/`; DI registered in `Program.cs` |
| Testing | Strict TDD for backend (Red → Green → Refactor). Property tests via FsCheck for correctness invariants |
| Config secrets | Typed `IOptions<T>` (e.g. `MercadoPagoOptions`); never hardcode — use `appsettings.Development.json` |
| DB config | EF Core `OnModelCreating` in `Data/ApplicationDbContext.cs`; migrations in `backend/Migrations/` |
| Specs | OpenSpec delta specs under `openspec/changes/<change>/specs/<area>/spec.md` |

## Status (2026-07-01)

| Milestone | State |
|-----------|-------|
| Tasks 1-11 (auth, events, reservations, QR/tickets, expiration) | Done — 188 tests |
| Task 12 (Mercado Pago payments + webhooks) | Done — +14 tests, verified pass-with-warnings |
| Tasks 13-32 (email, metrics, admin, error handling, frontend) | Not started |

### Known debt

- **Task 12.6 (tracked):** `PaymentService.ProcessApprovedPaymentAsync` creates tickets with placeholder DNI `"00000000"` because `Reservation` has no `PurchaserDNI` field. This silently breaks `LookupTicketsAsync` (filters by email + DNI) for production tickets. Full fix scope in `openspec/changes/ticketera-online/tasks.md` under 12.6.
- Stale README claims ASP.NET Core 8.0; project targets net9.0.
- `WeatherForecast` scaffold endpoint still in `Program.cs`.
- Task 12 diff is 1145 insertions — 43% over the 800-line review budget; needs maintainer `size:exception` before a single PR.

## Checklist (verify before trusting this doc)

- [ ] `dotnet test` from `backend/` returns 202 passing
- [ ] `openspec/changes/ticketera-online/tasks.md` reflects actual code state
- [ ] No secrets committed in `appsettings*.json`

## Next step

Continue SDD with Task 12.6 (DNI fix) or Task 13 (checkpoint) → Task 14 (email). Frontend work (Tasks ~23-32) needs a test framework chosen first — vitest is the natural pick.
