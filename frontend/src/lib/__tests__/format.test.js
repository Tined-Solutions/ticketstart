import { describe, it, expect } from 'vitest'
import { formatEventDate, formatCurrency } from '../format.js'

describe('formatEventDate', () => {
  it('returns a localized date string for a valid ISO date', () => {
    const result = formatEventDate('2026-12-25T20:00:00Z')
    expect(result).toBeTruthy()
    expect(typeof result).toBe('string')
    // Should contain at least day, month, and year components
    expect(result).toMatch(/25/)
    expect(result).toMatch(/diciembre/i)
    // Time must be 24h (es-AR defaults to h12; formatEventDate forces 24h).
    // Computed dynamically so the assertion is timezone-robust.
    const expectedTime = new Date('2026-12-25T20:00:00Z').toLocaleTimeString('es-AR', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    })
    expect(result).toContain(expectedTime)
    expect(result).not.toMatch(/a\. m\.|p\. m\./i)
  })

  it('returns the expected fallback for null', () => {
    const result = formatEventDate(null)
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
    // Should NOT be a raw date string or empty
    expect(result).not.toBe('Invalid Date')
    expect(result).not.toBe('')
  })

  it('returns the expected fallback for undefined', () => {
    const result = formatEventDate(undefined)
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
    expect(result).not.toBe('Invalid Date')
    expect(result).not.toBe('')
  })

  it('returns the expected fallback for an invalid date string', () => {
    const result = formatEventDate('not-a-valid-date')
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
    expect(result).not.toBe('Invalid Date')
    expect(result).not.toBe('')
  })

  it('returns the expected fallback for an empty string', () => {
    const result = formatEventDate('')
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
    expect(result).not.toBe('Invalid Date')
    expect(result).not.toBe('')
  })
})

describe('formatCurrency', () => {
  it('formats a positive integer as ARS currency', () => {
    const result = formatCurrency(15000)
    expect(typeof result).toBe('string')
    expect(result).toContain('$')
    expect(result).toContain('15.000')
  })

  it('rounds decimal amounts to whole pesos (no centavos)', () => {
    const result = formatCurrency(99.5)
    expect(result).toContain('100')
  })

  it('formats zero correctly', () => {
    const result = formatCurrency(0)
    expect(typeof result).toBe('string')
    expect(result).toContain('$')
    expect(result).toContain('0')
  })

  it('returns "$ --" for null', () => {
    const result = formatCurrency(null)
    expect(result).toBe('$ --')
  })

  it('returns "$ --" for undefined', () => {
    const result = formatCurrency(undefined)
    expect(result).toBe('$ --')
  })

  it('formats a large number with thousands separators', () => {
    const result = formatCurrency(1234567)
    expect(result).toContain('1.234.567')
  })

  it('defaults to whole pesos when no options are passed', () => {
    expect(formatCurrency(300)).toBe('$ 300')
    expect(formatCurrency(300.4)).toBe('$ 300')
    expect(formatCurrency(300.6)).toBe('$ 301')
  })

  it('shows cents with fractionDigits: 2 (es-AR comma separator)', () => {
    expect(formatCurrency(300.5, { fractionDigits: 2 })).toBe('$ 300,50')
    expect(formatCurrency(50, { fractionDigits: 2 })).toBe('$ 50,00')
  })

  it('keeps thousands separators with fractionDigits: 2', () => {
    expect(formatCurrency(1234.56, { fractionDigits: 2 })).toBe('$ 1.234,56')
  })

  it('returns "$ --" for null/undefined regardless of fractionDigits', () => {
    expect(formatCurrency(null, { fractionDigits: 2 })).toBe('$ --')
    expect(formatCurrency(undefined, { fractionDigits: 2 })).toBe('$ --')
  })
})
