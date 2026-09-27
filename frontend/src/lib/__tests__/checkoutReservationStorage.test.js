import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import {
  CHECKOUT_RESERVATION_KEY,
  buildCartSignature,
  loadCheckoutReservation,
  saveCheckoutReservation,
  updateCheckoutReservation,
  clearCheckoutReservation,
} from '../checkoutReservationStorage.js'

const cart = { eventId: 'event-1', ticketTypeId: 'tt-1', quantity: 2 }

function buildEntry(overrides = {}) {
  return {
    signature: buildCartSignature(cart),
    id: 'reservation-1',
    token: 'reservation-token-1',
    expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
    quantity: 2,
    purchaserName: 'Juan Perez',
    purchaserEmail: 'juan@example.com',
    purchaserDNI: '12345678',
    documentCountry: 'AR',
    ...overrides,
  }
}

describe('checkoutReservationStorage', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('builds the signature from event, ticket type and quantity', () => {
    expect(buildCartSignature(cart)).toBe('event-1|tt-1|2')
  })

  it('saves and loads an entry with a matching cart signature', () => {
    const entry = buildEntry()
    saveCheckoutReservation(entry)

    expect(loadCheckoutReservation(cart)).toEqual(entry)
  })

  it('returns null when nothing was stored', () => {
    expect(loadCheckoutReservation(cart)).toBeNull()
  })

  it('removes and ignores an entry whose signature does not match the cart', () => {
    saveCheckoutReservation(
      buildEntry({
        signature: buildCartSignature({ ...cart, quantity: 5 }),
      })
    )

    expect(loadCheckoutReservation(cart)).toBeNull()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('removes and ignores an entry whose signature differs only by event', () => {
    saveCheckoutReservation(
      buildEntry({
        signature: buildCartSignature({ ...cart, eventId: 'event-2' }),
      })
    )

    expect(loadCheckoutReservation(cart)).toBeNull()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('removes and ignores an expired entry', () => {
    saveCheckoutReservation(
      buildEntry({ expiresAt: new Date(Date.now() - 1000).toISOString() })
    )

    expect(loadCheckoutReservation(cart)).toBeNull()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('removes and ignores an entry with an unparseable expiresAt', () => {
    saveCheckoutReservation(buildEntry({ expiresAt: 'not-a-date' }))

    expect(loadCheckoutReservation(cart)).toBeNull()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('removes and ignores a corrupt JSON entry', () => {
    sessionStorage.setItem(CHECKOUT_RESERVATION_KEY, '{not-json')

    expect(loadCheckoutReservation(cart)).toBeNull()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('removes and ignores a JSON primitive entry', () => {
    sessionStorage.setItem(CHECKOUT_RESERVATION_KEY, '"just-a-string"')

    expect(loadCheckoutReservation(cart)).toBeNull()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('degrades silently when sessionStorage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError')
    })
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('SecurityError')
    })

    expect(loadCheckoutReservation(cart)).toBeNull()
    expect(() => saveCheckoutReservation(buildEntry())).not.toThrow()
    expect(() => clearCheckoutReservation()).not.toThrow()
  })

  it('clears the stored entry', () => {
    saveCheckoutReservation(buildEntry())
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).not.toBeNull()

    clearCheckoutReservation()

    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('merges a patch into the stored entry without dropping existing fields', () => {
    const entry = buildEntry()
    saveCheckoutReservation(entry)

    updateCheckoutReservation({ preferenceId: 'pref-123' })

    expect(loadCheckoutReservation(cart)).toEqual({
      ...entry,
      preferenceId: 'pref-123',
    })
  })

  it('is a silent no-op when there is no stored entry', () => {
    expect(() => updateCheckoutReservation({ preferenceId: 'pref-123' })).not.toThrow()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('is a silent no-op when the stored entry is corrupt JSON', () => {
    sessionStorage.setItem(CHECKOUT_RESERVATION_KEY, '{not-json')

    expect(() => updateCheckoutReservation({ preferenceId: 'pref-123' })).not.toThrow()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBe('{not-json')
  })

  it('survives storage failures while updating', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError')
    })
    expect(() => updateCheckoutReservation({ preferenceId: 'pref-123' })).not.toThrow()

    vi.restoreAllMocks()

    saveCheckoutReservation(buildEntry())
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError')
    })
    expect(() => updateCheckoutReservation({ preferenceId: 'pref-123' })).not.toThrow()
  })
})
