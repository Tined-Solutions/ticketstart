# Review Ledger — ticketera-online

Last updated: 2026-07-09
Scope of this entry: Task 17.4.1 re-review (focused 4R lenses R1 Risk + R4 Resilience) on commits `bd17a8f..3a98d75` (HEAD `3a98d75`, base `ffdbc97`).

## Verdict

- **R1 Risk**: PASS (no open BLOCKER/CRITICAL).
- **R4 Resilience**: PASS (no open BLOCKER/CRITICAL).

Both prior MERGE_BLOCKING findings from the Task 17.4 re-review are **verified resolved**. The slice is clear to proceed to `sdd-verify` for the whole Task 17 (17.1, 17.2, 17.3, 17.4, 17.4.1) slice, with the open WARNING/SUGGESTION items tracked below as known debt for a future 17.4.2 micro-slice.

## Findings

### Prior findings — VERIFIED RESOLVED by Task 17.4.1

| id | lens | location | severity | status | evidence |
|----|------|----------|----------|--------|----------|
| R1-NF-1 | risk | backend/Controllers/TicketController.cs:45,88 | BLOCKER | verified | Email is hashed via `LogRedactor.HashIdentifier(email)` and emitted as `{EmailHash}` in both the success and error log paths. No remaining TicketController log emission interpolates raw email. `RedactingConsoleFormatter_RedactsInlineEmailInRenderedMessage` drives the real controller render through the formatter and asserts raw email absence end-to-end. No leak path remains. |
| R1-NF-3 | risk | backend/Helpers/RedactingConsoleFormatter.cs:25-40 | WARNING | verified | `Write` now wraps the formatter invocation, message redaction, and exception-string write in `try { ... } catch { /* Logging must never fail the request. */ }`. Catch is empty (swallows), no rethrow. `RedactingConsoleFormatter_SwallowsFormatterException` proves a throwing formatter no longer propagates. |
| R4-N-1 | resilience | backend/Middleware/GlobalExceptionHandler.cs:37-42 | BLOCKER | verified | 499 branch sets `StatusCode=499` and `return true;` immediately after the Information log — NO body write, NO fall-through to `WriteAsJsonAsync`. Test `Property47d_OperationCanceled_WithCancelledToken_ReturnsTrueWithoutWriting` constructs a cancelled CTS, asserts handled=true, `Response.Body.Length==0`, no Error-level log. Reproduces the original fault mode and proves the fix. |
| R4-N-2 | resilience | backend/Middleware/GlobalExceptionHandler.cs:74-77 | BLOCKER | verified | `HasStarted` guard is the FIRST statement in the outer catch, before any `StatusCode`/`ContentType`/`WriteAsync`. Test `Property47e_HandlerSelfProtection_ResponseAlreadyStarted_ReturnsTrueWithoutWriting` registers a `StartedResponseFeature` whose `StatusCode` setter throws (mirrors real ASP.NET Core post-Start behavior), asserts handled=true and body length 0. Self-protection confirmed. |

### New findings — OPEN (deferred to 17.4.2)

