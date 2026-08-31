# Tasks: Admin User Management

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1780 (range 1500–2000): backend prod ~290, backend tests ~740, frontend ~680, docs ~70 |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Single PR on `feat/admin-user-management` — work-unit commits keep review slices bounded |
| Delivery strategy | single-pr (preflight review budget: 3000 lines) |
| Chain strategy | pending (N/A — single-pr) |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: High

**Budget verdict**: forecast ~1780 ≤ 3000-line preflight budget → **`size:exception` NOT needed**. The generic 400-line budget is exceeded (High), mitigated by 7 work-unit commits (each ≤ ~650 lines, tests with code).

### Suggested Work Units

| Unit | Goal | Commit | Focused test command | Runtime harness | Rollback boundary |
|------|------|--------|----------------------|-----------------|-------------------|
| 1 | `SinAcceso` enum append + audit enum values (AUM-002, D1, D10) | `feat(api): agregar rol SinAcceso y acciones de auditoria` | `dotnet test --filter AuthenticationPropertyTests` | N/A — enum append-only proven by stability test + int-stored snapshot | Revert UserRole.cs, AuditLog.cs, enum test |
| 2 | Role-edit endpoint RED→GREEN + WAF role cases (AUM-001, D2–D7, D10) | `feat(api): endpoint de edicion de rol con auditoria` | `dotnet test --filter "AdminControllerTests\|AdminServiceTests\|AdminUserManagementIntegrationTests"` | WAF suite boots factory; manual: PUT role w/ admin cookie → 200 + audit row | Revert IAdminService/AdminService, PUT endpoint, role test regions |
| 3 | PasswordGenerator + reset endpoint RED→GREEN + WAF reset/SinAcceso/session cases (AUM-002/003/004, D8, D9, D11) | `feat(api): reseteo de contrasena admin con credencial unica` | `dotnet test --filter "AuthenticationPropertyTests\|AdminControllerTests\|AdminUserManagementIntegrationTests"` | WAF: login-with-temp succeeds, old password fails; audit rows credential-free | Revert PasswordGenerator.cs, AuthService reset, POST endpoint, reset test regions |
| 4 | Frontend actions column + modals (AUM-005, D12–D16) | `feat(frontend): acciones de usuario con modales de rol y reseteo` | `npx vitest run src/components/__tests__/RoleEditModal.test.jsx src/components/__tests__/ResetPasswordModal.test.jsx src/pages/AdminPanel.test.jsx` | Manual: role edit reloads badge; reset shows credential once | Revert 3 component files + AdminPanel + tests |
| 5 | `SinAcceso` redirect pinned (AUM-002) | `test(frontend): redirect post-login de SinAcceso a home` | `npx vitest run src/pages/Login.test.jsx` | N/A — behavior already correct (Login.jsx getRedirectPath default) | Revert Login.test.jsx case |
| 6 | Docs sync (AUM-006) | `docs: sincronizar matriz de autorizacion y README` | N/A — manual review checklist (task 6.3) | N/A — docs only | Revert 2 doc files |

## Phase 1: Backend RED Tests (strict TDD — write failing first)

- [x] 1.1 RED enum+generator: `backend/Tests/AuthenticationPropertyTests.cs` — enum-stability test asserting `UserRole` values 0–3 deserialize to `Organizador`/`Staff`/`Admin`/`SinAcceso` (AUM-002 `role-enum-append-only`, D1); FsCheck properties for `PasswordGenerator.Generate()`: length ∈ [12,16], charset ⊆ alphanumeric, output passes login min-8 validation (AUM-003 `temp-password-passes-policy`, D9).
- [x] 1.2 RED role-edit unit: `backend/Tests/AdminControllerTests.cs` new `#region` (Moq `IAdminService`, `SetAuthenticatedUser`) — 200 + audit verify `UpdateUserRole`/`AuditResourceType.User`/target id; self-edit 400 with service + audit `Times.Never` ×2; unknown 404, no audit; 500 catch-all (AUM-001 all 3 scenarios, D3–D6, D10).
- [x] 1.3 RED role-edit service: `backend/Tests/AdminServiceTests.cs` (EF InMemory `UseInMemoryDatabase(Guid.NewGuid()...)`) — persists role change + returns `UserSummary`; unknown id throws `KeyNotFoundException` (AUM-001, D7).
- [x] 1.4 RED reset unit: `backend/Tests/AdminControllerTests.cs` — 200 body contains temp password; audit Details asserts it does **NOT** contain the credential; unknown 404 no audit; self-reset 200 (guard is role-only); 500 (AUM-003 scenarios, D4, D10, D11).
- [x] 1.5 RED reset service: `backend/Tests/AuthenticationPropertyTests.cs` — unknown → failure "User not found" (string pinned per D6); stored hash verifies via `BCrypt.Verify(temp)`; previous password no longer verifies (AUM-003, D8).
- [x] 1.6 RED WAF integration: create `backend/Tests/AdminUserManagementIntegrationTests.cs` (`AdminUserCreationApiFactory` pattern, `[Collection("EnvConfigTests")]`, `X-CSRF-PROTECT: 1`, `HasLiveDatabase()` guard) — role PUT 200/400/404 + persisted role; reset POST 200/404 + login-with-temp succeeds + old fails; audit rows credential-free; `SinAcceso` login 200 + role-gated endpoint 403 after re-login; old cookie still `Staff`-authorized (no revocation); missing CSRF header rejected (AUM-001/002/003/004, D2, D11).

## Phase 2: Backend GREEN (minimal implementation)

