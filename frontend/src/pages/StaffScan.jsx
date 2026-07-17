import { useState, useRef, useEffect, useCallback } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { Html5Qrcode } from 'html5-qrcode'
import apiClient from '../api/client.js'
import Badge from '../components/ui/Badge.jsx'
import Spinner from '../components/Spinner.jsx'
import { fadeInScale } from '../lib/motion.js'

// ---------------------------------------------------------------------------
// Audio helpers
// ---------------------------------------------------------------------------

function playSuccessBeep() {
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)()
    const now = ctx.currentTime

    const osc1 = ctx.createOscillator()
    const gain1 = ctx.createGain()
    osc1.type = 'sine'
    osc1.frequency.value = 880
    gain1.gain.setValueAtTime(0.3, now)
    gain1.gain.exponentialRampToValueAtTime(0.01, now + 0.15)
    osc1.connect(gain1)
    gain1.connect(ctx.destination)
    osc1.start(now)
    osc1.stop(now + 0.15)

    const osc2 = ctx.createOscillator()
    const gain2 = ctx.createGain()
    osc2.type = 'sine'
    osc2.frequency.value = 1100
    gain2.gain.setValueAtTime(0.3, now + 0.15)
    gain2.gain.exponentialRampToValueAtTime(0.01, now + 0.35)
    osc2.connect(gain2)
    gain2.connect(ctx.destination)
    osc2.start(now + 0.15)
    osc2.stop(now + 0.35)
  } catch {
    // Audio not available — fail silently
  }
}

function playErrorBeep() {
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)()
    const now = ctx.currentTime

    const osc1 = ctx.createOscillator()
    const gain1 = ctx.createGain()
    osc1.type = 'square'
    osc1.frequency.value = 400
    gain1.gain.setValueAtTime(0.3, now)
    gain1.gain.exponentialRampToValueAtTime(0.01, now + 0.25)
    osc1.connect(gain1)
    gain1.connect(ctx.destination)
    osc1.start(now)
    osc1.stop(now + 0.25)

    const osc2 = ctx.createOscillator()
    const gain2 = ctx.createGain()
    osc2.type = 'square'
    osc2.frequency.value = 300
    gain2.gain.setValueAtTime(0.3, now + 0.25)
    gain2.gain.exponentialRampToValueAtTime(0.01, now + 0.5)
    osc2.connect(gain2)
    gain2.connect(ctx.destination)
    osc2.start(now + 0.25)
    osc2.stop(now + 0.5)
  } catch {
    // Audio not available — fail silently
  }
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

const SESSION_STORAGE_KEY = 'staff_scan_history'

function loadHistoryFromStorage() {
  try {
    const stored = sessionStorage.getItem(SESSION_STORAGE_KEY)
    return stored ? JSON.parse(stored) : []
  } catch {
    return []
  }
}

function saveHistoryToStorage(history) {
  try {
    sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(history))
  } catch {
    // Storage may be unavailable — fail silently
  }
}

