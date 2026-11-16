/**
 * Ticket prices are natural numbers: whole pesos, at least 1. No decimals —
 * what the buyer sees, what Mercado Pago charges and what is stored must be
 * the same number — and no zero (there is no free-ticket flow).
 *
 * Mirrors backend `TicketPriceRules`; the backend is the authority, this copy
 * only gives the organizer/admin instant feedback. Shared by every form that
 * sets a price (EventForm, AddTicketsModal, EditTicketsModal).
 */

/** Spanish validation message for a price value, or null when valid. */
export function getTicketPriceError(value) {
  const raw = String(value ?? '').trim()
  if (raw === '') return 'El precio es obligatorio'

  // "1,5" is a decimal for an es-AR user even though Number() rejects it.
  if (/^-?\d+[.,]\d*$/.test(raw)) return 'El precio debe ser un número entero, sin decimales'

  const price = Number(raw)
  if (Number.isNaN(price)) return 'El precio es obligatorio'
  if (!Number.isInteger(price)) return 'El precio debe ser un número entero, sin decimales'
  if (price < 1) return 'El precio debe ser mayor a 0'
  return null
}

/** Input attributes for a whole-peso price field (spread onto the <input>). */
export const ticketPriceInputProps = {
  type: 'number',
  min: '1',
  step: '1',
  inputMode: 'numeric',
  onKeyDown: blockNonIntegerKeys,
}

/**
 * Keydown guard for number inputs: drops the characters that would turn the
 * value into a decimal, exponent or signed number. Pasted values are still
 * caught by getTicketPriceError.
 */
export function blockNonIntegerKeys(event) {
  if (['.', ',', 'e', 'E', '-', '+'].includes(event.key)) event.preventDefault()
}
