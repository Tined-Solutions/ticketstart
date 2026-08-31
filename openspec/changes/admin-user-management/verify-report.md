```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:398cdb63699e42c897238ada1cb9d71f022101f6e93992710fe51fcca1b72001
verdict: fail
blockers: 1
critical_findings: 1
requirements: 5/6
scenarios: 20/21
test_command: dotnet test (cwd backend/)
test_exit_code: 1
test_output_hash: sha256:63254f56d1f409c52188ee39cad658eeedf11927815c929ea72ca9ea99931c99
build_command: dotnet build (cwd backend/) exit 0; vite build (cwd frontend/) exit 0
build_exit_code: 0
build_output_hash: sha256:f48bd1d42440f0990d4cb090ba0741f29c51d7ea987b3a19157c44dc580f5171
```

Evidence-revision preimage: `cat dotnet-test.log vitest-run.log | sha256sum` → `398cdb63699e42c897238ada1cb9d71f022101f6e93992710fe51fcca1b72001` (both logs are the exact outputs of this verification run; hashes below).

# Verify Report: admin-user-management

- **Change**: admin-user-management (AUM-001…AUM-006, 21 scenarios)
- **Branch**: feat/admin-user-management @ 1e91f93 (base 287f298)
- **Date**: 2026-08-31
- **Verdict**: **FAIL** — 1 CRITICAL, 2 WARNING, 2 SUGGESTION
- **Spec counts (authoritative)**: 6 requirements, 21 scenarios — scenario coverage 21/21 mapped, 20/21 fully satisfied, 1 violated (S4 → C1)

## Strict-TDD Compliance

- **Mode**: Strict TDD active (per orchestrator + skills/dotnet-testing/SKILL.md). All AUM work units followed RED→GREEN: Phase 1 RED tests (tasks 1.1–1.6) precede Phase 2 GREEN implementations (2.1–2.6); frontend suites written with components (3.1–3.4).
- **Test layer distribution**: pure-helper unit (PasswordGenerator policy property, enum stability) / controller unit with Moq (`AdminControllerTests` AUM regions) / service unit with EF InMemory (`AdminServiceTests`) / HTTP pipeline via `WebApplicationFactory<Program>` (`AdminUserManagementIntegrationTests`, `[Collection("EnvConfigTests")]`, `X-CSRF-PROTECT`, `HasLiveDatabase()` guard) / frontend vitest + Testing Library (jsdom, `maxWorkers: 1`). No live Supabase dependencies beyond the guarded WAF suite; no test hits production auth paths.
- **Changed-file coverage**: every production file touched by this change has a directly covering test — UserRole.cs (enum stability ×2), AuditLog.cs (audit assertions in controller + WAF tests), PasswordGenerator.cs (property + determinism), IAdminService/AdminService.cs (service tests + WAF), IAuthService/AuthService.cs (reset service tests + WAF), AdminController.cs (4 role + 4 reset unit tests + 8 WAF tests), RoleEditModal.jsx (4 tests), ResetPasswordModal.jsx (4 tests), AdminPanel.jsx (4 AUM-005 tests), Login.jsx (redirect case).
- **Quality metrics**: backend 716 tests (+33 new vs base), frontend 472 (+15 new); 0 new failures across two consecutive runs; builds green on both stacks.

## Test Evidence

| Suite | Command (cwd) | Exit | Result | test_output_hash (sha256) |
|-------|---------------|------|--------|---------------------------|
| Backend | `dotnet test` (backend/) | 1 | **711 passed / 716**, 0 skipped; 5 failed — all 5 pre-existing baseline (287f298): webhook signature ×2 (PaymentPropertyTests.Property17, PaymentControllerTests.Webhook_InvalidSignature), email retry (PendingEmailRetryTests.Exhaustion), image upload (EventImageUploadTests.S3ClientParams), stale CSRF (AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook) | `63254f56d1f409c52188ee39cad658eeedf11927815c929ea72ca9ea99931c99` |
| Frontend | `npx vitest run` (frontend/) | 1 | **469 passed / 472**; 3 failed — all 3 pre-existing baseline: Checkout ×2 (Editar datos value, PATCH on save), DNI validation (identityValidation.test.js:112) | `8c866f4cd16e9ee69a86efb9be17ac0c656193fe81b59fb45e69ae3f98bb6312` |

