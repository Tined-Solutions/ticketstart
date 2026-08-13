# Feature Flag — HideExpiredEvents Runtime Toggle

**Requirements covered**: EHE-009, EHE-010

## Purpose

A runtime feature flag MUST gate all expiry filtering and purchase guards, enabling instant rollback without redeploy. The backend is the single authority for enforcement; the frontend is decorative only.

## Requirements

### Requirement: EHE-009 — Runtime feature flag gates all expiry behavior

A typed `IOptions<HideExpiredEventsOptions>` bound to `appsettings.json` key `HideExpiredEvents` SHALL gate all filtering (EHE-002, EHE-003) and purchase guards (EHE-004, EHE-005). When `Enabled = false`, the system MUST behave identically to pre-change (no filter, no guard). Toggling MUST NOT require redeploy. The flag MUST default to `true`. A missing config key MUST fail-fast at startup.

#### Scenario: Flag enabled — catalog filters active

- GIVEN `HideExpiredEvents:Enabled = true`
- WHEN `GET /api/events` is called
- THEN expired events are excluded from the response

#### Scenario: Flag disabled — catalog returns all events

- GIVEN `HideExpiredEvents:Enabled = false`
- WHEN `GET /api/events` is called
- THEN all events are returned including expired ones (pre-change behavior)

#### Scenario: Flag disabled — purchase guards inactive

- GIVEN `HideExpiredEvents:Enabled = false` and an expired event
- WHEN `POST /api/reservations` is called for that event
- THEN the reservation succeeds (no 409)

#### Scenario: Flag enabled — purchase guards active

- GIVEN `HideExpiredEvents:Enabled = true` and an expired event
- WHEN `POST /api/reservations` is called
- THEN the response is 409 with `ProblemDetails`

#### Scenario: Missing flag key — fail-fast at startup

- GIVEN `appsettings.json` has no `HideExpiredEvents` section
- WHEN the application starts
- THEN startup throws a configuration exception with a clear message
- AND the application does NOT serve requests

#### Scenario: Default value is true

- GIVEN `appsettings.json` has `HideExpiredEvents:Enabled` not explicitly set
- WHEN the application starts
- THEN the default `true` is applied and filtering is active

### Requirement: EHE-010 — Backend is the authority for expiry enforcement

The backend MUST be the single enforcement point for all expiry rules. The frontend MAY display optional "event expired" UX but MUST NOT be the enforcement point. All security-critical behavior (filtering, purchase blocking) MUST be validated via backend tests (`dotnet test`). Frontend-only behaviors (EventList/EventDetail expired UX) are OPTIONAL and verified manually.

#### Scenario: Backend test proves catalog filtering (Strict TDD)

- GIVEN the backend test suite
- WHEN `dotnet test` runs
- THEN tests verify expired events are excluded from `GET /api/events`

#### Scenario: Backend test proves purchase guard (Strict TDD)

- GIVEN the backend test suite
- WHEN `dotnet test` runs
- THEN tests verify reservation and payment preference reject expired events

#### Scenario: Frontend expired UX is optional (manual verification)

- GIVEN a buyer viewing an expired event URL directly
- WHEN the page loads and `GET /api/events/{id}` returns 404
- THEN the frontend MAY show an "event no longer available" message
- NOTE: No frontend test runner — manual verification only

#### Scenario: Frontend cannot bypass backend enforcement

- GIVEN a modified frontend that skips client-side expiry checks
- WHEN it calls `POST /api/reservations` for an expired event
- THEN the backend still returns 409 regardless of client behavior

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EHE-009 | flag-enabled-catalog-filters, flag-disabled-catalog-all, flag-disabled-purchase-open, flag-enabled-purchase-guarded, missing-key-fail-fast, default-true |
| EHE-010 | backend-test-catalog, backend-test-purchase, frontend-ux-optional, frontend-cannot-bypass |
