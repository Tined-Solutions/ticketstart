import { describe, it, expect } from 'vitest'
import { statusBadgeVariant, statusLabel } from '../eventStatus.js'

describe('eventStatus utilities', () => {
  describe('statusBadgeVariant', () => {
    it('maps pending to warning', () => {
      expect(statusBadgeVariant('Pending')).toBe('warning')
    })

    it('maps approved to success', () => {
      expect(statusBadgeVariant('Approved')).toBe('success')
    })

    it('maps rejected to error', () => {
      expect(statusBadgeVariant('Rejected')).toBe('error')
    })

    it('falls back to info for unknown or empty status', () => {
      expect(statusBadgeVariant('Unknown')).toBe('info')
      expect(statusBadgeVariant(undefined)).toBe('info')
      expect(statusBadgeVariant('')).toBe('info')
    })
  })

  describe('statusLabel', () => {
    it('returns Spanish labels for the three statuses', () => {
      expect(statusLabel('Pending')).toBe('Pendiente')
      expect(statusLabel('Approved')).toBe('Aprobado')
      expect(statusLabel('Rejected')).toBe('Rechazado')
    })

    it('returns the raw value for unknown status', () => {
      expect(statusLabel('Weird')).toBe('Weird')
      expect(statusLabel(undefined)).toBe('')
    })
  })
})
