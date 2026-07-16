# User Management Specification

## Purpose

Enforce the business rule that only administrators create and manage user accounts. Remove public self-registration, add the `Name` field to the User model, and consolidate email validation.

## JD Findings Covered

JD-C1, JD-S5, JD-SG1, JD-SG10, JD-SG19

## Requirements

### REQ-1: Public Registration Removed

The system MUST NOT expose any public self-registration endpoint.

**JD-C1** — Files: `backend/Controllers/AuthController.cs`, `backend/Services/AuthService.cs`

#### Scenario: POST /auth/register returns 404

- GIVEN the application is running
- WHEN an unauthenticated client sends `POST /auth/register`
- THEN the server returns 404 Not Found (endpoint removed)

#### Scenario: No registration route in API metadata

- GIVEN the application is running
- WHEN API routes are enumerated
- THEN no route matches `POST /auth/register`

**Tests**: Integration test confirming endpoint removal.

---

### REQ-2: Admin-Only User Creation

The system MUST provide `POST /api/admin/users` restricted to users with the Admin role for creating new user accounts with role assignment.

**JD-C1** — Files: `backend/Controllers/AdminController.cs` (new), `backend/Services/AuthService.cs`

#### Scenario: Admin creates a user with a valid role

- GIVEN an authenticated Admin user
- WHEN `POST /api/admin/users` is called with `{ name, email, password, role }` where role is `Organizador`, `Staff`, or `Admin`
- THEN the user is created and 201 Created is returned

#### Scenario: Non-admin user rejected

- GIVEN an authenticated non-Admin user
- WHEN `POST /api/admin/users` is called
- THEN the server returns 403 Forbidden

#### Scenario: Unauthenticated request rejected

- GIVEN no authentication token
- WHEN `POST /api/admin/users` is called
- THEN the server returns 401 Unauthorized

**Tests**: Integration tests for admin success, non-admin 403, anonymous 401.

---

### REQ-3: Name Field on User Model

The system MUST store a `Name` field on the `User` entity.

**JD-SG1** — Files: `backend/Models/User.cs`, EF Core migration

#### Scenario: User created with Name persisted

- GIVEN an admin creates a user with `Name = "Juan Perez"`
- WHEN the user is retrieved from the database
- THEN the `Name` field equals `"Juan Perez"`

**Tests**: Unit test for model property; integration test for migration + persistence.

---

### REQ-4: Auth Tests Migrated to Admin Endpoint

The system MUST replace all tests referencing the removed public registration endpoint with tests targeting `POST /api/admin/users`.

**JD-S5** — File: `backend/Tests/AuthenticationPropertyTests.cs`

#### Scenario: No test references POST /auth/register

- GIVEN the test suite
- WHEN tests are enumerated
- THEN no test references the removed public registration endpoint

#### Scenario: Admin user creation covered by property tests

- GIVEN the test suite
- WHEN property-based tests run
- THEN `POST /api/admin/users` is tested for valid role assignment and admin-only access

**Tests**: Updated `AuthenticationPropertyTests` with admin-endpoint scenarios.

---

### REQ-5: Deduplicated Email Validation

The system MUST use a single shared email validation function across all authentication-related flows.

**JD-SG10** — Files: `backend/Services/AuthService.cs`

#### Scenario: Email validation consistent across endpoints

- GIVEN any endpoint that accepts an email address
- WHEN an invalid email format is submitted
- THEN the same validation error message is returned

**Tests**: Unit test for shared validator covering valid/invalid formats.

---

### REQ-6: Register.jsx Removed from Frontend

The system MUST NOT include the `Register.jsx` page or route in the frontend application.

**JD-SG19** — File: `frontend/src/pages/Register.jsx` (deleted)

#### Scenario: No /register route in frontend

- GIVEN the frontend application is running
- WHEN a user navigates to `/register`
- THEN the 404 page is displayed

**Tests**: Frontend test verifying route removal (or manual verification).
