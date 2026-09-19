import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { DayPicker } from '@daypicker/react'
import { es } from '@daypicker/react/locale'
import { CalendarDays } from 'lucide-react'
import { useDialog } from '../../hooks/useDialog.js'
import { prefersReducedMotion } from '../../lib/motion.js'
import Button from '../Button.jsx'

/**
 * DateTimePicker — custom date + time picker for the event form.
 *
 * Contract (kept identical to the native datetime-local input it replaces):
 * `value` is a local wall-clock string "YYYY-MM-DDTHH:mm"; `onChange` receives
 * the same shape. The parent owns the conversion to an ISO instant.
 *
 * Built on DayPicker/`@daypicker/react` v10 with Tailwind classNames mapped to
 * the app tokens (no structural style.css import: all slots are mapped here so
 * utilities never fight unlayered library CSS).
 */

const VALUE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::\d{2})?$/

const HOURS = Array.from({ length: 24 }, (_, index) => String(index).padStart(2, '0'))
// Minutes in 5-minute steps; an existing off-grid value is appended
// dynamically (see minuteOptions) so editing never loses the original time.
const MINUTES = Array.from({ length: 12 }, (_, index) => String(index * 5).padStart(2, '0'))

const ES_DATE = new Intl.DateTimeFormat('es-AR', {
  weekday: 'short',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
})

