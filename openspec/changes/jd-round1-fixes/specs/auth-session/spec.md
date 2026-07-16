# Auth Session Specification

## Purpose

Migrate JWT storage from localStorage to httpOnly cookies, add session introspection and logout endpoints, fix the API client baseURL logic, and apply rate limiting to authentication and reservation endpoints.

## JD Findings Covered

JD-W3, JD-W22, JD-W25, JD-S2

## Requirements

### REQ-1: JWT Stored in httpOnly Cookie

The system MUST issue JWT tokens as `httpOnly; Secure; SameSite=Lax` cookies instead of returning them in the response body for localStorage storage.

**JD-W3** — Files: `backend/Controllers/AuthController.cs`, `backend/Program.cs`

#### Scenario: Login sets httpOnly cookie

- GIVEN valid credentials submitted to `POST /auth/login`
- WHEN authentication succeeds
- THEN the response includes a `Set-Cookie` header with the JWT token
- AND the cookie has `HttpOnly=true`, `Secure=true`, `SameSite=Lax`

#### Scenario: JWT bearer reads token from cookie

- GIVEN a request with the auth cookie present
- WHEN the JWT bearer middleware processes the request
- THEN the token is extracted from the cookie via `OnMessageReceived`

#### Scenario: Cookie not accessible from JavaScript

- GIVEN the auth cookie is set
- WHEN frontend JavaScript attempts to read `document.cookie`
- THEN the auth cookie is not present in the result

**Tests**: Integration test for cookie attributes; unit test for `OnMessageReceived` extraction.

---

### REQ-2: Session Introspection Endpoint

The system MUST provide `GET /auth/me` to return the authenticated user's identity without exposing the cookie to JavaScript.

**JD-W3** — File: `backend/Controllers/AuthController.cs`

#### Scenario: Authenticated user gets identity

- GIVEN a valid auth cookie is present
- WHEN `GET /auth/me` is called
- THEN the response includes `{ id, email, name, role }`

#### Scenario: Unauthenticated request returns 401

- GIVEN no auth cookie
- WHEN `GET /auth/me` is called
- THEN the server returns 401 Unauthorized

**Tests**: Integration test for authenticated and unauthenticated cases.

---

### REQ-3: Logout Endpoint

The system MUST provide `POST /auth/logout` to clear the auth cookie.

**JD-W3** — File: `backend/Controllers/AuthController.cs`

#### Scenario: Logout clears auth cookie

- GIVEN an authenticated session
- WHEN `POST /auth/logout` is called
- THEN the auth cookie is deleted (MaxAge=0)
- AND subsequent requests are unauthenticated

**Tests**: Integration test verifying cookie deletion.

---

### REQ-4: Frontend Cookie-Based Auth

The system MUST remove all localStorage token logic from the frontend and use `/auth/me` for session recovery.

**JD-W3** — Files: `frontend/src/api/client.js`, `frontend/src/context/AuthProvider.jsx`

#### Scenario: AuthProvider uses /auth/me on mount

- GIVEN the frontend loads with a valid auth cookie
- WHEN `AuthProvider` mounts
- THEN it calls `GET /auth/me` to restore the session

#### Scenario: No localStorage token operations

- GIVEN the frontend codebase
- WHEN client.js and AuthProvider are inspected
- THEN no `localStorage.getItem("token")` or `localStorage.setItem("token")` calls exist

**Tests**: Frontend test verifying `/auth/me` call on mount; grep-based test for localStorage removal.

---

### REQ-5: API Client BaseURL Fix

The system MUST use `VITE_API_BASE_URL` as mandatory in production and fall back to `localhost:5193` only in development.

**JD-W22** — File: `frontend/src/api/client.js`

#### Scenario: Production requires VITE_API_BASE_URL

- GIVEN `import.meta.env.PROD` is true and `VITE_API_BASE_URL` is set
- WHEN the API client is initialized
- THEN `baseURL` equals `VITE_API_BASE_URL`

#### Scenario: Development falls back to localhost

- GIVEN `import.meta.env.DEV` is true and `VITE_API_BASE_URL` is not set
- WHEN the API client is initialized
- THEN `baseURL` defaults to `http://localhost:5193`

**Tests**: Unit test for both env branches.

---

### REQ-6: Rate Limiting on Login and Reservations

The system MUST apply rate limiting to `POST /auth/login` and `POST /api/reservations`.

**JD-W25, JD-S2** — Files: `backend/Program.cs`, `backend/Controllers/AuthController.cs`, `backend/Controllers/ReservationController.cs`

#### Scenario: Login rate limit enforced

- GIVEN more than 10 `POST /auth/login` requests from the same IP within 1 minute
- WHEN the 11th request arrives
- THEN the server returns 429 Too Many Requests (SlidingWindow)

#### Scenario: Reservation rate limit enforced

- GIVEN more than 5 `POST /api/reservations` requests from the same IP within 1 minute
- WHEN the 6th request arrives
- THEN the server returns 429 Too Many Requests (FixedWindow)

**Tests**: Integration test for both rate limiters with rapid sequential requests.
