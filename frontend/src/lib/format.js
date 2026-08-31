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
 * Converts an ISO instant string into the value for an
 * <input type="datetime-local">: the wall clock in the USER'S local timezone
 * ("YYYY-MM-DDTHH:mm"), matching how datetime-local values are parsed back on
 * submit (local time).
 *
 * NEVER build this value from toISOString(): that yields the UTC wall clock,
 * and prefilling a datetime-local input with it shifts the stored instant by
 * the timezone offset on every save (es-AR users: +3h per edit, with spurious
 * date-change emails to all buyers). Regression suite:
 * lib/__tests__/format.datetime.tz.test.js (pinned to America/Argentina/Buenos_Aires).
 *
 * @param {string|null|undefined} dateString ISO instant from the API
 * @returns {string} "YYYY-MM-DDTHH:mm" in local time; '' for empty/invalid
 */
export function toDateTimeLocalValue(dateString) {
  if (!dateString) return ''
  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return ''
  const pad = (n) => String(n).padStart(2, '0')
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  )
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
