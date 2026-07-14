import { useEffect, useRef, useCallback } from 'react'

export default function Modal({
  open,
  onClose,
  title,
  children,
  footer,
  className = '',
}) {
  const overlayRef = useRef(null)
  const previousFocus = useRef(null)

  // Save and restore focus
  useEffect(() => {
    if (open) {
      previousFocus.current = document.activeElement
    } else if (previousFocus.current) {
      previousFocus.current.focus()
      previousFocus.current = null
    }
  }, [open])

  // ESC to close
  const handleKeyDown = useCallback(
    (e) => {
      if (e.key === 'Escape') {
        onClose()
      }
    },
    [onClose]
  )

  useEffect(() => {
    if (open) {
      document.addEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'hidden'
    }
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = ''
    }
  }, [open, handleKeyDown])

  // Focus trap: keep focus inside modal
  useEffect(() => {
    if (!open) return

    const overlay = overlayRef.current
    if (!overlay) return

    const focusableSelector =
      'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'

    const handleFocusTrap = (e) => {
      const focusable = overlay.querySelectorAll(focusableSelector)
      if (focusable.length === 0) return

      const first = focusable[0]
      const last = focusable[focusable.length - 1]

      if (e.key === 'Tab') {
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault()
          last.focus()
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault()
          first.focus()
        }
      }
    }

    overlay.addEventListener('keydown', handleFocusTrap)

    // Auto-focus first focusable element
    const firstFocusable = overlay.querySelector(focusableSelector)
    if (firstFocusable) {
      firstFocusable.focus()
    }

    return () => overlay.removeEventListener('keydown', handleFocusTrap)
  }, [open])

  if (!open) return null

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-5"
      role="dialog"
      aria-modal="true"
      aria-labelledby={title ? 'modal-title' : undefined}
      ref={overlayRef}
    >
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/50"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Content */}
      <div
        className={`relative bg-white rounded-xl shadow-xl max-w-lg w-full max-h-[90vh] overflow-auto z-10 ${className}`}
      >
        {/* Header */}
        {title && (
          <div className="flex items-center justify-between px-6 py-4 border-b border-border">
            <h2 id="modal-title" className="text-lg font-semibold text-gray-900 m-0">
              {title}
            </h2>
            <button
              type="button"
              onClick={onClose}
              className="p-1 rounded-md text-neutral-500 hover:text-neutral-700 hover:bg-neutral-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
              aria-label="Cerrar"
            >
              <svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                <path d="M15 5L5 15M5 5l10 10" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
              </svg>
            </button>
          </div>
        )}

        {/* Body */}
        <div className="px-6 py-4">{children}</div>

        {/* Footer */}
        {footer && (
          <div className="flex justify-end gap-3 px-6 py-4 border-t border-border bg-neutral-50">
            {footer}
          </div>
        )}
      </div>
    </div>
  )
}
