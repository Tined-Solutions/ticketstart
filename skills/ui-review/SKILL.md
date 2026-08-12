---
name: ui-review
description: "Trigger: UI review, visual review, visual consistency, polish, responsive. Review frontend UI for visual quality and state coverage."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when reviewing or polishing React UI for visual quality and completeness.

## Hard Rules

- Every data view covers loading (`Skeleton`), empty (`EmptyState`), and error (`role="alert"` + retry) — not just the happy path.
- Consistent spacing and typography hierarchy from design tokens (`font-heading` for headings).
- Responsive first: verify narrow and wide layouts; no horizontal overflow.
- No raw hex or inconsistent colors; no dead/unused styles.
- Check focus, hover, and reduced-motion behavior.

## Decision Gates

| State | Component |
|-------|-----------|
| Loading | `Skeleton` |
| Empty | `EmptyState` |
| Error | `role="alert"` + retry action |

## Execution Steps

1. Audit the view against the three states.
2. Flag token/hierarchy inconsistencies.
3. Fix using design-system tokens and existing components.

## Output Contract

Return the states added/fixed and any visual inconsistencies resolved.

## References

- `frontend/src/components/ui/Skeleton.jsx`, `frontend/src/components/ui/EmptyState.jsx`, `frontend/src/tokens.css`.
