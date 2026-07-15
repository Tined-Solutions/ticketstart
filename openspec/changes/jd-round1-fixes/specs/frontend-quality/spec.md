# Frontend Quality Specification

## Purpose

Extract shared utilities, fix component bugs, improve error handling and accessibility, harden StaffScan, add error boundaries, and fix authentication-related frontend issues.

## JD Findings Covered

JD-W6, JD-W7, JD-W11, JD-W12, JD-W13, JD-W14, JD-W15, JD-W30, JD-W31, JD-SG3, JD-SG7, JD-SG8, JD-SG9, JD-SG11, JD-SG12

## Requirements

### REQ-1: Shared Utility Extraction

The system MUST extract `formatEventDate`, `formatCurrency` to `src/lib/format.js` and `getErrorMessage` to `src/lib/apiError.js`, replacing all duplicated inline implementations.

**JD-W6** — Files: `frontend/src/lib/format.js` (new), `frontend/src/lib/apiError.js` (new), 7+ consuming files

#### Scenario: Formatters imported from shared module

- GIVEN any component that formats dates or currency
- WHEN the component renders
- THEN it uses the imported function from `src/lib/format.js`

#### Scenario: Error messages use shared helper

- GIVEN any component that displays API error messages
- WHEN an error occurs
- THEN the message is produced by `src/lib/apiError.js`

**Tests**: Unit tests for each utility function (date formatting, currency formatting, error extraction).

---

### REQ-2: RoleGuard Shows 403 Page

The system MUST display a 403 "Not Authorized" page instead of silently redirecting when a user lacks the required role.

**JD-W7** — File: `frontend/src/components/RoleGuard.jsx`

#### Scenario: Unauthorized user sees 403 page

- GIVEN a user without the required role accesses a guarded route
- WHEN RoleGuard evaluates the role
- THEN a 403 page is displayed with an explanation
- AND no silent redirect to `/` occurs

**Tests**: Frontend test rendering RoleGuard with insufficient role.

---

### REQ-3: EventForm Validation and Error Handling

The system MUST validate `eventId` before PUT and use correct error feedback in catch blocks.

**JD-W11, JD-W12** — File: `frontend/src/components/EventForm.jsx`

#### Scenario: PUT blocked when eventId is undefined

- GIVEN `initialData?.id` is undefined
- WHEN the form is submitted
- THEN no PUT request is sent and an error is shown

#### Scenario: Upload failure shows error not success

- GIVEN an image upload fails
- WHEN the catch block executes
- THEN the feedback type is `error` or `warning` (not `success`)

**Tests**: Frontend test for undefined eventId; test for catch block feedback type.

---

### REQ-4: Modal Focus Trap Re-evaluation

The system MUST re-evaluate focusable nodes on each Tab press in the Modal component.

**JD-W13** — File: `frontend/src/components/Modal.jsx`

#### Scenario: Dynamic content focusable after Tab

- GIVEN a modal with dynamically added focusable elements
- WHEN the user presses Tab
- THEN the focus trap includes the newly added elements

**Tests**: Frontend test with dynamic content and Tab key simulation.

---

### REQ-5: ToastProvider useRef for nextId

The system MUST use `useRef` for the `nextId` counter in ToastProvider to prevent HMR persistence.

**JD-W14** — File: `frontend/src/context/ToastProvider.jsx`

#### Scenario: nextId resets on remount

- GIVEN the ToastProvider unmounts and remounts (e.g., HMR)
- WHEN a new toast is created
- THEN `nextId` starts from the initial value (not persisted from previous mount)

**Tests**: Frontend test verifying useRef behavior across remounts.

---

### REQ-6: StaffScan Hardening

The system MUST validate GUID format before API calls, use `useRef` with cleanup, and persist scan history in `sessionStorage`.

**JD-W15, JD-SG7** — File: `frontend/src/pages/StaffScan.jsx`

#### Scenario: Invalid GUID rejected client-side

- GIVEN the user enters a non-GUID string
- WHEN the scan is submitted
- THEN no API call is made and a validation error is shown

#### Scenario: Scan history survives page refresh

- GIVEN a staff member has performed scans
- WHEN the page is refreshed
- THEN scan history is restored from `sessionStorage`

**Tests**: Frontend test for GUID validation; test for sessionStorage persistence.

---

### REQ-7: OrganizerEventDetail Authenticated Endpoint

The system MUST use the authenticated `GET /events/{id}/manage` endpoint with `EventOwnership` policy instead of the anonymous endpoint.

**JD-W30** — File: `frontend/src/pages/OrganizerEventDetail.jsx`

#### Scenario: Organizer loads event via authenticated endpoint

- GIVEN an authenticated organizer views an event detail page
- WHEN the component fetches event data
- THEN it calls `GET /events/{id}/manage` (not the public endpoint)

**Tests**: Frontend test verifying the correct API URL is called.

---

### REQ-8: EventForm Content-Type Auto-Detection

The system MUST NOT set explicit `Content-Type` headers in EventForm, allowing axios to auto-detect multipart boundaries.

**JD-W31** — File: `frontend/src/components/EventForm.jsx`

#### Scenario: File upload with auto-detected boundary

- GIVEN a form submission with file upload
- WHEN the request is sent
- THEN axios auto-detects `Content-Type: multipart/form-data` with correct boundary

**Tests**: Frontend test verifying no explicit Content-Type header is set.

---

### REQ-9: ErrorBoundary on Routes

The system MUST wrap route content with an `ErrorBoundary` to prevent a single component crash from taking down the entire app.

**JD-SG3** — File: `frontend/src/App.jsx`

#### Scenario: Component error caught by boundary

- GIVEN a route component throws an error
- WHEN the error propagates
- THEN the ErrorBoundary catches it and displays a fallback UI
- AND other routes remain functional

**Tests**: Frontend test with a throwing component verifying boundary catches it.

---

### REQ-10: Card Stops Spreading Arbitrary Props

The system MUST NOT spread unknown props onto the DOM element in the Card component.

**JD-SG8** — File: `frontend/src/components/Card.jsx`

#### Scenario: Unknown props not passed to DOM

- GIVEN `<Card customProp="value">` is rendered
- WHEN the DOM is inspected
- THEN `customProp` does not appear on the rendered DOM element

**Tests**: Frontend test verifying prop filtering.

---

### REQ-11: Explicit vi Import in Tests

The system MUST use explicit `vi` imports in test files instead of relying on `globals: true`.

**JD-SG9** — File: `frontend/src/components/__tests__/accessibility.test.jsx`

#### Scenario: vi imported explicitly

- GIVEN a test file that uses `vi`
- WHEN the file is inspected
- THEN `import { vi } from 'vitest'` is present at the top

**Tests**: Grep-based verification.

---

### REQ-12: Native Button Elements

The system MUST use native `<button>` elements instead of `div` with `role="button"` and manual keyboard handling.

**JD-SG11** — File: `frontend/src/pages/EventList.jsx`

#### Scenario: Clickable elements are native buttons

- GIVEN the EventList page renders clickable cards
- WHEN the DOM is inspected
- THEN clickable elements are `<button>` (not `<div role="button">`)

**Tests**: Frontend test verifying element type.

---

### REQ-13: 404 Page Navigation Link

The system MUST include a navigation link on the 404 page so users are not stranded.

**JD-SG12** — File: `frontend/src/pages/NotFound.jsx`

#### Scenario: 404 page has home link

- GIVEN the user lands on the 404 page
- WHEN the page renders
- THEN a link to navigate back to the home page is present

**Tests**: Frontend test verifying link presence.
