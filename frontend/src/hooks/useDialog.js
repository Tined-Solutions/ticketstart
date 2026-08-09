import { useEffect, useRef } from 'react'

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'

/**
 * useDialog — dialog/modal behavior for plain dialog markup (a div with
 * `role="dialog"`). Callers keep full control of the rendered DOM; the hook
 * only wires up the behavior:
 *
 *   - focus trap: Tab/Shift+Tab cycle inside the dialog, re-querying focusable
 *     nodes on every keypress (safe for dynamically rendered content)
 *   - Escape → onClose
 *   - body scroll lock (overflow: hidden) + `overscroll-contain` class
 *   - auto-focus on the first focusable element when opened
 *   - focus restore to the previously focused element on close
 *
 * @param {object} options
 * @param {() => void} options.onClose — called on Escape
 * @param {boolean} [options.open=true] — when false the effects are inert
 * @param {boolean} [options.autoFocus=true] — focus the first focusable on open
 * @returns {React.RefObject} ref to attach to the dialog element
 */
export function useDialog({ onClose, open = true, autoFocus = true }) {
  const ref = useRef(null)
  const onCloseRef = useRef(onClose)

  // Keep the ref in sync without re-attaching the keydown listeners.
  useEffect(() => {
    onCloseRef.current = onClose
  })

  useEffect(() => {
    const dialog = ref.current
    if (!open || !dialog) return undefined

    const previousFocus = document.activeElement
    const prevOverflow = document.body.style.overflow

    // Body scroll lock + overscroll containment (the class is defined in index.css).
    document.body.style.overflow = 'hidden'
    document.body.classList.add('overscroll-contain')

    const handleKeyDown = (e) => {
      if (e.key === 'Escape') {
        onCloseRef.current()
      }
    }

    const handleFocusTrap = (e) => {
      if (e.key !== 'Tab') return
      // Re-evaluate focusable nodes on each Tab press (dynamic content).
      const focusable = dialog.querySelectorAll(FOCUSABLE_SELECTOR)
      if (focusable.length === 0) return

      const first = focusable[0]
      const last = focusable[focusable.length - 1]

      if (e.shiftKey && (document.activeElement === first || !dialog.contains(document.activeElement))) {
        e.preventDefault()
        last.focus()
      } else if (!e.shiftKey && (document.activeElement === last || !dialog.contains(document.activeElement))) {
        e.preventDefault()
        first.focus()
      }
    }

    if (autoFocus) {
      const firstFocusable = dialog.querySelector(FOCUSABLE_SELECTOR)
      if (firstFocusable) {
        firstFocusable.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    dialog.addEventListener('keydown', handleFocusTrap)

    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      dialog.removeEventListener('keydown', handleFocusTrap)
      document.body.style.overflow = prevOverflow
      document.body.classList.remove('overscroll-contain')
      // Restore focus to whatever was focused before the dialog opened.
      if (previousFocus && document.contains(previousFocus)) {
        previousFocus.focus()
      }
    }
  }, [open, autoFocus])

  return ref
}

export default useDialog