const ES_TIME = new Intl.DateTimeFormat('es-AR', {
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

// DayPicker's default labelDayButton is English ("Today, ...", ", selected");
// keep screen-reader copy in Spanish for this app.
const DAYPICKER_LABELS = {
  labelDayButton: (date, modifiers) => {
    let label = new Intl.DateTimeFormat('es-AR', { dateStyle: 'full' }).format(date)
    if (modifiers?.today) label = `Hoy, ${label}`
    if (modifiers?.selected) label = `${label}, seleccionado`
    return label
  },
}

// Full slot mapping for DayPicker v10 (UI + day flags + selection states).
// State precedence is expressed with `data-*` guards so it never depends on
// Tailwind's generated stylesheet order:
//   - base day  -> hover violet
//   - selected  -> accent background (and accent-hover while hovering)
//   - today     -> brand-1 text, forced white once selected (guard beats base)
//   - outside   -> muted text
const CALENDAR_CLASSNAMES = {
  root: 'relative w-full font-sans text-text-1',
  months: 'relative flex max-w-fit',
  month: 'relative w-full',
  month_caption: 'flex h-10 items-center',
  caption_label: 'inline-flex items-center gap-0.5 whitespace-nowrap',
  dropdowns: 'inline-flex items-center gap-1',
  dropdown_root:
    'relative inline-flex min-h-9 cursor-pointer items-center gap-0.5 rounded-lg px-2 py-1 text-[14px] font-semibold text-text-1 transition-colors hover:bg-purpura/10 focus-within:ring-2 focus-within:ring-accent focus-within:ring-offset-1',
  dropdown:
    'absolute inset-0 z-[2] w-full cursor-pointer appearance-none border-0 bg-transparent p-0 opacity-0',
  nav: 'absolute right-0 top-0 flex h-10 items-center gap-0.5',
  button_previous:
    'inline-flex h-8 w-8 items-center justify-center rounded-full border-0 bg-transparent p-0 text-text-2 transition-colors hover:bg-purpura/10 hover:text-brand-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-1 aria-disabled:opacity-40',
  button_next:
    'inline-flex h-8 w-8 items-center justify-center rounded-full border-0 bg-transparent p-0 text-text-2 transition-colors hover:bg-purpura/10 hover:text-brand-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-1 aria-disabled:opacity-40',
  chevron: 'fill-current',
  month_grid: 'w-full table-fixed border-collapse',
  weekdays: '',
  weekday: 'pb-1 text-center text-[12px] font-medium text-text-muted',
  weeks: '',
  week: '',
  // Fluid cells: table-fixed splits the panel width in 7 equal columns, and the
  // button is an aspect-square circle that shrinks with it — no horizontal
  // overflow at 320px, comfortable ≥40px targets at 360px.
  day: 'p-0 text-center align-middle text-[14px] text-text-1 hover:bg-purpura/10 data-[outside]:text-text-muted',
  day_button:
    'flex aspect-square w-full items-center justify-center rounded-full border-0 bg-transparent p-0 text-[14px] font-normal leading-none transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent',
  selected:
    'rounded-full bg-accent data-[selected]:text-white data-[selected]:hover:bg-accent-hover',
  today: 'font-semibold data-[today]:text-brand-1 data-[selected]:text-white data-[today]:data-[selected]:text-white',
  disabled: 'opacity-40',
  hidden: 'invisible',
  focused: '',
  outside: '',
  range_start: '',
  range_middle: '',
  range_end: '',
}

/** Parses "YYYY-MM-DDTHH:mm" (local wall time) into a draft { day, hours, minutes }. */
function parseValue(value) {
  const match = typeof value === 'string' ? VALUE_PATTERN.exec(value) : null
  if (!match) return { day: undefined, hours: '00', minutes: '00' }
  const [, year, month, day, hours, minutes] = match
  return {
    day: new Date(Number(year), Number(month) - 1, Number(day)),
    hours,
    minutes,
  }
}

/** Human-readable es-AR label for the trigger (e.g. "vie, 25 de diciembre de 2026 · 20:00"). */
function formatDisplayValue(value) {
  const { day, hours, minutes } = parseValue(value)
  if (!day) return ''
  const local = new Date(
    day.getFullYear(),
    day.getMonth(),
    day.getDate(),
    Number(hours),
    Number(minutes)
  )
  return `${ES_DATE.format(local)} · ${ES_TIME.format(local)}`
}

/** Builds "YYYY-MM-DDTHH:mm" from a local Date + zero-padded strings (no timezone math). */
function buildValue(day, hours, minutes) {
  const pad = (part) => String(part).padStart(2, '0')
  return (
    `${day.getFullYear()}-${pad(day.getMonth() + 1)}-${pad(day.getDate())}` +
    `T${hours}:${minutes}`
  )
}

export default function DateTimePicker({
  id,
  value = '',
  onChange,
  disabled = false,
  readOnly = false,
  className = '',
  'aria-invalid': ariaInvalid,
  'aria-describedby': ariaDescribedBy,
}) {
  const generatedId = useId()
  const baseId = id || generatedId
  const panelId = `${baseId}-panel`
  const hoursId = `${baseId}-hours`
  const minutesId = `${baseId}-minutes`

  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState(() => parseValue(value))

  const triggerRef = useRef(null)
  const wasOpenRef = useRef(false)
  // Focus trap, Escape→close, body scroll lock and focus restore come from the
  // project dialog hook. DayPicker's own autoFocus owns initial focus, so
  // useDialog must not steal it (autoFocus: false).
  const popoverRef = useDialog({
    onClose: () => setOpen(false),
    open,
    autoFocus: false,
  })

  const { today, startMonth, endMonth } = useMemo(() => {
    const now = new Date()
    return {
      // Local midnight today: days before it are never selectable, and past
      // months are outside the navigable range entirely.
      today: new Date(now.getFullYear(), now.getMonth(), now.getDate()),
      startMonth: new Date(now.getFullYear(), now.getMonth(), 1),
      endMonth: new Date(now.getFullYear() + 6, 11, 31),
    }
  }, [])

  // Return focus to the trigger when the popover closes (not on first render).
  // useDialog-restored focus would land nowhere here: DayPicker's autoFocus
  // moves focus into the calendar from a child passive effect, which runs
  // BEFORE useDialog captures the previously focused element, so the hook's
  // restore target is a day button that is detached by close time.
  useEffect(() => {
    if (wasOpenRef.current && !open) {
      triggerRef.current?.focus()
    }
    wasOpenRef.current = open
  }, [open])

  // Close on an outside pointerdown. Escape is owned by useDialog.
  useEffect(() => {
    if (!open) return undefined

    function handlePointerDown(event) {
      if (popoverRef.current?.contains(event.target)) return
      if (triggerRef.current?.contains(event.target)) return
      setOpen(false)
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
    }
  }, [open, popoverRef])

  // The dialog hook locks body scroll while the popover is open, so a picker
  // opening near the viewport edge would stay clipped with no way to scroll it
  // into view. Center it on open (jsdom doesn't implement scrollIntoView).
  useEffect(() => {
    if (!open) return
    const popover = popoverRef.current
    if (!popover || typeof popover.scrollIntoView !== 'function') return
    popover.scrollIntoView({
      block: 'center',
      behavior: prefersReducedMotion() ? 'auto' : 'smooth',
    })
  }, [open, popoverRef])

  function handleToggle() {
    if (open) {
      setOpen(false)
      return
    }
    // Re-sync the draft from the committed value: opening without changes must
    // never emit, and a closed picker never keeps a stale draft.
    setDraft(parseValue(value))
    setOpen(true)
  }

  function handleConfirm() {
    if (!draft.day) return
    onChange?.(buildValue(draft.day, draft.hours, draft.minutes))
    setOpen(false)
  }

  const displayValue = formatDisplayValue(value)
  const isDisabled = disabled || readOnly
  // 5-minute grid, plus the current value if it sits off-grid (existing events
  // may carry any minute), so editing never loses the time.
  const minuteOptions = useMemo(
    () => (MINUTES.includes(draft.minutes) ? MINUTES : [...MINUTES, draft.minutes].sort()),
    [draft.minutes]
  )

  return (
    <div className={`relative ${className}`}>
      <button
        ref={triggerRef}
        id={id}
        type="button"
        onClick={handleToggle}
        disabled={isDisabled}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-invalid={ariaInvalid}
        aria-describedby={ariaDescribedBy}
        className="flex w-full items-center gap-2 rounded-lg border border-border bg-white px-[12px] py-[10px] text-left text-[16px] text-text-1 transition-colors hover:border-purpura/60 focus-visible:border-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-light disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:border-border aria-[invalid=true]:border-danger aria-[invalid=true]:focus-visible:ring-danger/25"
      >
        <CalendarDays className="h-5 w-5 shrink-0 text-brand-1" aria-hidden="true" />
        <span className={`min-w-0 flex-1 truncate ${displayValue ? '' : 'text-text-muted'}`}>
          {displayValue || 'Seleccioná fecha y hora'}
        </span>
      </button>

      {open && (
        <div
          ref={popoverRef}
          id={panelId}
          role="dialog"
          aria-modal="true"
          aria-label="Seleccionar fecha y hora"
          className="absolute left-0 top-full z-50 mt-2 w-[min(92vw,18rem)] max-w-full rounded-2xl border border-gris-oscuro/15 bg-white/95 p-2.5 shadow-xl backdrop-blur sm:w-[25rem]"
        >
          <div className="sm:flex sm:items-start sm:gap-3">
            <div className="min-w-0 sm:flex-1">
              <DayPicker
                mode="single"
                required
                selected={draft.day}
                onSelect={(day) => setDraft((prev) => ({ ...prev, day }))}
                defaultMonth={draft.day && draft.day >= startMonth ? draft.day : undefined}
                locale={es}
                weekStartsOn={1}
                captionLayout="dropdown"
                navLayout="after"
                startMonth={startMonth}
                endMonth={endMonth}
                disabled={{ before: today }}
                autoFocus
                labels={DAYPICKER_LABELS}
                classNames={CALENDAR_CLASSNAMES}
              />
            </div>

            <div className="sm:w-[6.5rem]">
              <div className="mt-3 flex items-end gap-2 sm:mt-0 sm:flex-col sm:items-stretch sm:gap-1.5">
                <label htmlFor={hoursId} className="min-w-0 flex-1">
                  <span className="mb-0.5 block text-[12px] font-medium text-text-muted">Hora</span>
                  <select
                    id={hoursId}
                    value={draft.hours}
                    onChange={(event) =>
                      setDraft((prev) => ({ ...prev, hours: event.target.value }))
                    }
                    className="w-full min-h-10"
                  >
                    {HOURS.map((hour) => (
                      <option key={hour} value={hour}>
                        {hour}
                      </option>
                    ))}
                  </select>
                </label>

                <label htmlFor={minutesId} className="min-w-0 flex-1">
                  <span className="mb-0.5 block text-[12px] font-medium text-text-muted">Minutos</span>
                  <select
                    id={minutesId}
                    value={draft.minutes}
                    onChange={(event) =>
                      setDraft((prev) => ({ ...prev, minutes: event.target.value }))
                    }
                    className="w-full min-h-10"
                  >
                    {minuteOptions.map((minute) => (
                      <option key={minute} value={minute}>
                        {minute}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              <Button
                variant="accent"
                onClick={handleConfirm}
                disabled={!draft.day}
                className="mt-3 min-h-10 w-full sm:mt-1.5"
              >
                Listo
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
