/**
 * Service fee ("cargo por servicio") over a ticket subtotal.
 *
 * Mirrors backend `ServiceFeeOptions.Calculate` exactly: subtotal × percentage,
 * rounded to cents with midpoint away from zero. The backend is the authority
 * (it is what Mercado Pago charges); this copy only lets the buyer see the
 * same breakdown before reserving.
 */
export function calculateServiceFee(subtotal, percentage) {
  if (!subtotal || !percentage) return 0
  // Fee in cents = (subtotal × percentage / 100) × 100 = subtotal × percentage.
  // The tiny epsilon compensates binary float error so an exact .5 rounds up.
  const cents = Math.round(subtotal * percentage * (1 + Number.EPSILON))
  return cents / 100
}