Exit 1 on both test suites is caused **only** by the pre-existing baseline failures. **Zero new failures**; none of the new AUM test files appear in either failure list. Counts reproduced identically on two consecutive runs. Full logs: `/tmp/opencode/dotnet-test.log` (sha256 `63254f56…`), `/tmp/opencode/vitest-run.log` (sha256 `8c866f4cd16e9ee69a86efb9be17ac0c656193fe81b59fb45e69ae3f98bb6312`).

Build evidence (envelope `build_*` fields cover the backend build; both builds are green):

| Build | Command (cwd) | Exit | Log sha256 |
|-------|---------------|------|------------|
| Backend | `dotnet build` (backend/) | 0 | `f48bd1d42440f0990d4cb090ba0741f29c51d7ea987b3a19157c44dc580f5171` |
| Frontend | `vite build` (frontend/) | 0 | `f9b2efe0cf0bc774a0d977db2ec504968e17371131c8cdee4385a4dd16778a04` |

## Scenario → Test Map (21/21)

| # | Scenario | Verdict | Covering test(s) → file:line |
|---|----------|---------|------------------------------|
| 1 | admin-changes-role | PASS | AdminUserManagementIntegrationTests.PutAdminUsersRole_WithAdminCookie_ReturnsOkAndPersistsRole → AdminUserManagementIntegrationTests.cs:149; AdminControllerTests.UpdateUserRole_Success_ReturnsOkWithSummary_AndAuditsTargetUser → AdminControllerTests.cs:868; AdminServiceTests.UpdateUserRoleAsync_ExistingUser_PersistsRoleAndReturnsSummary → AdminServiceTests.cs:150 |
| 2 | self-role-edit-400 | PASS | PutAdminUsersRole_SelfEdit_ReturnsBadRequest → :176; UpdateUserRole_SelfEdit_Returns400_NeitherServiceNorAuditRun → :905 (service + audit Times.Never ×2) |
| 3 | role-edit-unknown-user-404 | PASS | PutAdminUsersRole_UnknownUser_ReturnsNotFound → :193; UpdateUserRole_UnknownUser_Returns404_NoAudit → :921; UpdateUserRoleAsync_UnknownUser_ThrowsKeyNotFoundException → AdminServiceTests.cs:169 |
| 4 | sinacceso-403-all-gated | **VIOLATED (C1)** | SinAcceso_LoginStillSucceeds_RoleGatedEndpointsReturn403OnlyAfterNextLogin → :320 covers RequireScanAccessRole (/api/events/manage) only; EventOwnership-gated endpoints untested and non-compliant — see C1 |
| 5 | sinacceso-login-succeeds | PASS | same WAF test → :351 (re-login 200 with SinAcceso claim) |
| 6 | sinacceso-redirect-home | PASS | Login.test.jsx:72-97 (it.each SinAcceso→'/'); getRedirectPath default '/' → Login.jsx:14 |
| 7 | role-enum-append-only | PASS | UserRole_StoredInts_DeserializeUnchanged_AndSinAccesoIsIndex3 → AuthenticationPropertyTests.cs:632; UserRole_JsonRoundTrip_SerializesSinAccesoByName → :646; guard comment → UserRole.cs:11-13 |
| 8 | reset-returns-usable-credential | PASS | PostAdminUsersResetPassword_ReturnsUsableTempPassword_OldPasswordStopsWorking → :236 (12–16 alnum; old login 401; temp login 200); ResetPasswordAsync_ExistingUser_PersistsHashThatVerifies_OldPasswordStopsAuthenticating → AuthenticationPropertyTests.cs:589 |
| 9 | credential-absent-audit-logs | PASS | PostAdminUsersResetPassword_AuditRowIsCredentialFree → :271 (real audit rows, DoesNotContain(temp)); ResetPassword_Success_..._AndAuditsWithoutCredential → AdminControllerTests.cs:956 (exact Details, no credential) |
| 10 | reset-unknown-user-404 | PASS | PostAdminUsersResetPassword_UnknownUser_ReturnsNotFound → :302; ResetPassword_UnknownUser_Returns404_NoAudit → :990; ResetPasswordAsync_UnknownUser_ReturnsFailure_WithPinnedMessage → :577 |
| 11 | reset-self-allowed | PASS (W1) | ResetPassword_SelfReset_Returns200_GuardIsRoleOnly → AdminControllerTests.cs:1006 (unit only; no WAF case) |
| 12 | temp-password-passes-policy | PASS | GeneratedTempPasswords_AlwaysSatisfyTempPasswordPolicy [FsCheck Property] → AuthenticationPropertyTests.cs:531; PasswordGenerator_ConsecutiveCalls_ProduceRandomLookingCredentials → :556 |
| 13 | role-change-no-session-revocation | PASS | WAF test → AdminUserManagementIntegrationTests.cs:343-348 (old cookie still 200 after role change) |
| 14 | next-login-picks-up-role | PASS | same WAF test → :350-357 (re-login → 403) |
| 15 | actions-column-offers-flows | PASS | AdminPanel.test.jsx:1226-1237 (both menuitems per row) |
| 16 | role-edit-modal-works | PASS | AdminPanel.test.jsx:1239-1266 (PUT + success feedback + list reload); RoleEditModal.test.jsx:39 |
| 17 | self-role-edit-ui-guard | PASS | RoleEditModal.test.jsx:58-75 (alert, modal open, onSuccess never fired) |
| 18 | reset-modal-credential-once | PASS | ResetPasswordModal.test.jsx:30-43, 60-76 (once + Copiar + warning; state cleared; absent from DOM after close) |
| 19 | sinacceso-filter-labels-not-create | PASS | AdminPanel.test.jsx:1268-1286 (badge 'Sin acceso', filter contains SinAcceso, create select ['',Organizador,Staff] unchanged), 1288-1297 (filter works) |
| 20 | auth-matrix-matches-implementation | PASS (C1 caveat) | Manual review AUTHORIZATION_MATRIX.md:53-73 (12 endpoints, RequireAdminRole inherited), :77-94 (SinAcceso column + 2 new rows), :102-108 (next-login note); caveat: rows 84-86 contradict implementation (see C1) |
| 21 | readme-sync-current | PASS | Manual review README.md:192 (roles incl. SinAcceso + next-login note), :181-182 (both new endpoints) |

