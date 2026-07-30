## Exploration: Ticket Lookup Visual Spacing Redesign

### Current State

The `/tickets/lookup` page renders correctly but has a visual quality problem: all text, inputs, and buttons touch the edges of their `GlassCard` containers. The `glass-surface` CSS utility (defined in `frontend/src/tokens.css:326`) provides backdrop blur, background color, border, and border-radius — but **zero padding**. The `GlassCard` component (`frontend/src/components/ui/GlassCard.jsx`) is a thin wrapper that passes through `className` without adding any default padding. Every consumer is responsible for adding its own padding.

**Five GlassCard instances in TicketLookup.jsx lack padding:**

| Line | Context | Current className | Problem |
|------|---------|-------------------|---------|
| 35 | TicketCard | `"relative"` | Content text and badge touch edges. Has `pr-20` on h3 for badge spacing, but no outer padding. |
| 80 | TicketCardSkeleton | _(none)_ | Skeleton blocks touch card edges on all sides. |
| 215 | Lookup form | _(none)_ | Email input and button touch card borders. |
| 264 | Error state | `"text-center py-6"` | Has vertical padding, missing horizontal — text touches left/right edges. |
| 322 | Resend section | _(none)_ | Header, form, checkbox, and button all touch card borders. |

The outer page container (line 201) IS responsive: `max-w-3xl mx-auto px-4 sm:px-6 py-8 space-y-10`. The problem is entirely inside the GlassCards.

### Affected Areas

- `frontend/src/pages/TicketLookup.jsx` — lines 35, 80, 215, 264, 322 (5 GlassCard instances)
- `frontend/src/components/ui/GlassCard.jsx` — component definition (zero default padding)
- `frontend/src/tokens.css:326-332` — `glass-surface` utility (zero padding)
- `frontend/src/pages/TicketLookup.test.jsx` — tests reference `.glass-surface` elements, may need to verify padding classes
- `frontend/src/pages/Checkout.jsx` — lines 182, 221, 345 have the same antipattern (bare GlassCards in form/confirmation contexts)

### Approaches

#### 1. Page-level fix — Add explicit padding classes to every GlassCard in TicketLookup.jsx

Change each bare GlassCard to include responsive padding matching the project's best patterns (`p-4 md:p-6` for content cards, `text-center px-4 py-6 md:px-6` for error states).

```diff
- Line  35: <GlassCard className="relative">
+ Line  35: <GlassCard className="relative p-4 md:p-6">

- Line  80: <GlassCard>
+ Line  80: <GlassCard className="p-4 md:p-6">

- Line 215: <GlassCard>
+ Line 215: <GlassCard className="p-4 md:p-6">

- Line 264: <GlassCard className="text-center py-6">
+ Line 264: <GlassCard className="text-center px-4 py-6 md:px-6">

- Line 322: <GlassCard>
+ Line 322: <GlassCard className="p-4 md:p-6">
```

- **Pros**: Minimal risk (only TicketLookup changes), fast (~5 lines changed), no component API changes, tests unaffected.
- **Cons**: Only fixes TicketLookup — Checkout.jsx and any future pages still need manual padding. Reactive fix, not systemic.
- **Effort**: Low (~15 minutes)

#### 2. Component-level fix — Add default padding to GlassCard with override support

