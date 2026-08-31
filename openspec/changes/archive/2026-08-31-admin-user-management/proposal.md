# Proposal: Admin User Management

## Intent

Admins can create users but cannot manage existing accounts. Access cannot be revoked without deleting the account row — which `AuditLogs` FK-restrictions depend on — and there is no recovery path when a user loses a password. This change adds role editing (including a new `SinAcceso` revoke role) and admin-triggered manual password reset.

## Scope

### In Scope
- Append `UserRole.SinAcceso` at enum index 3 (append-only; stored as `int`, no migration).
- `PUT /api/admin/users/{userId}/role` — 400 when `targetUserId == caller adminId` (self-edit guard); 404 unknown user; audit `UpdateUserRole`.
- `POST /api/admin/users/{userId}/reset-password` — generated temp password returned ONCE in response body; audit `ResetPassword` WITHOUT the credential.
- New `AuditActionType` values (stored `HasConversion<string>` varchar(100), no migration).
- AdminPanel users table: actions column with role-edit and reset-password flows (existing `Modal`/`DropdownMenu`/`Badge` primitives).
- Docs sync: `backend/AUTHORIZATION_MATRIX.md` (incl. stale AdminController section + "role applies on next login" note), `README.md` role/endpoint claims.
- Tests: xUnit (unit, EF InMemory, FsCheck password properties), vitest AdminPanel.

### Out of Scope (Non-goals)
- Account deletion (rows never deleted).
- Self-service password change.
- JWT revocation middleware (role claim frozen in httpOnly cookie up to 7 days; applies on next login — documented only).
- Email delivery of temp password (out-of-band handoff).
- Forced password-change flow.

## Capabilities

### New Capabilities
- `admin-user-management`: admin role editing with `SinAcceso` semantics, self-edit guard, manual password reset with response-once credential contract, audit coverage, and admin UI flows.

### Modified Capabilities
- None. (Existing specs — e.g. `role-access` — keep their requirements; `SinAcceso` grants nothing, so no policy behavior changes.)

## Approach

**Backend** (aspnet-api-design + backend-security): append-only enum — insertion would corrupt existing int rows; no policy references `SinAcceso` → 403 on all role-gated endpoints, login still works. Role edit in `IAdminService`; reset in `IAuthService` (password domain, owns BCrypt). Temp password via `RandomNumberGenerator` (12–16 alnum, satisfies min-8), BCrypt-hashed, never logged (`LogRedactor`) nor audited. CSRF via existing `X-CSRF-PROTECT`; no new rate limiter (admin endpoints trusted, repo precedent).

**Frontend** (react-patterns): fourth actions column on the users table; role-edit modal with role select; reset modal showing the temp password once with copy hint; `roleLabel`/badge variant for `SinAcceso`; login redirect already falls to `'/'`.

**Docs**: rewrite stale AdminController matrix section; add SinAcceso column/rows and next-login-role note; sync README role lists and endpoint table.

## Affected Areas

| Area | Impact | ~Lines |
|------|--------|--------|
| `backend/Models/UserRole.cs`, `AuditLog.cs` | Modified | ~10 |
| `backend/Controllers/AdminController.cs` + DTOs | Modified | ~120 |
| `backend/Services/IAdminService/AdminService`, `IAuthService/AuthService` | Modified | ~80 |
| Backend tests (unit/InMemory/FsCheck/WAF) | New | ~600–800 |
| `frontend/src/pages/AdminPanel.jsx` | Modified | ~250 |
| `frontend/src/pages/__tests__/AdminPanel.test.jsx` | Modified | ~400 |
| `backend/AUTHORIZATION_MATRIX.md`, `README.md` | Modified | ~60 |

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Temp-password leakage (logs/audit/response replay) | Medium | Response-once contract; no credential in `Details`; `LogRedactor`; tests assert absence in audit rows and logs |
| Enum append discipline (insertion corrupts int-stored roles) | High if violated | Append at index 3 only; review guard; documented in spec |
| Stale AUTHORIZATION_MATRIX AdminController section | Medium | Docs sync rewrites the section in this change |
| AdminPanel.jsx size (753 lines + flows) | Medium | Reuse existing primitives; extract modal components if tasks phase warrants |

## Rollback Plan

Single-PR branch revert; zero schema changes (enum append leaves stored ints untouched), so revert is code+docs only with no data migration.

## Dependencies

None new (BCrypt, existing primitives, existing test stack).

## Success Criteria

- [ ] Admin changes any non-self user's role; `SinAcceso` user receives 403 on role-gated endpoints and lands on `'/'` after login.
- [ ] Admin resets a password; temp password authenticates; credential absent from logs and audit rows.
- [ ] Self role-edit returns 400; unknown user returns 404.
- [ ] Docs match implementation; backend and frontend suites green.

## Open Assumptions (owner-confirmable)

1. **Create-user form does NOT offer `SinAcceso`** (creation implies granting access; current select already limits to Organizador/Staff). **Users-list role filter DOES include `SinAcceso`** (needed to find and restore locked-out users). Minimal + coherent default; flag for owner confirmation in specs.
