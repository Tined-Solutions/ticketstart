import { describe, it, expect } from 'vitest'
import { calculateServiceFee } from '../serviceFee.js'

// Mirrors backend ServiceFeeOptions.Calculate: subtotal × percentage, rounded
// to cents with midpoint away from zero. Both sides must agree so the total
// shown before paying equals what Mercado Pago charges.
describe('calculateServiceFee', () => {
  it.each([
    [100, 10, 10],
    [45000, 10, 4500],
    [1.5, 10, 0.15],
    [33.33, 10, 3.33],
    [0.05, 10, 0.01],
    [100, 0, 0],
    [0, 10, 0],
  ])('fee(%s, %s%%) = %s', (subtotal, percentage, expected) => {
    expect(calculateServiceFee(subtotal, percentage)).toBe(expected)
  })
})
