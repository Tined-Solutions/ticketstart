import { useEffect, useId, useRef, useState } from 'react'
import { ChevronDown } from 'lucide-react'
import Spinner from '../Spinner.jsx'

function formatEventLabel(event) {
  const date = new Date(event.date)
  const dateStr = date.toLocaleDateString('es-AR')
  const timeStr = date.toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hour12: false })
  return `${event.name} — ${dateStr} — ${timeStr} — ${event.location}`
}

export default function EventSelect({
  events = [],
  value = '',
  onChange,
  disabled = false,
  loading = false,
  error = '',
  placeholder = 'Seleccionar evento...',
  id,
  className = '',
  ...rest
}) {
  const [open, setOpen] = useState(false)
  const [highlightedIndex, setHighlightedIndex] = useState(-1)
  const containerRef = useRef(null)
  const optionRefs = useRef({})
  const listboxId = useId()

  const selectedIndex = events.findIndex((e) => e.id === value)
  const selectedEvent = selectedIndex >= 0 ? events[selectedIndex] : null

  const close = () => {
    setOpen(false)
    setHighlightedIndex(-1)
  }

  const selectOption = (id) => {
    onChange?.(id)
    close()
  }

  const handleTriggerClick = () => {
    if (disabled || loading) return
    if (open) {
      close()
      return
    }
    setHighlightedIndex(selectedIndex)
    setOpen(true)
  }

  const handleTriggerKeyDown = (e) => {
    if (disabled || loading) return

    if (!open) {
      if (e.key === 'ArrowDown') {
        e.preventDefault()
        setHighlightedIndex(0)
        setOpen(true)
      } else if (e.key === 'ArrowUp') {
        e.preventDefault()
        setHighlightedIndex(events.length - 1)
        setOpen(true)
      } else if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault()
        setHighlightedIndex(selectedIndex >= 0 ? selectedIndex : 0)
        setOpen(true)
      }
      return
    }

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault()
        setHighlightedIndex((i) => (i >= events.length - 1 ? i : i + 1))
        break
      case 'ArrowUp':
        e.preventDefault()
        setHighlightedIndex((i) => (i < 0 ? events.length - 1 : i - 1))
        break
      case 'Home':
        e.preventDefault()
        setHighlightedIndex(0)
        break
      case 'End':
        e.preventDefault()
        setHighlightedIndex(events.length - 1)
        break
      case 'Enter':
      case ' ':
        e.preventDefault()
        if (events[highlightedIndex]) {
          selectOption(events[highlightedIndex].id)
        }
        break
      case 'Escape':
        e.preventDefault()
        close()
        break
      default:
        break
    }
  }

  useEffect(() => {
    if (!open) return
    const handleMouseDown = (e) => {
      if (containerRef.current && !containerRef.current.contains(e.target)) {
        close()
      }
    }
    const handleKeyDown = (e) => {
      if (e.key === 'Escape') close()
    }
    document.addEventListener('mousedown', handleMouseDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('mousedown', handleMouseDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [open])

  useEffect(() => {
    if (open && highlightedIndex >= 0) {
      optionRefs.current[highlightedIndex]?.scrollIntoView?.({ block: 'nearest' })
    }
  }, [open, highlightedIndex, events])

  if (loading) {
    return (
      <div className="flex items-center gap-2 py-2">
        <Spinner size="sm" label="Cargando eventos..." />
        <span className="text-sm text-text-2">Cargando eventos...</span>
      </div>
    )
  }

  if (error) {
    return (
      <p className="form-error" role="alert">
        {error}
      </p>
    )
  }

  return (
    <div ref={containerRef} className={`relative w-full ${className}`} {...rest}>
      <button
        type="button"
        id={id}
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listboxId}
        onClick={handleTriggerClick}
        onKeyDown={handleTriggerKeyDown}
        className="flex w-full min-w-0 items-center justify-between gap-2 rounded-lg border border-border bg-canvas px-3 py-2.5 text-left text-base text-text-1 transition-colors hover:border-border-hover focus-visible:border-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/30 disabled:cursor-not-allowed disabled:opacity-60"
      >
        <span className={`min-w-0 truncate ${selectedEvent ? 'text-text-1' : 'text-text-muted'}`}>
          {selectedEvent ? formatEventLabel(selectedEvent) : placeholder}
        </span>
        <span
          aria-hidden="true"
          className={`shrink-0 text-text-muted transition-transform ${open ? 'rotate-180' : ''}`}
        >
          <ChevronDown className="h-5 w-5" />
        </span>
      </button>

      {open && events.length > 0 && (
        <ul
          id={listboxId}
          role="listbox"
          aria-label="Eventos"
          aria-activedescendant={
            highlightedIndex >= 0 ? `${listboxId}-option-${highlightedIndex}` : undefined
          }
          className="glass-surface absolute z-50 mt-2 max-h-72 w-full overflow-y-auto rounded-lg p-1 shadow-xl"
        >
          {events.map((event, i) => {
            const selected = event.id === value
            const highlighted = i === highlightedIndex
            return (
              <li
                key={event.id}
                ref={(el) => {
                  optionRefs.current[i] = el
                }}
                id={`${listboxId}-option-${i}`}
                role="option"
                aria-selected={selected}
                onClick={() => selectOption(event.id)}
                onMouseEnter={() => setHighlightedIndex(i)}
                className={`cursor-pointer rounded-md px-3 py-2 text-sm transition-colors ${
                  selected
                    ? 'bg-accent-light font-medium text-accent-hover'
                    : 'text-text-1 hover:bg-neutral-100'
                } ${highlighted && !selected ? 'bg-neutral-100' : ''}`}
              >
                {formatEventLabel(event)}
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}