export default function StaffScan() {
  const [eventId, setEventId] = useState('')
  const [scanning, setScanning] = useState(false)
  const [result, setResult] = useState(null) // { type: 'success'|'error', message, ticket }
  const [history, setHistory] = useState(() => loadHistoryFromStorage()) // Initialize from sessionStorage
  const [error, setError] = useState('')
  const [events, setEvents] = useState([])
  const [eventsLoading, setEventsLoading] = useState(true)
  const [eventsError, setEventsError] = useState('')
  const scannerRef = useRef(null)

  // -----------------------------------------------------------------------
  // Fetch events on mount
  // -----------------------------------------------------------------------

  useEffect(() => {
    let cancelled = false
    async function fetchEvents() {
      try {
        setEventsLoading(true)
        setEventsError('')
        const response = await apiClient.get('/events')
        if (!cancelled) {
          setEvents(response.data)
        }
      } catch {
        if (!cancelled) {
          setEventsError('No se pudieron cargar los eventos. Intente recargar la pagina.')
        }
      } finally {
        if (!cancelled) {
          setEventsLoading(false)
        }
      }
    }
    fetchEvents()
    return () => { cancelled = true }
  }, [])

  // -----------------------------------------------------------------------
  // Scanner lifecycle
  // -----------------------------------------------------------------------

  const stopScanner = useCallback(async () => {
    const s = scannerRef.current
    if (s && s.isScanning) {
      try {
        await s.stop()
      } catch {
        // Swallow — already stopped or element removed
      }
    }
    scannerRef.current = null
    setScanning(false)
  }, [])

  const handleScanSuccess = useCallback(
    async (decodedText) => {
      await stopScanner()

      try {
        const response = await apiClient.post('/tickets/validate', {
          qrCodeData: decodedText,
          eventId,
        })

        const { isValid, error: apiError, ticket } = response.data

        const entry = {
          timestamp: new Date().toISOString(),
          qrCodeData: decodedText,
          eventId,
          isValid,
          error: apiError || null,
          ticket: ticket || null,
        }
        setHistory((prev) => {
          const updated = [entry, ...prev]
          saveHistoryToStorage(updated)
          return updated
        })

        if (isValid) {
          setResult({ type: 'success', message: 'Ticket válido', ticket })
          playSuccessBeep()
        } else {
          setResult({ type: 'error', message: apiError || 'Ticket inválido', ticket: ticket || null })
          playErrorBeep()
        }
      } catch (err) {
        const message =
          err?.response?.data?.error?.message ||
          err?.response?.data?.error ||
          'Error de conexión al validar el ticket'

        const entry = {
          timestamp: new Date().toISOString(),
          qrCodeData: decodedText,
          eventId,
          isValid: false,
          error: typeof message === 'string' ? message : 'Error de validación',
          ticket: null,
        }
        setHistory((prev) => {
          const updated = [entry, ...prev]
          saveHistoryToStorage(updated)
          return updated
        })

        setResult({
          type: 'error',
          message: typeof message === 'string' ? message : 'Error de validación',
          ticket: null,
        })
        playErrorBeep()
      }
    },
    [eventId, stopScanner]
  )

  const startScanning = useCallback(async () => {
    if (!eventId) {
      setError('Debe seleccionar un evento')
      return
    }

    setError('')
    setResult(null)

    try {
      const scanner = new Html5Qrcode('qr-reader')
      scannerRef.current = scanner

      await scanner.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 250, height: 250 } },
        (decodedText) => {
          // Fire-and-forget to avoid blocking the scanner callback
          handleScanSuccess(decodedText)
        },
        () => {
          // Ignore intermediate scan-frame errors (e.g. no QR in frame)
        }
      )

      setScanning(true)
    } catch (err) {
      setError('No se pudo acceder a la cámara. Verifique los permisos del navegador.')
      console.error('Camera error:', err)
    }
  }, [eventId, handleScanSuccess])

  const resetScan = useCallback(() => {
    setResult(null)
    setError('')
  }, [])

  // Cleanup scanner on unmount
  useEffect(() => {
    return () => {
      if (scannerRef.current?.isScanning) {
        scannerRef.current.stop().catch(() => {})
      }
    }
  }, [])

  // -----------------------------------------------------------------------
  // Render
  // -----------------------------------------------------------------------

  return (
    <motion.div
      variants={fadeInScale}
      initial="initial"
      animate="animate"
      className="max-w-3xl mx-auto px-4 sm:px-6 py-10"
    >
      {/* Page header */}
      <header className="mb-8 text-center">
        <h1 className="text-4xl font-display font-bold text-text-1 mb-2">
          Escanear QR
        </h1>
        <p className="text-text-2">Escanee los codigos QR de los tickets para validar su entrada al evento.</p>
      </header>

      {/* Event selector & controls */}
      <div className="glass-surface p-6 mb-6">
        <div className="form-group">
          <label htmlFor="event-select">Evento</label>
          {eventsLoading && (
            <div className="flex items-center gap-2 py-2">
              <Spinner size="sm" label="Cargando eventos..." />
              <span className="text-text-2 text-sm">Cargando eventos...</span>
            </div>
          )}
          {eventsError && (
            <motion.p
              className="form-error"
              role="alert"
              initial={{ opacity: 0, y: -4 }}
              animate={{ opacity: 1, y: 0 }}
            >
              {eventsError}
            </motion.p>
          )}
          {!eventsLoading && !eventsError && (
            <select
              id="event-select"
              value={eventId}
              onChange={(e) => {
                setEventId(e.target.value)
                setError('')
                setResult(null)
              }}
              disabled={scanning}
            >
              <option value="" disabled>Seleccionar evento...</option>
              {events.map((event) => (
                <option key={event.id} value={event.id}>
                  {event.name} — {new Date(event.date).toLocaleDateString('es-AR')} — {event.location}
                </option>
              ))}
            </select>
          )}
          {error && (
            <motion.p
              className="form-error"
              role="alert"
              initial={{ opacity: 0, y: -4 }}
              animate={{ opacity: 1, y: 0 }}
            >
              {error}
            </motion.p>
          )}
        </div>

        <div className="flex gap-3 justify-center flex-wrap mt-4">
          {!scanning && !result && (
            <button onClick={startScanning} className="button-primary">
              Iniciar Escaneo
            </button>
          )}
          {scanning && (
            <button onClick={stopScanner} className="button-secondary">
              Detener Escaneo
            </button>
          )}
          {result && (
            <button onClick={resetScan} className="button-primary">
              Escanear Otro
            </button>
          )}
        </div>
      </div>

      {/* Camera feed */}
      <div
        id="qr-reader"
        className={`qr-reader-container${scanning ? ' active' : ''}`}
        aria-label={scanning ? 'Camara activa — escaneando' : undefined}
      />

      {/* Result overlay */}
      <AnimatePresence>
        {result && (
          <motion.div
            variants={fadeInScale}
            initial="initial"
            animate="animate"
            exit="exit"
            className={`text-center p-6 rounded-xl mb-6 ${
              result.type === 'success'
                ? 'bg-emerald-500/10 border-2 border-emerald-500'
                : 'bg-rose-500/10 border-2 border-rose-500'
            }`}
            role="alert"
          >
            <span
              className={`inline-flex items-center justify-center w-14 h-14 rounded-full text-2xl font-bold mb-3 ${
                result.type === 'success' ? 'bg-emerald-500 text-white' : 'bg-rose-500 text-white'
              }`}
              aria-hidden="true"
            >
              {result.type === 'success' ? '\u2713' : '\u2717'}
            </span>
            <p className="text-lg font-semibold text-text-1 mb-4">{result.message}</p>
            {result.ticket && (
              <dl className="grid grid-cols-[auto_1fr] gap-1 gap-x-3 text-left max-w-sm mx-auto p-3 bg-surface-elevated rounded-lg">
                <dt className="font-medium text-text-1">Evento</dt>
                <dd className="text-text-2 m-0">{result.ticket.eventName}</dd>
                <dt className="font-medium text-text-1">Tipo</dt>
                <dd className="text-text-2 m-0">{result.ticket.ticketTypeName}</dd>
                <dt className="font-medium text-text-1">Comprador</dt>
                <dd className="text-text-2 m-0">{result.ticket.purchaserEmail}</dd>
              </dl>
            )}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Scan history */}
      {history.length > 0 && (
        <section className="mt-8">
          <h2 className="text-xl font-display font-semibold text-text-1 mb-4">
            Historial de Escaneos ({history.length})
          </h2>
          <div className="flex flex-col gap-2" role="list">
            {history.map((entry, i) => (
              <div
                key={`${entry.timestamp}-${i}`}
                className={`flex items-center gap-2 py-2.5 px-3.5 rounded-lg text-sm border ${
                  entry.isValid ? 'border-l-4 border-l-emerald-500 border-border' : 'border-l-4 border-l-rose-500 border-border'
                }`}
                role="listitem"
              >
                <span className="font-mono text-text-2 whitespace-nowrap min-w-[4.5rem]">
                  {new Date(entry.timestamp).toLocaleTimeString('es-AR')}
                </span>
                <Badge variant={entry.isValid ? 'success' : 'error'}>
                  {entry.isValid ? 'Valido' : 'Invalido'}
                </Badge>
                <span className="flex-1 text-text-2 overflow-hidden text-ellipsis whitespace-nowrap">
                  {entry.ticket
                    ? `${entry.ticket.eventName} — ${entry.ticket.ticketTypeName}`
                    : entry.error}
                </span>
              </div>
            ))}
          </div>
        </section>
      )}
    </motion.div>
  )
}
