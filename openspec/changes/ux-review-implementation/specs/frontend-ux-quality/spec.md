# Frontend UX Quality Specification

## Purpose

The checkout, dialogs, and admin tables MUST be accessible (WCAG-aligned), usable on mobile (44px touch targets, no horizontal page scroll, reduced-motion support), and have correct Spanish (Rioplatense) microcopy. Two behaviors are intentionally EXCLUDED for MVP: blocked paste on email/DNI fields, and double email+DNI confirmation fields.

## Non-Goals

Blocked-paste on email/DNI, double email+DNI confirmation, backend changes, refactoring existing `Modal.jsx` onto the new hook, and changing `type="number"` inputs.

## Requirements

### Requirement: UXQ-001: Inline per-field form errors

Checkout and AddTicketsModal MUST render field-level errors with `role="alert"`, set `aria-invalid` on the invalid input, and link input ↔ error via `aria-describedby`. A global error Badge is allowed only for API errors and MUST carry `role="alert"`.

#### Scenario: Submit with invalid field shows inline error

- GIVEN the checkout form with an empty required field
- WHEN the user submits
- THEN the field shows an inline error with `role="alert"` and `aria-describedby` links it to the input, and focus moves to the first invalid field

### Requirement: UXQ-002: Dialog behavior (focus trap, Escape, scroll lock)

Dialogs (AddTicketsModal, AdminPanel DeleteConfirmationDialog) MUST trap focus, close on Escape, lock body scroll, contain overscroll, restore focus to the previously focused element on close, and auto-focus the first focusable element on open.

#### Scenario: Escape closes and focus returns

- GIVEN a dialog is open
- WHEN the user presses Escape
- THEN the dialog closes and focus returns to the element that opened it

#### Scenario: Tab cycles inside the dialog

- GIVEN a dialog is open
- WHEN the user presses Tab past the last focusable element
- THEN focus wraps to the first focusable element inside the dialog (no focus leaks to the page behind)

### Requirement: UXQ-003: Mobile touch targets

Interactive controls in dialogs and admin table rows MUST have a minimum 44px touch target.

#### Scenario: Dialog buttons are ≥44px

- GIVEN a dialog with action buttons
- WHEN rendered on a mobile viewport
- THEN each button's hit area is at least 44px high

### Requirement: UXQ-004: No horizontal page scroll on admin mobile

Admin tables (AdminPanel events/users, OrganizerDashboard metrics) MUST render as card-style rows below `md` (767px) with no horizontal page scroll. Long unbroken content (emails, names) MUST wrap instead of overflowing.

#### Scenario: Long email on mobile does not overflow

- GIVEN the users table with a long email address on a mobile viewport
- WHEN the table renders
- THEN the row wraps the email text and the page has no horizontal scroll

#### Scenario: OrganizerDashboard table uses the responsive pattern

- GIVEN the OrganizerDashboard metrics table on a mobile viewport
- WHEN it renders
- THEN it uses the same `.admin-table` card-style layout as AdminPanel (no horizontal scroll)

### Requirement: UXQ-005: Reduced-motion support

Shake animations, phase transitions, and global motion MUST respect `prefers-reduced-motion`.

#### Scenario: Reduced motion collapses animations

- GIVEN the user has `prefers-reduced-motion: reduce` enabled
- WHEN a checkout error triggers the shake animation
- THEN no shake is rendered (animation collapsed)

### Requirement: UXQ-006: Correct Spanish microcopy

User-visible strings MUST use correct Rioplatense Spanish accents: "válida", "catálogo", "Ubicación", "administración", "Contraseña", "válido", "Eliminación", "número", "Guardando…", "Eliminando…".

#### Scenario: Accent-corrected strings render

- GIVEN the checkout, AddTicketsModal, and AdminPanel render
- WHEN inspecting user-visible text
- THEN accented forms are used (no "valida", "catalogo", "Ubicacion", "administracion", etc.)

### Requirement: UXQ-007: Excluded behaviors stay intact

The `onPaste` `preventDefault` on email/confirm-email/confirm-DNI fields and the absence of new double-confirmation fields MUST be preserved.

#### Scenario: Paste blocking remains

- GIVEN the checkout email/DNI fields
- WHEN a user attempts to paste
- THEN paste is still blocked (unchanged behavior)

#### Scenario: No new confirmation fields

- GIVEN the checkout form
- WHEN it renders
- THEN no additional repeat-email/repeat-DNI confirmation fields exist beyond the pre-existing single confirm fields
