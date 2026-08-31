# Design: Admin User Management

## Technical Approach

Two admin endpoints on the existing `AdminController` (class-level `RequireAdminRole` inherited), backed by a service split along domain lines: role edit → `IAdminService` (user-management domain), password reset → `IAuthService` (password domain, owns BCrypt). Zero schema changes: `UserRole.SinAcceso` appends to an int-stored enum; two `AuditActionType` values append to a string-converted enum. Frontend adds a fourth actions column to the AdminPanel users table with two extracted modal components. All 21 spec scenarios map to concrete decisions/tests in the mapping table below (regression guard; note: orchestrator brief said 20 — the spec contains 21 `#### Scenario` blocks, all covered).

## Architecture Decisions

| # | Decision | Choice | Alternatives rejected | Rationale |
|---|----------|--------|----------------------|-----------|
| 1 | Enum append | Append `SinAcceso` at index 3 in `backend/Models/UserRole.cs` with an XML-doc guard comment: append-only, int-stored, never insert/reorder | Explicit values on all members; mid-enum insert | `User.Role` is int-stored with no `HasConversion` (ApplicationDbContext.cs:42; snapshot `Property<int>`), so appending keeps 0/1/2 stable — **no migration**. Explicit values don't prevent silent renumbering (C# allows duplicate implicit values), so the guard is the comment + spec scenario `role-enum-append-only` + review |
| 2 | Endpoint placement | `PUT /api/admin/users/{userId:guid}/role` and `POST /api/admin/users/{userId:guid}/reset-password` in `AdminController`; policy inherited from class attribute | New controller; `[Authorize]` per-method | House rule: role-gated endpoints reuse `RequireAdminRole`; both services already injected in the 6-arg ctor — **zero ctor/DI changes** |
| 3 | Caller identity | `TryGetUserId(out var adminId)` from `TicketeraControllerBase` (JWT `NameIdentifier` claim); 401 when absent | Raw claims access | House hard rule (aspnet-api-design) |
| 4 | Self-edit guard | Controller-level `if (userId == adminId) return BadRequest(...)` **before** the service call (role edit only; self reset allowed) | Service-level guard | Controller owns caller identity; pre-service check guarantees spec's "no role change or audit row persisted" with no service coupling |
| 5 | DTOs | `public record AdminUpdateUserRoleRequest(UserRole Role);` and `AdminResetPasswordResponse { string TemporaryPassword }` at the AdminController file bottom (house record precedent: `RejectEventRequest`, `RefundPurchaseRequest`); role-edit 200 returns `UserSummary` | `AdminUserResponse` for role edit; data annotations | No annotations — `[ApiController]` auto-400 on missing body and on invalid enum string (JsonStringEnumConverter binding failure). `UserSummary` is the canonical users-table row type; `AdminUserResponse` stays the create-201 contract |
| 6 | Error mapping | 400 self-edit (`new { error = ... }`); 404 unknown user — role edit: catch `KeyNotFoundException` (AdminService pattern); reset: `result.Error` contains "User not found" → 404 (mirrors CreateUser's "already exists" → 409 string-mapping precedent); 500 catch-all; `ProblemDetails`/`Problem()` reserved for the 409 event-finalized pattern (N/A here) | Typed error enum | Follows the controller's existing uniform 200/400/404/500 mapping |
| 7 | Role-edit service | `Task<UserSummary> UpdateUserRoleAsync(Guid targetUserId, UserRole newRole)` in `IAdminService`/`AdminService`; tracked `FindAsync` → throw `KeyNotFoundException` → set `Role` → `SaveChangesAsync` → return summary | Put in AuthService | User-management domain lives in AdminService (owns `GetAllUsersAsync`/`UserSummary`). No explicit transaction: single-row, single-save — matches `ApproveEventAsync` |
| 8 | Reset service | `Task<ResetPasswordResult> ResetPasswordAsync(Guid targetUserId)` in `IAuthService`/`AuthService`; result object `{ Success, Error, TemporaryPassword, UserId }` (AuthService convention: `CreateUserResult`/`AuthResult`) | Throw-based; put in AdminService | AuthService owns password domain + BCrypt + min-8 policy. Flow: `FindAsync` → null → failure "User not found" → `PasswordGenerator.Generate()` → `BCrypt.HashPassword` → persist hash only → `SaveChangesAsync` → return cleartext once |
| 9 | Temp password generator | Static `PasswordGenerator` in `backend/Helpers/` (new file): `RandomNumberGenerator.GetInt32(12, 17)` length + `RandomNumberGenerator.GetString(alnumChars, length)` | Injectable `IPasswordGenerator` | House precedent: `HmacHelper`/`LogRedactor` are static helpers; BCrypt used statically. FsCheck property tests assert output properties directly — DI indirection adds ctor/registration/Moq surface with zero test benefit |
| 10 | Audit | In-controller via existing `TryLogAuditAsync` after service success (CreateUser pattern). Append `UpdateUserRole`, `ResetPassword` to `AuditActionType` — string-converted varchar(100), **no migration** (in-repo precedent comments ATS-005/APR-007/EA-003). `AuditResourceType.User`, `ResourceId = targetUserId`. Details via `Truncate(..., 1000)`: `"Admin updated role for user {id} to {role}"` / `"Admin reset password for user {id}"` | Service-level audit; email in Details | Best-effort audit is the controller pattern. PII minimization: ids + roles only, **no email** (unlike CreateUser), **NEVER the credential** |
| 11 | Security posture | Credential exists in exactly one place: the reset 200 body (`Cache-Control: no-store` added as defense-in-depth). No log statement receives it; audit Details exclude it; `LogRedactor` denylist key "password" catches key=value-shaped accidental leaks. Honest note: a bare 12–16 alnum string in free-form logs would NOT match `RedactLongSecretLikeStrings` (needs ≥33 chars) — the primary guarantee is structural (no logger call sees it), proven by absence-asserting tests. No new rate limiter: named limiters cover buyer-facing abuse (Login/Resend/Reservations); admin endpoints trusted (fix-mp-webhook-400 design precedent). CSRF: both mutating verbs under `/api/admin` → `X-CSRF-PROTECT` required (not exempt); api client auto-adds | Rate limiter on reset; logging the credential anywhere | Matches backend-security decision gates |
| 12 | Frontend actions | Fourth table column with per-row `DropdownMenu` (items: "Editar rol", "Restablecer contraseña") | Inline buttons | Events section already uses the kebab pattern; two actions per row would bloat the table |
| 13 | Modal placement | Extract `frontend/src/components/RoleEditModal.jsx` + `ResetPasswordModal.jsx` built on the `Modal` primitive | Inline in AdminPanel.jsx | `AddTicketsModal`/`DeleteConfirmationDialog` precedent: feature modals live in `components/`. AdminPanel.jsx is 753 lines; inline flows would push ~950+. Page keeps users state + `loadData` |
| 14 | Reset modal UX | Two-step: confirm → result shows temp password once + "Copiar" (`navigator.clipboard.writeText`) + warning "no se volverá a mostrar"; `onClose` clears credential state; parent unmounts via `{resetTarget && ...}` | Persist to localStorage; keep retrievable | AUM-005 scenario: not retrievable after close. React state only — never storage |
| 15 | Role labels/filter | `roleLabel` += `SinAcceso: 'Sin acceso'`; `roleBadgeVariant` += `case 'SinAcceso': return 'warning'`; filter select += `<option value="SinAcceso">`; **create-user select unchanged** (Organizador/Staff only) | 'error' variant; SinAcceso in create form | 'warning' signals locked-out state, distinct from Admin's 'error'. Create-form exclusion is the spec MUST (owner-confirmable assumption #1) |
| 16 | Query invalidation | None: users list is manual-fetch — mutations re-run `loadData(controller)` (handleApprove/handleCreateUser precedent). No react-query key exists for users; role changes don't touch cached event queries; `queryKeys.js` unchanged | Add `adminUsers` key; invalidate `['events']` | Honest match to current architecture; the page is not react-query for users |

## Data Flow

Role edit:

    Admin JWT cookie ─▶ CsrfHeaderMiddleware (X-CSRF-PROTECT)
        ─▶ RequireAdminRole policy ─▶ PUT /api/admin/users/{userId}/role
        ─▶ TryGetUserId → adminId ─▶ (userId == adminId? → 400, stop)
        ─▶ AdminService.UpdateUserRoleAsync
              FindAsync (tracked) → 404 path: KeyNotFoundException
              Role = newRole → SaveChangesAsync → UserSummary
        ─▶ TryLogAuditAsync(UpdateUserRole, User, targetUserId, details≤1000)
        ─▶ 200 UserSummary ─▶ AdminPanel: feedback + loadData() re-run

Password reset:

    Admin JWT cookie ─▶ CsrfHeaderMiddleware ─▶ RequireAdminRole
        ─▶ POST /api/admin/users/{userId}/reset-password (self allowed)
        ─▶ AuthService.ResetPasswordAsync
              FindAsync → null → failure "User not found" → 404 path
              PasswordGenerator.Generate() (12–16 alnum, CSPRNG)
              BCrypt.HashPassword → persist hash ONLY → SaveChangesAsync
        ─▶ TryLogAuditAsync(ResetPassword, User, targetUserId, NO credential)
        ─▶ 200 { temporaryPassword } (Cache-Control: no-store) — cleartext's only appearance
        ─▶ ResetPasswordModal: display once + copy ─▶ cleared on close

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `backend/Models/UserRole.cs` | Modify | Append `SinAcceso` at index 3 + append-only guard comment |
| `backend/Models/AuditLog.cs` | Modify | Append `UpdateUserRole`, `ResetPassword` to `AuditActionType` (varchar(100), no migration) |
| `backend/Controllers/AdminController.cs` | Modify | Two endpoints + `AdminUpdateUserRoleRequest`/`AdminResetPasswordResponse` records |
| `backend/Services/IAdminService.cs` | Modify | `UpdateUserRoleAsync` + XML docs |
| `backend/Services/AdminService.cs` | Modify | Tracked update implementation |
| `backend/Services/IAuthService.cs` | Modify | `ResetPasswordAsync` + `ResetPasswordResult` |
| `backend/Services/AuthService.cs` | Modify | Reset implementation (generator + BCrypt) |
| `backend/Helpers/PasswordGenerator.cs` | Create | Static CSPRNG generator (12–16 alnum) |
| `backend/Tests/AdminControllerTests.cs` | Modify | Two new #regions (RED first) |
| `backend/Tests/AdminServiceTests.cs` | Modify | `UpdateUserRoleAsync` InMemory tests |
| `backend/Tests/AuthenticationPropertyTests.cs` | Modify | Reset region + FsCheck generator properties |
| `backend/Tests/AdminUserManagementIntegrationTests.cs` | Create | WAF integration (factory + CSRF header) |
| `frontend/src/components/RoleEditModal.jsx` | Create | Role select (all 4 roles) + PUT + feedback |
| `frontend/src/components/ResetPasswordModal.jsx` | Create | Confirm → one-time credential + copy → cleared on close |
| `frontend/src/pages/AdminPanel.jsx` | Modify | Actions column, `roleLabel`/`roleBadgeVariant`, filter option, modal wiring |
| `frontend/src/pages/AdminPanel.test.jsx` | Modify | AUM-005 page-level cases |
| `frontend/src/components/__tests__/RoleEditModal.test.jsx`, `.../ResetPasswordModal.test.jsx` | Create | Component contracts |
| `frontend/src/pages/Login.test.jsx` | Modify | `SinAcceso` → `'/'` redirect case |
| `backend/AUTHORIZATION_MATRIX.md`, `README.md` | Modify | AUM-006 docs sync |

## Interfaces / Contracts

```csharp
// IAdminService
Task<UserSummary> UpdateUserRoleAsync(Guid targetUserId, UserRole newRole); // KeyNotFoundException → 404

// IAuthService
Task<ResetPasswordResult> ResetPasswordAsync(Guid targetUserId);
public class ResetPasswordResult { public bool Success; public string Error; public string TemporaryPassword; public Guid UserId; }

// Helpers/PasswordGenerator.cs — static, CSPRNG, alphanumeric 12–16
public static string Generate() // GetInt32(12,17) + GetString(alnum, length)
```

## Testing Strategy (strict TDD — backend RED first)

| Layer | File | Cases |
|-------|------|-------|
| Unit (Moq, `SetAuthenticatedUser`) | `AdminControllerTests.cs` | Role: 200+audit verify, self 400 (service+audit `Times.Never`), unknown 404, 500. Reset: 200 + audit Details asserts **excludes** temp password, unknown 404, self 200, 500. Ctor unchanged (6 args) |
| Service EF InMemory | `AdminServiceTests.cs` | Persists + returns updated summary; unknown throws |
| Service EF InMemory | `AuthenticationPropertyTests.cs` | Reset: unknown → failure; temp verifies via `BCrypt.Verify`, old password stops verifying |
| FsCheck properties | `AuthenticationPropertyTests.cs` | Length ∈ [12,16]; charset ⊆ alnum; satisfies min-8 policy |
| WAF integration | `AdminUserManagementIntegrationTests.cs` (AdminUserCreationApiFactory pattern, `[Collection("EnvConfigTests")]`, `X-CSRF-PROTECT: 1`) | Both endpoints 200/400/404; persisted role; login with temp password succeeds + old fails; audit rows credential-free; SinAcceso next-login 403 + old-cookie still Staff (AUM-004); missing CSRF header rejected; `HasLiveDatabase()` guard where needed |
| Frontend vitest | `AdminPanel.test.jsx` + modal tests + `Login.test.jsx` | AUM-005 scenarios (below) + redirect home |
| Docs (AUM-006) | Manual verify checklist in tasks | No automated docs tests exist in repo; verify phase reviews both files |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary (application-level CRUD change).

## Scenario → Decision Mapping (regression guard, all 21)

| Scenario | Decision(s) | Test target |
|----------|-------------|-------------|
| AUM-001 admin-changes-role | D2,D5,D7,D10 | Controller 200+audit; InMemory persist; WAF 200 |
| AUM-001 self-role-edit-400 | D4 (pre-service guard) | Controller 400, `Times.Never`×2 |
| AUM-001 unknown-user-404 | D6 KeyNotFoundException | Controller 404, no audit |
| AUM-002 sinacceso-403-all-gated | D1 (no policy grants it) | WAF: role-gated endpoint 403 after next login |
| AUM-002 sinacceso-login-succeeds | Login has no role check; JWT claim `SinAcceso` | WAF login 200 + cookie role |
| AUM-002 sinacceso-redirect-home | `getRedirectPath` default `'/'` (Login.jsx:10-15) | Login.test.jsx case |
| AUM-002 role-enum-append-only | D1 guard comment + int storage | FsCheck/unit: values 0–3 stable; review guard |
| AUM-003 usable-one-time-credential | D8,D9 | Service verify + WAF login-with-temp |
| AUM-003 credential-absent-audit-logs | D10,D11 | Controller audit Details assert; WAF audit row |
| AUM-003 reset-unknown-404 | D6 result mapping | Controller + WAF 404, no audit |
| AUM-003 reset-self-allowed | D4 (guard is role-only) | Controller self 200 |
| AUM-003 temp-passes-policy | D9 12–16 ≥ min-8 | FsCheck properties |
| AUM-004 no-session-revocation | JWT frozen 7d cookie; no middleware | WAF: old cookie still Staff-authorized |
| AUM-004 next-login-new-role | Next login reads DB role | WAF: re-login → 403 |
| AUM-005 actions-column | D12 | AdminPanel.test.jsx row entries |
| AUM-005 role-edit-modal | D13,D16 | Modal test: PUT called, list reloads, badge |
| AUM-005 self-edit-ui-guard | Server 400 surfaced; modal stays open | Modal test: mock PUT 400 → alert |
| AUM-005 reset-modal-once | D14 | Modal test: shown once, cleared on close |
| AUM-005 filter-labels-not-create | D15 | Page test: option+badge present, create select unchanged |
| AUM-006 matrix-matches | Docs rewrite: all 12 endpoints, SinAcceso column, next-login note | Manual verify |
| AUM-006 readme-current | Roles + endpoint table +SinAcceso/+2 endpoints | Manual verify |

## Migration / Rollout

No migration (int-stored enum append; string-converted audit enum append). Single PR, no feature flags; rollback = code+docs revert, zero data impact.

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Enum insertion corrupts int-stored roles | High if violated | Guard comment + spec scenario + FsCheck value-stability test + review |
| Temp-password leakage | Medium | Structural no-log path; audit exclusion; absence-asserting tests; no-store; one-shot response |
| Stale AUTHORIZATION_MATRIX section | Medium | Full rewrite in docs task (AUM-006) |
| AdminPanel size | Medium | Modal extraction (D13) keeps page growth bounded |
| String-mapped 404 ("User not found") brittleness | Low | Test-pinned error string (CreateUser precedent) |
| WAF live-DB dependency | Low | `HasLiveDatabase()` guard per house pattern |

## Open Questions

None blocking. Owner-confirmable assumption #1 (create form excludes `SinAcceso`) is encoded as a spec MUST; revisit only if the owner objects during review.
