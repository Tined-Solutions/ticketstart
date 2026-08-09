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
})
