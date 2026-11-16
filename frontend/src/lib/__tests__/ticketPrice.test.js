import { describe, it, expect, vi } from 'vitest'
import { getTicketPriceError, blockNonIntegerKeys } from '../ticketPrice.js'

// Ticket prices are natural numbers (whole pesos, >= 1). Mirrors backend
// TicketPriceRules so the form never accepts what the API rejects.
describe('getTicketPriceError', () => {
  it.each([['1'], ['1500'], [1500], ['  250  ']])('accepts the whole price %j', (value) => {
    expect(getTicketPriceError(value)).toBeNull()
  })

  it.each([[''], ['   '], [null], [undefined], ['abc']])('requires a price (%j)', (value) => {
    expect(getTicketPriceError(value)).toBe('El precio es obligatorio')
  })

  it.each([['1.5'], ['1500.50'], [0.5], ['1,5']])('rejects decimals (%j)', (value) => {
    expect(getTicketPriceError(value)).toBe('El precio debe ser un número entero, sin decimales')
  })

  it.each([['0'], ['-5'], [-1]])('rejects zero and negatives (%j)', (value) => {
    expect(getTicketPriceError(value)).toBe('El precio debe ser mayor a 0')
  })
})

describe('blockNonIntegerKeys', () => {
  it.each(['.', ',', 'e', 'E', '-', '+'])('blocks "%s"', (key) => {
    const event = { key, preventDefault: vi.fn() }
    blockNonIntegerKeys(event)
    expect(event.preventDefault).toHaveBeenCalled()
  })

  it.each(['1', '0', 'Backspace', 'Tab', 'ArrowLeft'])('lets "%s" through', (key) => {
    const event = { key, preventDefault: vi.fn() }
    blockNonIntegerKeys(event)
    expect(event.preventDefault).not.toHaveBeenCalled()
  })
})