## Security Checks

| Check | Result | Evidence |
|-------|--------|----------|
| Temp password only in reset 200 body | PASS | AdminResetPasswordResponse single field → AdminController.cs:581-584; unit asserts body-only (:974-977); structural: credential lives only in ResetPasswordResult.TemporaryPassword |
| BCrypt hash-only persistence | PASS | AuthService.cs:204 (`user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword)`) |
| Credential absent from audit Details + logger calls | PASS | Audit details = ids only via Truncate(1000) → AdminController.cs:502; no logger call receives the credential (AuthService.cs:207,218; AdminController.cs:485,513) — guarantee is structural, and asserted by tests |
| `Cache-Control: no-store` on reset 200 | PASS (untested, S2) | AdminController.cs:505 |
| Self role-edit 400 pre-service | PASS | AdminController.cs:432-435 (before service call; audit/service Times.Never ×2) |
| Unknown user 404 both endpoints | PASS | Role: KeyNotFoundException → 404 (:450-453); Reset: "not found" string mapping → 404 (:488-491) |
| RequireAdminRole inherited, no [AllowAnonymous] | PASS | Class-level `[Authorize(Policy = "RequireAdminRole")]` → AdminController.cs:14; grep: zero AllowAnonymous in AdminController |
| CSRF middleware covers both mutating endpoints | PASS (W2) | CsrfHeaderMiddleware.cs:38-44 is method-based (POST/PUT/PATCH/DELETE), path-agnostic; WAF negative test proves PUT rejection (:210-229); all reset POST tests pass with header |
| Enum append-only with guard comment | PASS | UserRole.cs:11-13 (SinAcceso at index 3); AuditActionType appends UpdateUserRole/ResetPassword → AuditLog.cs:87-88 |
| Account rows never deleted | PASS | grep `Users.Remove`: zero hits in production code (only test cleanup, AuthenticationPropertyTests.cs:91) |
| **No policy grants SinAcceso anything (incl. EventOwnership)** | **FAIL — C1** | See below |

