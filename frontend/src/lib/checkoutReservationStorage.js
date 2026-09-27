/**
 * Persists the in-progress checkout reservation for the lifetime of the tab.
 *
 * The cart travels through history navigation (`location.state`), but the
 * reservation created from it only lived in React state: going Back from
 * phase 2 and Forward again remounted the page with an empty form, and
 * resubmitting created a second reservation (double hold). Storing the hold
 * in sessionStorage lets the page restore phase 2 with the countdown
 * continuing from the original `expiresAt`.
 *
 * Entries are keyed by a cart signature (event + ticket type + quantity) so a
 * deliberately different purchase never resurrects a stale reservation. Every
 * storage access degrades silently to null/no-op when sessionStorage is
 * unavailable (private mode, quota).
 *
 * An entry may additionally carry an optional `preferenceId` — the Mercado
 * Pago preference created for the hold once the buyer starts paying — so the
 * page can re-verify the payment after returning from the checkout.
 */

export const CHECKOUT_RESERVATION_KEY = 'ticketstart.checkout.reservation'

/**
 * Identity of the cart the reservation belongs to. Two purchases of the same
 * event/ticket type/quantity are indistinguishable on purpose: the stored hold
 * is only meaningful for the exact cart that created it.
 */
export function buildCartSignature({ eventId, ticketTypeId, quantity }) {
  return `${eventId}|${ticketTypeId}|${quantity}`
}

function isFutureTimestamp(value) {
  const time = new Date(value).getTime()
  return Number.isFinite(time) && time > Date.now()
}

function removeEntry() {
  try {
    sessionStorage.removeItem(CHECKOUT_RESERVATION_KEY)
  } catch {
    // Storage unavailable → nothing to clear.
  }
}

/**
 * Returns the stored reservation only when it belongs to this cart and its
 * hold is still active. A corrupt, mismatched or expired entry is discarded
 * (removed) and treated as absent.
 */
export function loadCheckoutReservation({ eventId, ticketTypeId, quantity }) {
  let raw
  try {
    raw = sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)
  } catch {
    return null
  }
  if (!raw) return null

  let entry
  try {
    entry = JSON.parse(raw)
  } catch {
    removeEntry()
    return null
  }

  const signature = buildCartSignature({ eventId, ticketTypeId, quantity })
  const matchesCart =
    entry && typeof entry === 'object' && entry.signature === signature
  if (!matchesCart || !isFutureTimestamp(entry.expiresAt)) {
    removeEntry()
    return null
  }

  return entry
}

/** Upserts the stored reservation entry. */
export function saveCheckoutReservation(entry) {
  try {
    sessionStorage.setItem(CHECKOUT_RESERVATION_KEY, JSON.stringify(entry))
  } catch {
    // Storage unavailable → the reservation simply isn't persisted.
  }
}

/**
 * Merges a partial update into the stored reservation and saves it back
 * (e.g. `{ preferenceId }` after creating the Mercado Pago preference).
 * Silent no-op when there is no entry, the entry is corrupt, or storage fails.
 */
export function updateCheckoutReservation(patch) {
  let raw
  try {
    raw = sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)
  } catch {
    return
  }
  if (!raw) return

  let entry
  try {
    entry = JSON.parse(raw)
  } catch {
    return
  }
  if (!entry || typeof entry !== 'object') return

  try {
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({ ...entry, ...patch })
    )
  } catch {
    // Storage unavailable → the update simply isn't persisted.
  }
}

/** Removes the stored reservation, if any. */
export function clearCheckoutReservation() {
  removeEntry()
}
