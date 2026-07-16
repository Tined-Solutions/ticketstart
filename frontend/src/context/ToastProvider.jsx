import { useCallback, useMemo, useState, useRef } from 'react'
import { ToastContext } from './ToastContext.js'

// ── Toast item component ──────────────────────────────────────────────────

const typeStyles = {
  success: 'bg-success text-white',
  error: 'bg-danger text-white',
  info: 'bg-info text-white',
  warning: 'bg-warning text-neutral-900',
}

const typeIcons = {
  success: '✓',
  error: '✕',
  info: 'ℹ',
  warning: '⚠',
}

function ToastItem({ toast, onDismiss }) {
  return (
    <div
      role="alert"
      aria-live="polite"
      className={`flex items-center gap-3 px-4 py-3 rounded-lg shadow-lg pointer-events-auto
        animate-[toast-in_0.3s_ease-out] ${typeStyles[toast.type] || typeStyles.info}`}
    >
      <span className="text-lg font-bold flex-shrink-0" aria-hidden="true">
        {typeIcons[toast.type] || typeIcons.info}
      </span>
      <span className="flex-1 text-sm font-medium">{toast.message}</span>
      <button
        type="button"
        onClick={() => onDismiss(toast.id)}
        className="flex-shrink-0 p-1 rounded hover:bg-white/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/50"
        aria-label="Cerrar notificacion"
      >
        <svg width="16" height="16" viewBox="0 0 20 20" fill="none" aria-hidden="true">
          <path d="M15 5L5 15M5 5l10 10" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
        </svg>
      </button>
    </div>
  )
}

// ── Provider ────────────────────────────────────────────────────────────────

export default function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([])
  const timersRef = useRef({})
  const nextIdRef = useRef(1)

  const dismiss = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id))
    if (timersRef.current[id]) {
      clearTimeout(timersRef.current[id])
      delete timersRef.current[id]
    }
  }, [])

  const addToast = useCallback(
    (message, type = 'info', duration = 5000) => {
      const id = nextIdRef.current++
      setToasts((prev) => [...prev, { id, message, type }])

      if (duration > 0) {
        timersRef.current[id] = setTimeout(() => {
          dismiss(id)
        }, duration)
      }

      return id
    },
    [dismiss]
  )

  const toast = useMemo(
    () => ({
      success: (message, duration) => addToast(message, 'success', duration),
      error: (message, duration) => addToast(message, 'error', duration),
      info: (message, duration) => addToast(message, 'info', duration),
      warning: (message, duration) => addToast(message, 'warning', duration),
    }),
    [addToast]
  )

  return (
    <ToastContext.Provider value={{ toast, dismiss }}>
      {children}

      {/* Toast container */}
      {toasts.length > 0 && (
        <div
          className="fixed bottom-4 right-4 z-[100] flex flex-col gap-2 max-w-sm w-full pointer-events-none"
          aria-label="Notificaciones"
        >
          {toasts.map((t) => (
            <ToastItem key={t.id} toast={t} onDismiss={dismiss} />
          ))}
        </div>
      )}
    </ToastContext.Provider>
  )
}