| id | lens | location | severity | status | evidence |
|----|------|----------|----------|--------|----------|
| R1-001 | risk | backend/Middleware/GlobalExceptionHandler.cs:45-55 | WARNING | open | Fix 4 (`LogError(exception, template, ...)`) newly passes the exception object to `ILogger.Log`. Only the redacted console formatter is wired today (`Program.cs`), which routes `exception.ToString()` through `RedactMessage`, so no live leak. But the stated rationale (commit `b9ec1cd` "para sinks estructurados") anticipates structured sinks; any future Serilog/Seq/AppInsights/file sink will receive the exception object and render `exception.ToString()` (containing raw `exception.Message` — connection strings, JWTs, secrets) **bypassing `RedactMessage`**. Non-console sinks have no redaction gate. Latent exposure introduced by this slice. **Mitigation for 17.4.2**: redact `exception.ToString()` / `exception.Message` at the call site (not relying solely on the console formatter) before enabling any structured sink. |
| R4-001 | resilience | backend/Helpers/RedactingConsoleFormatter.cs:25-40 | WARNING | open | The new `try { … } catch { /* Logging must never fail the request. */ }` correctly prevents a formatter/`RedactMessage` failure from bubbling into the request pipeline, BUT the catch is a bare swallow with no last-resort fallback: if `logEntry.Formatter(state, exception)` or `RedactMessage` throws (regex backtracking, null state, cyclic exception graph in `exception.ToString()`), the log line — including the exception — is LOST SILENTLY with zero signal. `RedactingConsoleFormatter_SwallowsFormatterException` only asserts no exception; it does NOT assert any fallback write. For a security- and observability-critical formatter, a single hidden `RedactMessage` bug would silently drop ALL logs across the whole process. **Mitigation for 17.4.2**: add a last-resort `Console.Error.WriteLine("[redacted-formatter-failed]")` (or stderr write) inside the catch so log-subsystem failure remains observable. |
| R4-002 | resilience/observability | backend/Middleware/GlobalExceptionHandler.cs:39 | SUGGESTION | open | The 499 path's only observability is the Information log `"Client disconnected during request"` — no `CorrelationId`, no `Method`, no `Path`, no token-state. Cannot triangulate client-disconnects by endpoint for SLO/error-budget purposes. Reusing the enriched structured template (method/pathAndQuery/correlationId) for the 499 branch would make client-disconnects a first-class SLO signal. |
| R4-003 | resilience | backend/Middleware/GlobalExceptionHandler.cs:40 | SUGGESTION | open | The 499 branch sets `StatusCode = 499` without a local `HasStarted` guard. Functionally safe (the outer catch's guard catches it), but it pays a throw/catch cost on every late-disconnect and relies on the outer catch. A `if (httpContext.Response.HasStarted) return true;` before the `StatusCode=499` line would make the 499 branch self-sufficient and consistent with the outer catch's defensive style. |

### New findings — INFO (no action)

| id | lens | location | severity | status | evidence |
|----|------|----------|----------|--------|----------|
| R1-002 | risk | backend/appsettings.json:10-11 (history) | CRITICAL | info | Standing reminder, out-of-scope for this slice (deferred security incident). A real Supabase DB cleartext password is committed in `appsettings.json` and lives in git history (both `DefaultConnection` and `MigrationConnection`, lines 10-11). Not introduced by this slice; no new secret/credential introduced by the touched code. Must be rotated and moved to user-secrets/env in a dedicated `security/credential-rotation` change — does not block this micro-slice. (Password value intentionally omitted from this ledger — see `backend/appsettings.json`。）|
| R4-004 | resilience/test-determinism | backend/Tests/ErrorHandlingPropertyTests.cs:113-122,194-223; backend/Tests/LogRedactorTests.cs:167-301 | SUGGESTION | info | Tests are hermetic and deterministic — no real HTTP, no shared mutable state, no timing. `Property47d` uses a pre-cancelled CTS. `Property47e` uses the `StartedResponseFeature` double. `RedactingConsoleFormatter_RedactsInlineEmailInRenderedMessage` uses `Task.Run(...).Wait()` with a Moq `ITicketService` returning an empty list — deterministic. No flaky path. |
| R4-005 | resilience/rollback | touched files (code + tests only) | SUGGESTION | info | Rollback safety confirmed: pure code + test changes, no DB migration/schema/config/DI/package. `git revert ffdbc97..HEAD -- backend/` (excluding the docs commit `3a98d75`) is a clean revert. |

## Re-review sweep log

- Sweep 1: R1 found R1-001 (open WARNING) + R1-002 (info); prior R1-NF-1, R1-NF-3 verified. R4 found R4-001..R4-005; prior R4-N-1, R4-N-2 verified.
- Sweep 2 (R4): zero new findings. Stopping after 2 consecutive dry sweeps.

## Next

- Proceed to `sdd-verify` for the whole Task 17 slice (17.1, 17.2, 17.3, 17.4, 17.4.1).
- Open WARNING/SUGGESTION items (R1-001, R4-001, R4-002, R4-003) are deferred to a future **Task 17.4.2** micro-slice; they do NOT block verify or the single PR for the change.