# Spec: page-visual-design

## Purpose

Per-page visual treatments for all public-facing pages: Home, EventList, EventDetail, Checkout, CheckoutReturn, Login, TicketLookup, and NotFound. Organizer/admin pages are Phase 3 (out of scope). Every page MUST respect dark/light theme and `prefers-reduced-motion`.

## Requirements

### REQ-P1: Home Page — Full Gradient Hero
**Priority**: P0
**Status**: proposed

The Home page MUST feature a full-viewport gradient hero with "Ticketera" branding, a CTA to browse events, and animated entry (600ms).

> **Scenario: Hero fills viewport**
> **Given** the user loads the Home page
> **When** the page renders
> **Then** the GradientHero occupies ≥90% of the viewport height

> **Scenario: Brand and CTA visible**
> **Given** the Home hero is visible
> **When** the page renders
> **Then** the "Ticketera" wordmark and a CTA button (e.g., "Browse Events") are visible

---

### REQ-P2: EventList — Glass Card Grid
**Priority**: P0
**Status**: proposed

The EventList page MUST display events in a grid of `GlassCard` components with image-forward design, date overlays, hover lift animation (200ms), and `Skeleton` loading placeholders.

> **Scenario: Events render as glass cards**
> **Given** events exist in the API response
> **When** the EventList page renders
> **Then** each event is shown in a `GlassCard` with image, title, and date overlay

> **Scenario: Hover lift animation**
> **Given** the user hovers over an event card
> **When** the pointer enters the card
> **Then** the card lifts (translateY or scale) with a 200ms transition

> **Scenario: Skeleton loading state**
> **Given** the events API is still loading
> **When** the EventList page renders
> **Then** `Skeleton` placeholders are shown in the grid layout until data arrives

---

### REQ-P3: EventDetail — Hero + Glass Ticket Selection
**Priority**: P0
**Status**: proposed

The EventDetail page MUST feature a hero image with gradient overlay, glass ticket-type selection cards, and animated quantity controls.

> **Scenario: Hero image with overlay**
> **Given** an event with an image
> **When** the EventDetail page renders
> **Then** the hero section shows the event image under a dark gradient overlay with title and date

> **Scenario: Ticket type selection as glass cards**
> **Given** the event has multiple ticket types
> **When** the user views the page
> **Then** each ticket type is rendered as a `GlassCard` with price, availability, and quantity selector

> **Scenario: Quantity controls animate**
> **Given** the user increments or decrements ticket quantity
> **When** the button is clicked
> **Then** the quantity number animates (scale pulse or number transition, ~200ms)

---

### REQ-P4: Checkout — Two-Phase Glass Flow
**Priority**: P0
**Status**: proposed

The Checkout page MUST use a two-phase flow (details → confirmation) rendered inside glass panels, with an animated countdown timer and form validation feedback via micro-interactions.

> **Scenario: Phase 1 — attendee details form**
> **Given** the user enters checkout with ticket selections
> **When** the page renders
> **Then** a glass panel shows the order summary and attendee detail form fields

> **Scenario: Phase 2 — confirmation**
> **Given** the user submits valid attendee details
> **When** the form is submitted
> **Then** the view transitions to a confirmation summary with a "Complete Purchase" action

> **Scenario: Countdown timer visible**
> **Given** the checkout session has a time limit
> **When** the checkout page renders
> **Then** an animated countdown timer is visible and decrements in real time

> **Scenario: Validation error micro-interaction**
> **Given** the user submits with an empty required field
> **When** the form validates
> **Then** the field border shakes or pulses red briefly (~200ms) and an error message appears

---

### REQ-P5: CheckoutReturn — Animated Status Display
**Priority**: P1
**Status**: proposed

The CheckoutReturn page MUST display the payment status with an animated icon (success/warning/error), a glass card containing the result message, and a return-to-events link.

> **Scenario: Success state with animated checkmark**
> **Given** the payment was successful
> **When** the CheckoutReturn page renders
> **Then** an animated checkmark icon and success message are visible inside a `GlassCard`

> **Scenario: Error state with warning icon**
> **Given** the payment failed
> **When** the CheckoutReturn page renders
> **Then** an error icon and failure explanation are visible inside a `GlassCard`

---

### REQ-P6: Login — Centered Glass Card
**Priority**: P1
**Status**: proposed

The Login page MUST display a centered `GlassCard` with branded header, animated button feedback, and error state animations.

> **Scenario: Centered glass card layout**
> **Given** the user navigates to `/login`
> **When** the page renders
> **Then** a `GlassCard` containing the login form is horizontally and vertically centered

> **Scenario: Button shows loading feedback**
> **Given** the user clicks "Login"
> **When** the auth request is in flight
> **Then** the button shows a spinner or loading state with animation

> **Scenario: Error state with shake animation**
> **Given** the login attempt fails
> **When** the error response arrives
> **Then** the form card shakes briefly (~200ms) and the error message appears

---

### REQ-P7: TicketLookup — Glass Card with Resend Feedback
**Priority**: P1
**Status**: proposed

The TicketLookup page MUST display ticket information in a read-only `GlassCard` layout, with a resend section that provides animated feedback on action.

> **Scenario: Ticket info in glass card**
> **Given** a valid ticket lookup code
> **When** the ticket data loads
> **Then** ticket details render inside a `GlassCard` in a read-only display

> **Scenario: Resend button with feedback**
> **Given** the user clicks "Resend Ticket"
> **When** the action completes
> **Then** a success/error feedback animation plays inside the glass card

---

### REQ-P8: NotFound — Centered 404 with Animation
**Priority**: P1
**Status**: proposed

The NotFound page MUST display a centered 404 with large typography, an animated illustration or icon, and a link back to the home page.

> **Scenario: Large 404 typography**
> **Given** the user lands on an unknown route
> **When** the page renders
> **Then** a large "404" heading and descriptive message are visible

> **Scenario: Animated illustration and home link**
> **Given** the NotFound page is rendered
> **When** the animation completes
> **Then** an illustration or icon animates in (600ms hero duration) and a "Go Home" link is visible

---

### REQ-P9: Theme Respect on All Pages
**Priority**: P0
**Status**: proposed

Every page MUST respect the dark/light theme preference set by the user toggle and `data-theme` attribute on `<html>`.

> **Scenario: All pages render in dark theme**
> **Given** `data-theme="dark"` on `<html>`
> **When** any public page renders (Home, EventList, EventDetail, Checkout, CheckoutReturn, Login, TicketLookup, NotFound)
> **Then** backgrounds, surfaces, and text use dark-palette tokens

> **Scenario: All pages render in light theme**
> **Given** `data-theme="light"` on `<html>`
> **When** any public page renders
> **Then** backgrounds, surfaces, and text use light-palette tokens

---

### REQ-P10: Scroll Reveal Animations
**Priority**: P2
**Status**: proposed

Content sections below the fold on scrollable pages SHOULD use staggered scroll-reveal animations (children appear in sequence as they enter the viewport).

> **Scenario: Sections reveal on scroll**
> **Given** the EventList or EventDetail page has content below the fold
> **When** the user scrolls down and a section enters the viewport
> **Then** that section fades/slides in while adjacent sections remain hidden

> **Scenario: Reduced motion disables scroll reveals**
> **Given** `prefers-reduced-motion: reduce` is active
> **When** the user scrolls
> **Then** all content is immediately visible — no scroll-triggered animations play
