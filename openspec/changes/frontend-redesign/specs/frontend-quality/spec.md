# Delta for frontend-quality

Extends the existing `frontend-quality` spec (base: `openspec/changes/jd-round1-fixes/specs/frontend-quality/spec.md`). All existing behavioral REQs (REQ-1 through REQ-13, covering JD-W6–W15 and JD-SG3–SG12) are PRESERVED. This delta adds visual quality acceptance criteria layered over the existing behavioral baseline.

## ADDED Requirements

### REQ-FQ-V1: No Behavioral Regression
**Priority**: P0
**Status**: proposed

All existing behavioral requirements (REQ-1 through REQ-13) MUST continue to pass after the redesign. The visual overhaul MUST NOT alter any component behavior, API call patterns, error handling, or accessibility contracts established by the base spec.

> **Scenario: Existing behavioral tests pass after redesign**
> **Given** the redesign is applied to all pages and components
> **When** the existing test suite runs
> **Then** all 13 behavioral REQs (formatting, RoleGuard, EventForm validation, Modal focus trap, ToastProvider useRef, StaffScan hardening, authenticated endpoints, Content-Type detection, ErrorBoundary, Card props, explicit imports, native buttons, 404 link) still pass

> **Scenario: Visual changes do not alter API contracts**
> **Given** the EventDetail page is restyled with GlassCard components
> **When** the page fetches event data
> **Then** it still calls `GET /events/{id}/manage` (preserving REQ-7)

---

### REQ-FQ-V2: Minimum Contrast Ratio (WCAG AA)
**Priority**: P0
**Status**: proposed

Every page MUST maintain a minimum contrast ratio of 4.5:1 for normal text and 3:1 for large text in BOTH dark and light themes, per WCAG AA.

> **Scenario: Text meets contrast in dark theme**
> **Given** `data-theme="dark"` is active
> **When** any public page renders (Home, EventList, EventDetail, Checkout, CheckoutReturn, Login, TicketLookup, NotFound)
> **Then** all body text has ≥4.5:1 contrast against its background

> **Scenario: Text meets contrast in light theme**
> **Given** `data-theme="light"` is active
> **When** any public page renders
> **Then** all body text has ≥4.5:1 contrast against its background

> **Scenario: Glass surfaces maintain readable text**
> **Given** a `GlassCard` or `.glass-surface` is rendered over content
> **When** text is displayed on the glass surface
> **Then** the effective contrast ratio (text against blended background) is ≥4.5:1

---

### REQ-FQ-V3: Visible Focus Rings
**Priority**: P0
**Status**: proposed

All interactive elements (buttons, links, inputs, selects, toggles) MUST have visible focus rings when focused via keyboard navigation. Focus rings MUST be visible in both themes.

> **Scenario: Button shows focus ring on Tab**
> **Given** the user navigates via keyboard Tab
> **When** focus lands on a `<button>` element
> **Then** a visible focus ring (outline or ring) appears around the button

> **Scenario: Focus ring is visible in dark theme**
> **Given** `data-theme="dark"` is active
> **When** any interactive element receives keyboard focus
> **Then** the focus ring contrasts against the dark background

> **Scenario: Focus ring is visible in light theme**
> **Given** `data-theme="light"` is active
> **When** any interactive element receives keyboard focus
> **Then** the focus ring contrasts against the light background

> **Scenario: Glass buttons show visible focus**
> **Given** a `<Button variant="glass">` receives keyboard focus
> **When** the focus ring renders
> **Then** the ring is visible against the glass surface and any background content

---

### REQ-FQ-V4: Existing Test Suite Integrity
**Priority**: P0
**Status**: proposed

All 262 existing frontend tests MUST pass after the redesign is applied. No existing test file MAY be removed or have its assertions relaxed to accommodate visual changes.

> **Scenario: Full test suite passes**
> **Given** the redesign is complete across all phases
> **When** `npm test` runs in `frontend/`
> **Then** all 262 existing tests pass with zero failures

> **Scenario: No test assertions weakened**
> **Given** a visual change would cause a behavioral test to fail
> **When** implementing the change
> **Then** the component behavior is adjusted to match the test expectation — not the test changed to match new visuals
