# Admin User Management

**Requirements covered**: AUM-001 … AUM-006

## Purpose

Admins can create users but cannot manage existing accounts: access cannot be revoked without deleting the account row (which `AuditLogs` FK-restrictions depend on), and there is no recovery path when a user loses a password. This change adds **admin role editing** — including a new `UserRole.SinAcceso` revoke role — and **admin-triggered manual password reset**, both audited. Account rows are never deleted; `SinAcceso` is a pure revocation state that grants nothing. Role changes and resets apply on next login (no JWT-revocation middleware), and that behavior is documented in the authorization matrix. Existing `role-access` capabilities are unaffected: no policy grants `SinAcceso`, so EHE-006/007/008 requirements remain valid unchanged.

## Requirements

### Requirement: AUM-001: Admin role editing endpoint

`PUT /api/admin/users/{userId}/role` MUST allow an Admin to set any existing user's role to any `UserRole` value and MUST return the updated user summary (200). It MUST inherit the controller-level `RequireAdminRole` policy and the `X-CSRF-PROTECT` mutating-request requirement. The endpoint MUST return **400** when `targetUserId == caller adminId` (self role-edit guard) and **404** when the target user does not exist. Every successful edit MUST record an `UpdateUserRole` audit entry (`AuditResourceType.User`, `ResourceId` = target user id) whose details MUST NOT contain credentials or unnecessary PII. The account row MUST never be deleted — role editing is the only revoke mechanism.

#### Scenario: Admin changes a user's role

- GIVEN an Admin and an existing non-self user with role `Staff`
- WHEN the Admin calls `PUT /api/admin/users/{userId}/role` with role `Organizador`
- THEN the response is 200 with the updated role and the persisted user row has role `Organizador`
- AND an `UpdateUserRole` audit entry references the target user id

#### Scenario: Admin cannot edit own role

- GIVEN an Admin whose token identifies them as user U
- WHEN the Admin calls `PUT /api/admin/users/U/role` with any role
- THEN the response is 400 and no role change or audit row is persisted

#### Scenario: Role edit for unknown user

- GIVEN an Admin and a userId with no matching account
- WHEN the Admin calls `PUT /api/admin/users/{userId}/role`
- THEN the response is 404 and no role change or audit row is persisted

### Requirement: AUM-002: SinAcceso grants nothing

`UserRole` MUST gain a fourth value `SinAcceso` **appended at enum index 3** (append-only: `User.Role` is int-stored with no conversion, so inserting mid-enum would corrupt existing rows; no migration is required). No authorization policy (`RequireOrganizadorRole`, `RequireScanAccessRole`, `RequireAdminRole`, `EventOwnership`) SHALL grant `SinAcceso` anything: a `SinAcceso` user MUST receive 403 on every role-gated endpoint. Login MUST still succeed for a `SinAcceso` account, and the frontend post-login redirect MUST land on `'/'`. Setting `SinAcceso` MUST NOT delete or disable the account row.

#### Scenario: SinAcceso user is rejected on role-gated endpoints

- GIVEN a user whose role was set to `SinAcceso`
- WHEN the user calls any role-gated endpoint (organizer, staff scan, admin)
- THEN every response is 403

#### Scenario: SinAcceso user can still log in

- GIVEN a user whose password is valid and role is `SinAcceso`
- WHEN the user logs in with correct credentials
- THEN login succeeds and the session cookie carries role `SinAcceso`

#### Scenario: SinAcceso redirect lands on home

- GIVEN a `SinAcceso` user completing login
- WHEN the frontend computes the post-login redirect
- THEN the destination is `'/'` (no organizer, staff, or admin surface is offered)

#### Scenario: Role enum remains append-only

- GIVEN the `UserRole` enum definition in this change
- WHEN existing stored role ints (0, 1, 2) are read after deploy
- THEN they deserialize to `Organizador`, `Staff`, `Admin` unchanged and `SinAcceso` is index 3 (no data migration)

### Requirement: AUM-003: Admin-triggered password reset with response-once credential

`POST /api/admin/users/{userId}/reset-password` MUST generate a temporary password server-side using a cryptographically secure generator (`RandomNumberGenerator`), alphanumeric, 12–16 characters (satisfying the min-8 policy), BCrypt-hash it, persist only the hash, and return the cleartext credential **exactly once** in the response body for out-of-band handoff. Admins MUST NOT see, set, or choose user passwords. Every successful reset MUST record a `ResetPassword` audit entry (`AuditResourceType.User`) whose details and any logs MUST NOT contain the credential (`LogRedactor` MUST apply to any accidental log path). The endpoint MUST return 404 for an unknown user. Resetting one's own password MUST be allowed (the self role-edit guard does not apply; no lockout risk).

#### Scenario: Reset returns a usable one-time credential

- GIVEN an Admin and an existing user
- WHEN the Admin calls `POST /api/admin/users/{userId}/reset-password`
- THEN the response is 200 with an alphanumeric temporary password of 12–16 characters
- AND the stored hash verifies against the returned credential and the previous password no longer authenticates

#### Scenario: Credential never reaches audit or logs

- GIVEN a successful password reset
- WHEN the audit rows and application logs are inspected
- THEN no row or log line contains the temporary password (audit details omit it; LogRedactor applies)

#### Scenario: Reset for unknown user

