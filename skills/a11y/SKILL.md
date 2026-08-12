---
name: a11y
description: "Trigger: accessibility, a11y, ARIA, keyboard, screen reader, focus. Apply Ticketera's accessibility conventions to React UI."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when building or reviewing React UI where accessibility matters.

## Hard Rules

- Semantic HTML first (`button`, `dialog`, `heading`); add ARIA only when HTML cannot express it.
- Dialogs: `role="dialog"` + `aria-modal` + `aria-label`/`aria-labelledby` + focus trap (`useDialog`).
- Live regions: `aria-live="polite"` for toasts, `role="alert"` for errors, `role="status"` for loading.
- Forms: `label htmlFor`/`id` pairs, `aria-invalid` + `aria-describedby` on error fields.
- Decorative icons/images: `aria-hidden="true"`.
- Motion must respect `prefers-reduced-motion` (`useReducedMotion` / `prefersReducedMotion`).
- Prove semantics in tests via `getByRole`.

## Decision Gates

| Need | Mechanism |
|------|-----------|
| Modal / dialog | `role="dialog"` + focus trap |
| Toast / notification | `aria-live="polite"` |
| Inline error | `role="alert"` + `aria-describedby` |
| Loading state | `role="status"` + `aria-label` |
| Decorative visual | `aria-hidden="true"` |

## Execution Steps

1. Use the correct semantic element.
2. Add ARIA only where needed.
3. Verify keyboard + screen-reader behavior with a `getByRole` test.

## Output Contract

Return the element/ARIA changes and the test that proves the semantics.

## References

- `frontend/src/hooks/useDialog.js`, `frontend/src/context/ToastProvider.jsx`, `frontend/src/components/ui/EmptyState.jsx`.