- [x] 2.1 `backend/Models/UserRole.cs`: append `SinAcceso` at index 3 + append-only XML-doc guard comment (int-stored, never insert/reorder) (AUM-002, D1).
- [x] 2.2 `backend/Models/AuditLog.cs`: append `UpdateUserRole`, `ResetPassword` to `AuditActionType` (string-converted varchar(100), no migration) (D10).
- [ ] 2.3 Create `backend/Helpers/PasswordGenerator.cs`: static `Generate()` using `RandomNumberGenerator.GetInt32(12,17)` + `GetString(alnum, length)` (AUM-003, D9).
- [x] 2.4 `backend/Services/IAdminService.cs` + `AdminService.cs`: `UpdateUserRoleAsync(Guid, UserRole)` — tracked `FindAsync` → `KeyNotFoundException` → set Role → `SaveChangesAsync` → `UserSummary` (AUM-001, D7).
- [x] 2.5 `backend/Services/IAuthService.cs` + `AuthService.cs`: `ResetPasswordAsync(Guid)` → `ResetPasswordResult { Success, Error, TemporaryPassword, UserId }`; `FindAsync` → null → failure; generate → `BCrypt.HashPassword` → persist hash only (AUM-003, D8).
- [x] 2.6 `backend/Controllers/AdminController.cs`: `PUT users/{userId:guid}/role` + `POST users/{userId:guid}/reset-password` (policy inherited, D2); records `AdminUpdateUserRoleRequest(UserRole)` + `AdminResetPasswordResponse` (D5); controller-level self-edit guard pre-service (D4); error mapping — self 400 `new { error }`, role 404 via `KeyNotFoundException`, reset 404 via "User not found" string, 500 catch-all (D6); `TryLogAuditAsync` with `Truncate(1000)` details, no credential, no email (D10); `Cache-Control: no-store` on reset 200 (D11).
- [x] 2.7 `dotnet test` from `backend/` — all Phase 1 tests green, zero regressions.

## Phase 3: Frontend (TDD-ready — vitest tests with code)

- [ ] 3.1 Create `frontend/src/components/RoleEditModal.jsx` + `__tests__/RoleEditModal.test.jsx` — offers all 4 `UserRole` values; confirm calls PUT and fires success (list reload via parent `loadData`, D16); mocked PUT 400 surfaces error feedback, modal stays open, no change applied (AUM-005 `role-edit-modal`, `self-edit-ui-guard`; D13).
- [ ] 3.2 Create `frontend/src/components/ResetPasswordModal.jsx` + `__tests__/ResetPasswordModal.test.jsx` — confirm → result shows temp password **once** + "Copiar" (`navigator.clipboard.writeText`) + warning "no se volverá a mostrar"; `onClose` clears credential state; assert not retrievable after close (AUM-005 `reset-modal-credential-once`; D14).
- [ ] 3.3 Modify `frontend/src/pages/AdminPanel.jsx` + `AdminPanel.test.jsx` — fourth actions column with per-row `DropdownMenu` ("Editar rol", "Restablecer contraseña") (AUM-005 `actions-column`; D12); `roleLabel` `SinAcceso: 'Sin acceso'` + `roleBadgeVariant` `'warning'`; filter select gains `SinAcceso` option; create-user select **unchanged** (Organizador/Staff only) (AUM-005 `sinacceso-filter-labels-not-create`; D15); wire both modals + reload.
- [ ] 3.4 Modify `frontend/src/pages/Login.test.jsx` — `SinAcceso` post-login redirect lands on `'/'` (AUM-002 `sinacceso-redirect-home`).
- [ ] 3.5 `npx vitest run` from `frontend/` — all suites green, no new failures.

## Phase 4: Docs Sync + Final Verification

- [ ] 4.1 Rewrite `backend/AUTHORIZATION_MATRIX.md` (AUM-006, D1, D11): correct stale AdminController section → `RequireAdminRole` policy, all 12 endpoints incl. the 2 new; Role Capabilities Matrix gains `SinAcceso` column (grants nothing) + "Edit user role" / "Reset password" rows; add next-login note (JWT role claim frozen, cookie ≤7d, no revocation middleware).
- [ ] 4.2 Update `README.md` (AUM-006): role lists include `SinAcceso`; admin endpoint table gains both new endpoints.
- [ ] 4.3 Final verification: `dotnet test` from `backend/` + `npx vitest run` from `frontend/` both green; docs walkthrough against AUM-006's 2 scenarios; work-unit commits executed per table above (conventional commits, Spanish subjects, tests with code).

## Commit Plan (work units → conventional commits, Spanish subjects)

| # | Commit | Tasks | AUM |
|---|--------|-------|-----|
| 1 | `feat(api): agregar rol SinAcceso y acciones de auditoria` | 1.1(enum), 2.1, 2.2 | AUM-002 |
| 2 | `feat(api): endpoint de edicion de rol con auditoria` | 1.2, 1.3, 1.6(role cases), 2.4, 2.6(PUT) | AUM-001, AUM-004 |
| 3 | `feat(api): reseteo de contrasena admin con credencial unica` | 1.1(generator), 1.4, 1.5, 1.6(reset/SinAcceso/session cases), 2.3, 2.5, 2.6(POST) | AUM-002, AUM-003, AUM-004 |
| 4 | `feat(frontend): acciones de usuario con modales de rol y reseteo` | 3.1, 3.2, 3.3 | AUM-005 |
| 5 | `test(frontend): redirect post-login de SinAcceso a home` | 3.4 | AUM-002 |
| 6 | `docs: sincronizar matriz de autorizacion y README` | 4.1, 4.2 | AUM-006 |
