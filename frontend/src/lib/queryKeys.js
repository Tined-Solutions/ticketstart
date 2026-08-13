/**
 * Centralized TanStack Query keys.
 *
 * Invalidation convention: `['events']` covers the catalog list,
 * `['event', id]` covers a single event detail. Prefix invalidation with
 * `['event']` clears every event detail query at once (used when the
 * affected event id is not known, e.g. after payment confirmation).
 */
export const queryKeys = {
  events: ['events'],
  event: (id) => ['event', id],
  // Management variants hit the role-gated /manage endpoints, which include
  // expired events. Separate keys so the public (filtered) catalog and the
  // organizer/staff views never share a cache entry.
  managementEvents: ['management-events'],
  managementEvent: (id) => ['management-event', id],
}
