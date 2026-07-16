# Spec: app-shell-layout

## Purpose

Layout shell with `Navbar`, `Footer`, route transitions (`AnimatePresence`), auth-aware navigation state, and mobile responsiveness. Wraps all routes to provide consistent chrome.

## Requirements

### REQ-L1: Layout Component Wraps All Routes
**Priority**: P0
**Status**: proposed

The `Layout` component MUST wrap all route content with Navbar + `<main>` + Footer structure. Every page MUST render inside this shell.

> **Scenario: Layout renders chrome around page content**
> **Given** the user navigates to any route
> **When** the page renders
> **Then** Navbar is at the top, page content is in `<main>`, Footer is at the bottom

---

### REQ-L2: Navbar Content and Structure
**Priority**: P0
**Status**: proposed

The `Navbar` MUST render the "Ticketera" logo/brand, navigation links (Events, My Tickets), an auth-aware section (Login link or user menu), and the theme toggle.

> **Scenario: Navbar shows guest navigation**
> **Given** the user is not authenticated
> **When** the Navbar renders
> **Then** links for "Events" and "My Tickets" are visible, and a "Login" link/button is present

> **Scenario: Navbar shows theme toggle**
> **Given** any auth state
> **When** the Navbar renders
> **Then** the theme toggle icon/button is visible and clickable

---

### REQ-L3: Glass Morphism Navbar
**Priority**: P1
**Status**: proposed

The `Navbar` MUST use glass morphism styling: `backdrop-blur` with a semi-transparent background that lets page content show through.

> **Scenario: Navbar has glass effect**
> **Given** the Navbar is rendered
> **When** content scrolls behind it
> **Then** the background behind the Navbar is blurred and the Navbar background is semi-transparent

---

### REQ-L4: Auth-Aware Navigation
**Priority**: P0
**Status**: proposed

The `Navbar` MUST be auth-aware: show a user menu (avatar/name + logout) when authenticated; show a "Login" link when not.

> **Scenario: Authenticated user sees user menu**
> **Given** the user is authenticated with a valid session
> **When** the Navbar renders
> **Then** user avatar/name and a logout option are visible; "Login" link is hidden

> **Scenario: Unauthenticated user sees login link**
> **Given** no auth token is present
> **When** the Navbar renders
> **Then** the "Login" link is visible; user menu is hidden

---

### REQ-L5: Footer with Branding and Links
**Priority**: P1
**Status**: proposed

The `Footer` MUST display copyright information, relevant links, and branding consistent with the current theme.

> **Scenario: Footer renders at page bottom**
> **Given** any page with content shorter than the viewport
> **When** the page renders
> **Then** the Footer is pushed to the bottom of the viewport

> **Scenario: Footer respects theme**
> **Given** the user toggles between dark and light theme
> **When** the Footer renders
> **Then** colors and typography match the active theme tokens

---

### REQ-L6: Route Transitions via AnimatePresence
**Priority**: P1
**Status**: proposed

`AnimatePresence` MUST wrap route changes with a fade/slide transition (400ms duration). The current route component MUST animate out before the next route animates in.

> **Scenario: Page transition on route change**
> **Given** the user navigates from `/events` to `/events/123`
> **When** the route changes
> **Then** the outgoing page fades/slides out (~400ms) and the incoming page fades/slides in

> **Scenario: No transition when reduced motion is active**
> **Given** `prefers-reduced-motion: reduce` is active
> **When** the user navigates between routes
> **Then** the page swap is instant with no animation

---

### REQ-L7: Scroll-Aware Navbar Elevation
**Priority**: P2
**Status**: proposed

The `Navbar` MUST add shadow/elevation when the page is scrolled beyond the top (scrollY > 0) and remove it when scrolled back to top.

> **Scenario: Shadow appears on scroll**
> **Given** the page is scrolled down > 0px
> **When** the scroll position changes
> **Then** the Navbar gains a subtle shadow/elevation style

> **Scenario: Shadow removed at top**
> **Given** the page is scrolled back to the very top
> **When** the scroll position reaches 0
> **Then** the Navbar shadow/elevation is removed

---

### REQ-L8: Mobile Responsive Navbar
**Priority**: P1
**Status**: proposed

At viewport widths below 768px, the `Navbar` MUST collapse navigation links into a hamburger menu or equivalent mobile pattern.

> **Scenario: Hamburger menu on mobile**
> **Given** the viewport width is < 768px
> **When** the Navbar renders
> **Then** nav links are hidden behind a hamburger toggle; brand and theme toggle remain visible

> **Scenario: Mobile menu expands on tap**
> **Given** the viewport width is < 768px and the hamburger icon is visible
> **When** the user taps the hamburger icon
> **Then** nav links expand in a dropdown or drawer with smooth animation
