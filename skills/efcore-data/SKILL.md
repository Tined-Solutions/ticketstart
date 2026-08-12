---
name: efcore-data
description: "Trigger: EF Core, migration, DbContext, query, N+1, database update. Apply Ticketera's EF Core data-access and migration conventions."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when touching DbContext, entity models, migrations, or data-access queries.

## Hard Rules

- Single `ApplicationDbContext` in `backend/Data/`; configure entities in `OnModelCreating`.
- Read-only queries use `.AsNoTracking()`.
- Async everywhere: `ToListAsync`/`SingleAsync`/`FirstAsync`/`CountAsync`.
- Avoid N+1: `.Include(...)` only when the navigation is actually consumed.
- Migrations are MANUAL — the app does NOT auto-migrate at startup.

## Decision Gates

| Situation | Action |
|-----------|--------|
| Add/change a table or column | `dotnet ef migrations add <Name>` then apply (below) |
| Apply pending migrations | `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --context TicketeraOnline.Api.Data.ApplicationDbContext` |
| Read-only query | `.AsNoTracking()` |
| Runtime vs migrations connection | `DefaultConnection` (runtime) vs `MigrationConnection` (migrations) |

## Execution Steps

1. Edit model / `OnModelCreating`.
2. `dotnet ef migrations add <Name>` from `backend/`.
3. Review the generated migration before applying.
4. Apply with `--context TicketeraOnline.Api.Data.ApplicationDbContext` and `ASPNETCORE_ENVIRONMENT=Development`.
5. Run tests.

## Output Contract

Return migration name, files changed, and confirmation that `database update` was applied (or why it was deferred).

## References

- `backend/Data/ApplicationDbContext.cs`, `backend/Migrations/`, `backend/Program.cs` (DbContext registration).
