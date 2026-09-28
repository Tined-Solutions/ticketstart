// Purchaser names accept Unicode letters, combining marks (decomposed accents)
// and plain spaces only: no digits, symbols, emoji, hyphens or apostrophes.
const INVALID_NAME_CHARS = /[^\p{L}\p{M} ]/gu
const VALID_NAME = /^[\p{L}\p{M} ]+$/u

/**
 * Strips every character that is not a Unicode letter, a combining mark or a
 * plain space. Designed for controlled inputs so invalid characters are
 * non-enterable (keeps accents, ñ, ü and decomposed combining marks).
 */
export function sanitizeName(raw) {
  if (typeof raw !== 'string') return ''
  return raw.replace(INVALID_NAME_CHARS, '')
}

/**
 * Validates a purchaser name: the trimmed value must be non-empty and contain
 * only Unicode letters, combining marks and plain spaces.
 *
 * @param {string} raw - Raw name value
 * @returns {boolean}
 */
export function isValidName(raw) {
  if (typeof raw !== 'string') return false
  const trimmed = raw.trim()
  return trimmed.length > 0 && VALID_NAME.test(trimmed)
}
