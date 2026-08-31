---
name: aspnet-api-design
description: "Trigger: API endpoint, controller, service, ProblemDetails, DTO, authorization policy. Apply Ticketera's ASP.NET Core API conventions."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when adding or changing an API endpoint, controller, service, DTO, or authorization policy.

## Hard Rules

- Service pattern: `IXxx` interface + `XxxService` impl in `backend/Services/`, registered in `Program.cs`.
- Controllers inherit `TicketeraControllerBase`; resolve identity via `TryGetUserId` / `TryGetUserRole`, not raw claims.
- `[ApiController]` + `[Authorize(Policy = "...")]`; reuse existing policies (`EventOwnership`, `RequireOrganizadorRole`, `RequireScanAccessRole`, `RequireAdminRole`).
- Map domain exceptions in controllers (NotFound/Forbid/BadRequest); `GlobalExceptionHandler` returns RFC 7807 `ProblemDetails` as fallback.
- Validate via DTOs; never trust client input.

## Decision Gates

| Need | Mechanism |
|------|-----------|
| Owner-or-admin on a resource | `EventOwnership` policy + handler |
| Role-gated endpoint | `Require*Role` policy |
| External callback | exempt in `CsrfHeaderMiddleware` + signature validation |

## Execution Steps

1. Define/update DTO in `Models/`.
2. Add `IXxx` / `XxxService`.
3. Wire endpoint in controller with the right policy.
4. Register service in `Program.cs`.
5. Add tests.

## Output Contract

Return changed files and the authorization policy used.

## References

- `backend/Controllers/EventController.cs`, `backend/Controllers/TicketeraControllerBase.cs`, `backend/Program.cs`.
