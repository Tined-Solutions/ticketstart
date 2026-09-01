import { describe, it, expect } from 'vitest'
import { toDateTimeLocalValue } from '../format.js'

// This regression suite must run in a NON-UTC timezone: the original bug
// (datetime-local prefill built from toISOString, i.e. UTC wall clock) is
// invisible when local time == UTC. Pin the process to Argentina before any
// Date is constructed in this worker process.
process.env.TZ = 'America/Argentina/Buenos_Aires'

// Guard: if the runtime ignored the TZ switch (unsupported pool config),
// skip instead of failing — these assertions are only meaningful at UTC-3.
const probe = new Date('2026-08-15T21:00:00Z')
const tzHonored = probe.getHours() === 18 && probe.getDate() === 15

describe.skipIf(!tzHonored)('toDateTimeLocalValue (America/Argentina/Buenos_Aires)', () => {
  it('renders the LOCAL wall clock of the instant, not the UTC one', () => {
    // 21:00Z == 18:00 in Argentina (UTC-3). The buggy version returned '21:00',
    // which the submit path then parsed as 21:00 LOCAL (+3h drift per save).
    expect(toDateTimeLocalValue('2026-08-15T21:00:00Z')).toBe('2026-08-15T18:00')
  })

  it('round-trips: input value parsed as local yields the original instant', () => {
    const iso = '2026-08-15T21:00:00Z'
    const input = toDateTimeLocalValue(iso)
    // The submit path parses datetime-local values as LOCAL time
    // (EventForm handleSubmit: new Date(date).toISOString()).
    expect(new Date(input).toISOString()).toBe(new Date(iso).toISOString())
  })

  it('uses the LOCAL calendar day across the UTC day boundary', () => {
    // 2026-09-01T02:00:00Z is Aug 31, 23:00 in Argentina: the date PART must
    // be the local calendar day, and the round trip must hold.
    expect(toDateTimeLocalValue('2026-09-01T02:00:00Z')).toBe('2026-08-31T23:00')
    const iso = '2026-09-01T02:00:00Z'
    expect(new Date(toDateTimeLocalValue(iso)).toISOString()).toBe(new Date(iso).toISOString())
  })

  it('returns empty string for empty or invalid input', () => {
    expect(toDateTimeLocalValue('')).toBe('')
    expect(toDateTimeLocalValue(null)).toBe('')
    expect(toDateTimeLocalValue('not-a-date')).toBe('')
  })
})
