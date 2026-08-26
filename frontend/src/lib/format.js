/**
 * Formats an ISO date string into a human-readable localized date string.
 * Returns a fallback message for null, undefined, or invalid date strings.
 *
 * @param {string|null|undefined} dateString
 * @returns {string}
 */
export function formatEventDate(dateString) {
  if (!dateString) return 'Fecha por confirmar'
  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return 'Fecha no valida'
  return date.toLocaleDateString('es-AR', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    // es-AR defaults to h12 (e.g. "05:00 p. m."); force 24h for consistency
    // across the app (StaffScan and email templates already use 24h).
    hour12: false,
  })
}

/**
 * Formats a numeric amount as an ARS currency string (whole pesos — ARS/UYU
 * convention does not show centavos).
 * Returns "$ --" for null or undefined values.
 *
 * @param {number|null|undefined} amount
 * @returns {string}
 */
export function formatCurrency(amount) {
  if (amount === undefined || amount === null) return '$ --'
  return `$ ${Number(amount).toLocaleString('es-AR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  })}`
}
