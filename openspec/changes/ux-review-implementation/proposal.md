# Proposal: Frontend UX Review Implementation

## Intent

A UX/UI review of three frontend screens (Checkout, AddTicketsModal, AdminPanel) produced a list of accessibility, mobile-ux, and copy findings. The user approved implementing all findings EXCEPT two intentional MVP exclusions: (1) blocked paste on email/DNI fields and (2) double email+DNI confirmation fields. This change implements the approved findings and fixes a mobile horizontal-scroll regression in the admin surfaces that the responsive-table work exposed.

## Scope

### In Scope
- Checkout.jsx: inline per-field errors with `role="alert"`/`aria-invalid`/`aria-describedby`, `autocomplete`/`name`/`spellCheck` attributes, `focus-visible:`, explicit `transition-[border-color,box-shadow]`, `useReducedMotion()` for shake/phase animations, non-color countdown cue, "Paso 1 de 2" progress, focus-first-error on submit, `…` ellipses, accent corrections.
- IdentityDocumentInput.jsx: optional external `error` prop with `role="alert"`, `aria-describedby`, `focus-visible:`, transition fix.
- AddTicketsModal.jsx + AdminPanel DeleteConfirmationDialog: reusable `useDialog` hook (focus trap, Escape→onClose, body scroll lock, focus restore, overscroll-contain), `min-h-[44px]` touch targets, submit always enabled (`disabled={busy}`) with on-submit validation, `aria-describedby` on error spans, accent corrections.
- AdminPanel.jsx: responsive card-style tables on mobile via `.admin-table` + `data-label`, Skeleton loading, `min-h-[44px]` action buttons, accent corrections.
- index.css: `touch-action: manipulation`, global `prefers-reduced-motion`, responsive admin-table CSS.
- OrganizerDashboard.jsx: adopt `.admin-table` responsive pattern (was missing the class → horizontal scroll on mobile) + `min-h-[44px]` action buttons.
- Tests: new `useDialog.test.jsx`, updated Checkout/AdminPanel tests, css-migration guard updated.

### Out of Scope (user-mandated exclusions)
- `onPaste` `preventDefault` on email/DNI fields (kept).
- Adding email/DNI confirmation (repeat) fields (kept as-is; existing single confirm fields untouched).
- Any backend change.

## Capabilities

### New Capabilities
- `frontend-ux-quality`: Accessible, mobile-first behavior for checkout, dialogs, and admin tables.

### Modified Capabilities
- None (no previously spec-tracked capability changed its contract).

## Approach

- Delegate implementation to a single writer; hard exclusions passed explicitly.
- Verify no regressions against the documented frontend baseline (26 pre-existing failures in untouched areas: StaffScan QR/camera env, Checkout edit-data PATCH, OrganizerEventDetail `/manage`, identityValidation).
- Fix mobile horizontal scroll: (1) harden `.admin-table` mobile CSS (`min-width: 0`, `overflow-wrap: anywhere`, `word-break: break-word`, `display: block`), (2) add the missing `admin-table` class to OrganizerDashboard metrics table.
