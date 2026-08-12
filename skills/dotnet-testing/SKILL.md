---
name: dotnet-testing
description: "Trigger: .NET test, xUnit, Moq, FsCheck, backend test, strict TDD. Write ASP.NET Core tests following Ticketera's strict TDD conventions."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when writing or running backend tests for the Ticketera .NET API.

## Hard Rules

- Strict TDD: Red → Green → Refactor. No production code without a failing test first.
- xUnit `[Fact]`/`[Theory]`; Moq `Mock<T>` for dependencies.
- Unit tests use InMemory DB: `UseInMemoryDatabase(Guid.NewGuid().ToString())`. Never hit live Supabase from tests.
- Property-based invariants use FsCheck (`FsCheck.Xunit`, `GenStatic`, `PropStatic`).
- Integration tests use `WebApplicationFactory<Program>` (`Program` is `public partial class`).
- Run from `backend/` with `dotnet test`.

## Decision Gates

| Situation | Approach |
|-----------|----------|
| Business rule / invariant | FsCheck property test |
| Service with dependencies | xUnit + Moq + InMemory DB |
| HTTP pipeline / auth / middleware | `WebApplicationFactory<Program>` |
| Pure helper (HMAC, redaction) | xUnit unit test, no DB |

## Execution Steps

1. Write failing test (Red).
2. Implement minimal code (Green).
3. Refactor; keep green.
4. `dotnet test` from `backend/`.

## Output Contract

Return test file path(s), count of tests added, and the `dotnet test` result.

## References

- `backend/Tests/` — canonical patterns: FsCheck in `*PropertyTests.cs`, integration in `*Tests.cs` with `WebApplicationFactory`.
