---
name: react-testing
description: "Trigger: React test, vitest, Testing Library, frontend test. Write frontend tests following Ticketera's vitest + Testing Library conventions."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when writing or running frontend tests for the Ticketera React app.

## Hard Rules

- Vitest config lives in `frontend/vite.config.js` (`test` block): jsdom, `globals`, `setupFiles ./src/test/setup.js`, `forbidOnly: true`, `maxWorkers: 1`.
- Query by role/label/text (`getByRole`, `getByLabelText`, `getByText`) — never by implementation detail.
- Interact with `@testing-library/user-event` (`userEvent.click`, `userEvent.keyboard`); avoid `fireEvent`.
- Assert with `@testing-library/jest-dom` matchers (`toBeInTheDocument`, `toHaveFocus`).
- Mock `src/api/client.js` with `vi.mock` for data-fetching components.
- Run: `npm test` from `frontend/` (wraps `npx vitest run`).

## Decision Gates

| Situation | Approach |
|-----------|----------|
| Component render/behavior | `render` + `getByRole` + `userEvent` |
| Hook in isolation | `renderHook` from `@testing-library/react` |
| Route page | `MemoryRouter` with `initialEntries` |
| Async data (react-query) | `vi.mock` the api client |

## Execution Steps

1. Write failing test (Red).
2. Implement (Green).
3. `npm test` from `frontend/`.

## Output Contract

Return test file path(s), tests added, and the `npm test` result.

## References

- `frontend/src/test/setup.js`, `frontend/vite.config.js`, `frontend/src/components/ui/__tests__/`.