## Findings

### CRITICAL

- **C1 — AUM-002 SHALL violation: `EventOwnership` still grants a `SinAcceso` event owner access.** Spec (`specs/admin-user-management/spec.md:36`): "No authorization policy (RequireOrganizadorRole, RequireScanAccessRole, RequireAdminRole, **EventOwnership**) SHALL grant `SinAcceso` anything: a `SinAcceso` user MUST receive 403 on every role-gated endpoint." Implementation: `backend/Authorization/EventOwnershipHandler.cs:25-74` succeeds for **any** owner via the DB check (`e.OrganizerId == userId`, :67-73) regardless of role — only `Admin` short-circuits (:44). No role policy is ANDed on the single-policy endpoints: `GET /api/events/{id}/manage` (EventController.cs:72), `PUT /api/events/{id}` (:119; service re-check EventService.cs:483 is owner-or-admin → owner passes), `POST /api/events/{id}/image` (:226), `GET /api/metrics/events/{id}` (MetricsController.cs:30). (`DELETE /api/events/{id}` is safe: ED-001 service guard is admin-only → 403.) Net effect: a revoked organizer (after next login, claim = SinAcceso) **retains read/update/image/metrics on their own events** — contradicts "pure revocation state that grants nothing" (spec Purpose) and the matrix's own claims (AUTHORIZATION_MATRIX.md:84,86 assert SinAcceso ❌ for edit-own-events/own-metrics — docs match the spec but not the code, so AUM-006 "matches implementation" is also inaccurate until fixed). The WAF test covers only `/api/events/manage` (RequireScanAccessRole); no test exercises SinAcceso × EventOwnership. Fix direction: add an explicit role check in `EventOwnershipHandler` (owner path only for Organizador/Admin; SinAcceso → no succeed) + a WAF test asserting 403 for a SinAcceso owner; or obtain an owner decision to re-scope the spec sentence.

### WARNING

- **W1 — AUM-003 `reset-self-allowed` has no integration coverage.** Covered only at controller-unit level (AdminControllerTests.cs:1006, mocked service). The end-to-end self-reset path (real generator + hash + 200 body) is untested. Low risk — the reset path has no self guard — but the scenario's end-to-end behavior is asserted only indirectly via non-self WAF tests.
- **W2 — CSRF negative test exists only for the PUT endpoint.** `PutAdminUsersRole_WithoutCsrfHeader_ReturnsBadRequest` (AdminUserManagementIntegrationTests.cs:210-229) proves the middleware rejects the role edit without `X-CSRF-PROTECT`; there is no mirrored missing-header test for `POST .../reset-password`. Risk is minimal (middleware is method-based and path-agnostic, CsrfHeaderMiddleware.cs:38-44), but the orchestrator check "CSRF middleware covers both mutating endpoints" is proven directly only for the PUT.

### SUGGESTION

- **S1 — Pre-existing stale CSRF test** `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` (AuthCookieTests.cs:291) posts `/webhook`, but the middleware only exempts `/api/payments/webhook`, so it always fails when dev config is present. Already flagged in apply (#610 Learned 3); out of scope for this change — good first follow-up fix.
- **S2 — Pin `Cache-Control: no-store` with a test.** The header is set (AdminController.cs:505, D11) but no test asserts it; a one-line WAF assertion would protect a security-relevant response header from regression.

## Compliance Summary

- AUM-001: **PASS** (3/3 scenarios; endpoint, guard, audit, error mapping all verified in code + tests)
- AUM-002: **FAIL** (C1 on EventOwnership; other 3 scenarios pass: login succeeds, redirect '/', enum append-only)
- AUM-003: **PASS** (5/5 scenarios; W1/W2 coverage notes)
- AUM-004: **PASS** (2/2 scenarios; next-login semantics verified end-to-end, documented)
- AUM-005: **PASS** (5/5 scenarios; actions column, both modals, credential-once with state clearing, filter/labels/create-select, Login redirect)
- AUM-006: **PASS** (2/2 scenarios; 12-endpoint table, SinAcceso column, next-login note, README synced; rows 84/86 pending C1)

**Next**: resolve C1 (code + test, or owner re-scope), then re-run verification before archive.
