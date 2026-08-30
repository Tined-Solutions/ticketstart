import { useEffect, useRef, useState } from 'react'

export default function DropdownMenu({
  triggerLabel,
  align = 'right',
  items = [],
  className = '',
  ...rest
}) {
  const [open, setOpen] = useState(false)
  const containerRef = useRef(null)

  const close = () => setOpen(false)

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

  const handleItemClick = (item) => {
    item.onClick?.()
    close()
  }

  const alignClass = align === 'left' ? 'left-0' : 'right-0'

  return (
    <div
      ref={containerRef}
      className={`relative ${open ? 'z-50' : ''} ${className}`}
      {...rest}
    >
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={triggerLabel}
        onClick={() => setOpen((o) => !o)}
        className="inline-flex min-h-[44px] min-w-[44px] items-center justify-center rounded-full border border-gris-oscuro/15 bg-white/60 text-gris-oscuro transition-colors hover:bg-white/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1"
      >
        <span aria-hidden="true">⋯</span>
      </button>

      {open && (
        <div
          role="menu"
          className={`absolute ${alignClass} mt-2 w-48 rounded-lg glass-surface shadow-xl p-1 z-50`}
        >
          {items.map((item, i) => (
            <button
              key={i}
              type="button"
              role="menuitem"
              disabled={item.disabled}
              aria-label={item.ariaLabel}
              title={item.title}
              onClick={() => handleItemClick(item)}
              className={`block w-full min-h-[44px] px-3 py-2 text-left text-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed hover:bg-black/5 focus-visible:bg-black/5 focus-visible:outline-none ${
                item.variant === 'danger' ? 'text-rose-600' : 'text-gris-oscuro'
              }`}
            >
              {item.label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