- GIVEN an Admin and a userId with no matching account
- WHEN the Admin calls `POST /api/admin/users/{userId}/reset-password`
- THEN the response is 404 and no hash change or audit row is persisted

#### Scenario: Admin resets own password

- GIVEN an Admin whose token identifies them as user U
- WHEN the Admin calls `POST /api/admin/users/U/reset-password`
- THEN the response is 200 with a temporary password (self reset allowed)

#### Scenario: Temp password satisfies password policy

- GIVEN any generated temporary password
- WHEN the generated string is checked against the login password validation
- THEN it passes (length 12–16 satisfies the min-8 rule, allowed charset)

### Requirement: AUM-004: Changes apply on next login (no session revocation)

Role changes and password resets MUST apply on **next login**: the JWT role claim is frozen in the httpOnly cookie for up to 7 days and this change MUST NOT introduce JWT-revocation middleware. A user whose role was changed keeps their previous role's authority until their next login. The "applies on next login" behavior MUST be documented in `AUTHORIZATION_MATRIX.md` (documentation requirement, not middleware).

#### Scenario: Role change does not affect the current session

- GIVEN a logged-in `Staff` user whose role is changed to `SinAcceso`
- WHEN the user calls a staff-allowed endpoint with their existing cookie
- THEN the request succeeds with `Staff` authority until the cookie expires or the user logs in again

#### Scenario: Next login picks up the new role

- GIVEN the same user logging in again after the role change
- WHEN the new session is issued
- THEN the role claim is `SinAcceso` and role-gated endpoints return 403

### Requirement: AUM-005: AdminPanel user management flows

The AdminPanel users table MUST gain an actions column exposing role-edit and reset-password flows using existing primitives (`Modal`, `DropdownMenu`, `Badge`, `Button`). The role-edit modal MUST offer every `UserRole` value; the reset modal MUST display the temporary password **once** with a copy hint and MUST NOT retain it in state after closing. `roleLabel` and the badge variant MUST handle `SinAcceso` (label plus fallback variant). The users-list role filter MUST include `SinAcceso` (needed to find and restore locked-out users). The create-user form select MUST remain limited to `Organizador`/`Staff` — **owner-confirmable assumption: the create-user form does NOT offer `SinAcceso`** (creation implies granting access); users-list filter inclusion IS required.

#### Scenario: Actions column offers role edit and reset

- GIVEN an Admin viewing the users table
- WHEN a user row's actions column renders
- THEN role-edit and reset-password entries are available for that row

#### Scenario: Role-edit modal changes a role

- GIVEN an Admin opens the role-edit modal for a `Staff` user
- WHEN the Admin selects `Organizador` and confirms
- THEN the role endpoint is called, the list reloads, and the row badge shows the new role

#### Scenario: Self role-edit is guarded in the UI

- GIVEN an Admin invoking the role-edit flow on their own row
- WHEN the endpoint returns the 400 self-edit error
- THEN the error is surfaced as user feedback and no role change is applied

#### Scenario: Reset modal shows the credential once with copy hint

- GIVEN an Admin confirms a password reset
- WHEN the response returns the temporary password
- THEN the modal displays it with a copy affordance, and after closing it is no longer retrievable from the UI

#### Scenario: SinAcceso appears in filter and labels but not in the create form

- GIVEN the AdminPanel rendered with a `SinAcceso` user present
- WHEN the role filter, role labels, and create-user form render
- THEN the filter includes `SinAcceso`, labels/badges render it correctly, and the create-user role select still offers only `Organizador`/`Staff`

### Requirement: AUM-006: Documentation sync

`backend/AUTHORIZATION_MATRIX.md` MUST be rewritten to match implementation: the stale AdminController section (currently claiming `[Authorize(Roles="Admin")]` and listing 3 of 10 endpoints) MUST be corrected to the policy-based controller with all endpoints including the two new ones; the Role Capabilities Matrix MUST gain a `SinAcceso` column plus "Edit user role" and "Reset password" rows; and the next-login note (AUM-004) MUST be included. `README.md` role lists and the admin endpoint table MUST include `SinAcceso` and both new endpoints.

#### Scenario: Authorization matrix matches implementation

- GIVEN the updated `AUTHORIZATION_MATRIX.md`
- WHEN the AdminController section, matrix columns, and policies are reviewed
- THEN the controller section lists all admin endpoints with the correct `RequireAdminRole` policy, a `SinAcceso` column exists granting nothing, and the next-login note is present

#### Scenario: README role and endpoint claims are current

- GIVEN the updated `README.md`
- WHEN the role list and admin endpoint table are reviewed
- THEN `SinAcceso` appears in the role lists and both new endpoints are documented

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| AUM-001 | admin-changes-role, self-role-edit-400, role-edit-unknown-user-404 |
| AUM-002 | sinacceso-403-all-gated, sinacceso-login-succeeds, sinacceso-redirect-home, role-enum-append-only |
| AUM-003 | reset-returns-usable-credential, credential-absent-audit-logs, reset-unknown-user-404, reset-self-allowed, temp-password-passes-policy |
| AUM-004 | role-change-no-session-revocation, next-login-picks-up-role |
| AUM-005 | actions-column-offers-flows, role-edit-modal-works, self-role-edit-ui-guard, reset-modal-credential-once, sinacceso-filter-labels-not-create |
| AUM-006 | auth-matrix-matches-implementation, readme-sync-current |
