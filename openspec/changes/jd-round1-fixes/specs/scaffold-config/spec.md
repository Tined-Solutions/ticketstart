# Scaffold & Configuration Specification

## Purpose

Remove scaffold/template artifacts left from .NET project generation and harden startup configuration validation to prevent placeholder values, parse failures, and insecure defaults from reaching production.

## JD Findings Covered

JD-C7, JD-S1, JD-S7, JD-S8, JD-W16, JD-W18, JD-SG15

## Requirements

### REQ-1: Scaffold Artifact Removal

The system MUST NOT contain any scaffold/template endpoints or diagnostic controllers in production code.

**JD-C7** — Files: `backend/Program.cs`, `backend/Controllers/TestAuthorizationController.cs`

#### Scenario: WeatherForecast endpoint removed

- GIVEN the application is running
- WHEN a client requests `GET /weatherforecast`
- THEN the server returns 404 Not Found

#### Scenario: TestAuthorizationController removed

- GIVEN the application is running
- WHEN a client requests any `/api/testauthorization/*` endpoint
- THEN the server returns 404 Not Found

**Tests**: Integration test verifying both endpoints return 404.

---

### REQ-2: JWT Secret Placeholder Rejection

The system MUST reject JWT secret keys that are placeholder values at startup.

**JD-S1** — Files: `backend/appsettings.json`, `backend/Program.cs`

#### Scenario: Placeholder secret rejected at startup

- GIVEN `Jwt:SecretKey` starts with `YOUR_` or equals the template default
- WHEN the application starts
- THEN the application throws an exception and refuses to start

#### Scenario: Valid secret accepted

- GIVEN `Jwt:SecretKey` is >= 32 characters and does not start with `YOUR_`
- WHEN the application starts
- THEN startup completes successfully

**Tests**: Unit test for validation logic; integration test for startup failure.

---

### REQ-3: Safe Configuration Parsing

The system MUST use defensive parsing for numeric configuration values and delegate-based HttpClient configuration.

**JD-S7, JD-S8** — Files: `backend/Services/AuthService.cs`, `backend/Services/MercadoPagoClient.cs`, `backend/Program.cs`

#### Scenario: Non-numeric ExpirationMinutes falls back safely

- GIVEN `Jwt:ExpirationMinutes` is set to a non-numeric value
- WHEN a JWT token is generated
- THEN the expiration defaults to 1440 minutes without throwing

#### Scenario: HttpClient BaseAddress set via AddHttpClient delegate

- GIVEN `MercadoPagoClient` is registered via `AddHttpClient<T>`
- WHEN the client is resolved from DI
- THEN `BaseAddress` is set in the delegate, not in the constructor

**Tests**: Unit test for `int.TryParse` fallback; unit test verifying constructor does not set `BaseAddress`.

---

### REQ-4: StackTrace Redaction in Error Logs

The system MUST NOT log full exception stack traces in the global exception handler response.

**JD-W16** — File: `backend/Middleware/GlobalExceptionHandler.cs`

#### Scenario: StackTrace not exposed in logs

- GIVEN an unhandled exception occurs
- WHEN the GlobalExceptionHandler processes it
- THEN only `exception.Message` is logged as the primary message
- AND `StackTrace` is emitted as a separate structured property (filterable in production)

**Tests**: Unit test verifying log output does not contain `StackTrace` in the message field.

---

### REQ-5: Unified Password Minimum Length

The system MUST enforce a minimum password length of 8 characters on both backend and frontend.

**JD-W18** — File: `backend/Services/AuthService.cs`

#### Scenario: Password shorter than 8 characters rejected

- GIVEN a user submits a password with 7 characters
- WHEN the registration or password-change endpoint is called
- THEN the server returns 400 Bad Request with a validation error

**Tests**: Unit test for password validation boundary (7 rejected, 8 accepted).

---

### REQ-6: GetRequiredValue Configuration Helper

The system MUST extract a shared `GetRequiredValue` helper to eliminate duplicated config validation in `Program.cs`.

**JD-SG15** — File: `backend/Program.cs`

#### Scenario: Missing required config value throws at startup

- GIVEN a required configuration key is absent or empty
- WHEN `GetRequiredValue` is called for that key
- THEN an `InvalidOperationException` is thrown with the key name

#### Scenario: Present config value returned

- GIVEN a required configuration key has a non-empty value
- WHEN `GetRequiredValue` is called
- THEN the value is returned

**Tests**: Unit test for both missing and present cases.