Add a `padding` prop to GlassCard (mirroring the `Card` component's pattern) with default `md` = `p-4 md:p-6`. All existing bare GlassCards would automatically get padding. Cards that intentionally use zero padding (EventList card grid, OrganizerDashboard table) would pass `padding="none"`. Cards with their own `p-*` in className would override cleanly because their classes appear AFTER the default in the class string.

```jsx
export default function GlassCard({ children, className = '', as: Component = 'div', padding = 'md', ...rest }) {
  const paddingClasses = { none: '', sm: 'p-4', md: 'p-4 md:p-6', lg: 'p-6 md:p-8' };
  return (
    <Component className={`glass-surface ${paddingClasses[padding]} ${className}`} {...rest}>
      {children}
    </Component>
  )
}
```

Affected cards that need review:
- EventList cards: `<GlassCard className="overflow-hidden p-0 h-full...">` → className `p-0` overrides default → no change needed
- OrganizerDashboard table: `<GlassCard className="p-0 sm:p-0 overflow-hidden">` → same → no change
- CheckoutReturn: `<GlassCard className="text-center py-10">` → gets `p-4 md:p-6` from default + `py-10` → result: px-4/md:px-6 (horizontal from default) + py-10 (from className). Slightly wider padding on desktop. Acceptable, or use `padding="sm"`.
- Login: `<GlassCard className="p-8">` → className p-8 fully overrides default. No change.
- Checkout confirmation: `<GlassCard className="text-center">` → currently zero padding, would get `p-4 md:p-6`. Improves layout but should be tested.

- **Pros**: Systemic fix — prevents the bug in future pages. Consistent with `Card` component API. Zero config for most pages — they just get padding.
- **Cons**: Higher risk — affects ALL 27 GlassCard usages across 14 pages. Requires audit and at minimum a visual regression pass. Existing tests that rely on exact DOM structure may break.
- **Effort**: Medium (~2 hours + visual regression pass)

#### 3. Hybrid — Page-level fix now, component-level as a fast-follow SDD change

Apply Approach 1 to TicketLookup.jsx immediately (quick win). File Approach 2 as a separate SDD change `glasscard-default-padding` that addresses the root cause across the whole frontend, including the same issue in Checkout.jsx, EventDetail.jsx, and other pages.

- **Pros**: Gets TicketLookup fixed NOW without risk. The systemic fix gets proper SDD treatment (spec, design, tasks, verify). Separates concerns — urgent bug fix vs. design system improvement.
- **Cons**: Two changes instead of one. The GlassCard antipattern persists in other pages until the follow-up ships.
- **Effort**: Low for TicketLookup fix (15 min), Medium for the follow-up change.

### Recommendation

**Approach 1 — Page-level fix.** The user's immediate pain point is TicketLookup. This is the safest, fastest path to a visually clean page. The changes are 5 className additions — no component API changes, no test breakage, no risk of breaking other pages.

The long-term fix (default GlassCard padding) should be tracked as a separate SDD change that addresses the design system inconsistency. The `Card.jsx` component already demonstrates the right API pattern (`padding` prop with `none/sm/md/lg`), and GlassCard should follow suit.

Concrete changes to `TicketLookup.jsx`:
1. TicketCard (line 35): add `p-4 md:p-6` — provides breathing room around ticket info content
2. TicketCardSkeleton (line 80): add `p-4 md:p-6` — matches ticket card padding for consistent skeleton-to-content transition
3. Lookup form (line 215): add `p-4 md:p-6` — space around the email input and button
4. Error state (line 264): change `py-6` to `px-4 py-6 md:px-6` — fix missing horizontal padding
5. Resend section (line 322): add `p-4 md:p-6` — space around header, form, checkbox, and button

These values match the project's established patterns: EventDetail ticket types (`p-4 md:p-6` line 360), Login (`p-8`), AdminPanel (`p-6`), OrganizerEventMetrics (`p-8`).

### Risks

- **None** — This is a pure CSS className change. No logic, state, API, or test behavior is affected. The page already works correctly functionally; this only improves visual spacing.
- The one consideration: `pr-20` on the ticket card's h3 (line 42) reserves space for the absolutely-positioned Badge. With `p-4 md:p-6` now on the outer GlassCard, the Badge position (`top-3 right-3`) may need to shift to `top-7 right-7` or similar to stay visually balanced. **This should be verified visually** — the absolute positioning is relative to the GlassCard container, and with padding added, the badge will sit further inside, which may actually look better.

### Ready for Proposal

**Yes.** The exploration is clear and the fix is straightforward. Proceed to `sdd-propose` for `ticket-lookup-redesign` with Approach 1 (page-level padding fix). Optionally create a separate `glasscard-default-padding` proposal for the systemic design-system improvement.
