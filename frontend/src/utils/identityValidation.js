const CI_FACTORS = [2, 9, 8, 7, 6, 3, 4]

/**
 * Strips every non-digit character from a raw document string.
 * Preserves the original for display while working with the clean value.
 */
export function cleanDocument(raw) {
  if (typeof raw !== 'string') return ''
  return raw.replace(/\D/g, '')
}

/**
 * Formats a clean numeric string for display.
 * Argentina: XX.XXX.XXX  (7-8 digits)
 * Uruguay:   X.XXX.XXX-X (always 8 digits with check digit separated)
 */
export function formatDocument(clean, country) {
  if (!clean) return ''
  const digits = clean

  if (country === 'UY') {
    // Pad to 8 if needed, then format as X.XXX.XXX-X
    const padded = digits.padStart(8, '0')
    return `${padded[0]}.${padded.slice(1, 4)}.${padded.slice(4, 7)}-${padded[7]}`
  }

  // Argentina: simple dot grouping
  if (digits.length <= 3) return digits
  if (digits.length <= 6) {
    // 4-6 digits: X.XXX or XX.XXX or XXX.XXX
    const splitPoint = digits.length - 3
    return `${digits.slice(0, splitPoint)}.${digits.slice(splitPoint)}`
  }
  // 7-8 digits: X.XXX.XXX or XX.XXX.XXX
  const firstPart = digits.length === 7 ? 1 : 2
  return `${digits.slice(0, firstPart)}.${digits.slice(firstPart, firstPart + 3)}.${digits.slice(firstPart + 3)}`
}

/**
 * Validates a Uruguayan cédula de identidad using the official check-digit algorithm.
 *
 * Algorithm:
 *   1. Pad to 8 digits (prepend '0' if 7).
 *   2. Multiply the first 7 digits by factors [2, 9, 8, 7, 6, 3, 4].
 *   3. Sum the products.
 *   4. Find the next multiple of 10 above the sum.
 *   5. Subtract sum from that multiple → computed check digit.
 *   6. If computed === 10, check digit is 0.
 *   7. Compare computed check digit with the 8th digit.
 */
function validateCedulaUruguaya(clean) {
  // Must be 7 or 8 digits after cleanup
  if (!/^\d{7,8}$/.test(clean)) {
    return { valid: false, error: 'Cédula uruguaya inválida' }
  }

  const padded = clean.padStart(8, '0')
  const digits = padded.split('').map(Number)

  let sum = 0
  for (let i = 0; i < 7; i++) {
    sum += digits[i] * CI_FACTORS[i]
  }

  const nextMultiple = Math.ceil(sum / 10) * 10
  let computed = nextMultiple - sum
  if (computed === 10) computed = 0

  if (computed !== digits[7]) {
    return { valid: false, error: 'Cédula uruguaya inválida' }
  }

  return { valid: true, error: null }
}

/**
 * Validates an Argentine DNI.
 * Must be exactly 7 or 8 digits. No check digit.
 */
function validateDNIArgentino(clean) {
  if (!/^\d{7,8}$/.test(clean)) {
    return { valid: false, error: 'Formato de DNI inválido' }
  }
  return { valid: true, error: null }
}

/**
 * Main validation entry point.
 *
 * @param {string} raw - Raw user input (may include dots, spaces, hyphens)
 * @param {'AR' | 'UY'} country
 * @returns {{ valid: boolean, clean: string, formatted: string, error: string | null }}
 */
export function validateDocument(raw, country) {
  const clean = cleanDocument(raw)

  if (!clean) {
    return { valid: false, clean: '', formatted: '', error: 'El documento es obligatorio' }
  }

  let result
  if (country === 'UY') {
    result = validateCedulaUruguaya(clean)
  } else {
    result = validateDNIArgentino(clean)
  }

  return {
    ...result,
    clean,
    formatted: result.valid ? formatDocument(clean, country) : clean,
  }
}
