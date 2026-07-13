import { useState, useRef, useEffect, useCallback } from 'react'
import { Html5Qrcode } from 'html5-qrcode'
import apiClient from '../api/client.js'

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

export default function StaffScan() {
  const [eventId, setEventId] = useState('')
  const [scanning, setScanning] = useState(false)
  const [result, setResult] = useState(null) // { type: 'success'|'error', message, ticket }
  const [history, setHistory] = useState([]) // Array of scan entries
  const [error, setError] = useState('')
  const scannerRef = useRef(null)

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
        setHistory((prev) => [entry, ...prev])

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
        setHistory((prev) => [entry, ...prev])

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
    if (!eventId.trim()) {
      setError('Debe ingresar el ID del evento')
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
    <div className="staff-scan-page">
      {/* Page header */}
      <header className="page-header">
        <h1>Escanear QR</h1>
        <p>Escanee los códigos QR de los tickets para validar su entrada al evento.</p>
      </header>

      {/* Event ID input & controls */}
      <div className="scanner-controls">
        <div className="form-group">
          <label htmlFor="event-id">ID del Evento</label>
          <input
            id="event-id"
            type="text"
            value={eventId}
            onChange={(e) => {
              setEventId(e.target.value)
              setError('')
              setResult(null)
            }}
            placeholder="00000000-0000-0000-0000-000000000000"
            disabled={scanning}
          />
          {error && <p className="form-error" role="alert">{error}</p>}
        </div>

        <div className="scanner-buttons">
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
        aria-label={scanning ? 'Cámara activa — escaneando' : undefined}
      />

      {/* Result overlay */}
      {result && (
        <div className={`scan-result scan-result--${result.type}`} role="alert">
          <span className="scan-result__icon">
            {result.type === 'success' ? '✓' : '✗'}
          </span>
          <p className="scan-result__message">{result.message}</p>
          {result.ticket && (
            <dl className="scan-result__details">
              <dt>Evento</dt>
              <dd>{result.ticket.eventName}</dd>
              <dt>Tipo</dt>
              <dd>{result.ticket.ticketTypeName}</dd>
              <dt>Comprador</dt>
              <dd>{result.ticket.purchaserEmail}</dd>
            </dl>
          )}
        </div>
      )}

      {/* Scan history */}
      {history.length > 0 && (
        <section className="scan-history">
          <h2>Historial de Escaneos ({history.length})</h2>
          <div className="history-list" role="list">
            {history.map((entry, i) => (
              <div
                key={`${entry.timestamp}-${i}`}
                className={`history-item history-item--${entry.isValid ? 'valid' : 'invalid'}`}
                role="listitem"
              >
                <span className="history-item__time">
                  {new Date(entry.timestamp).toLocaleTimeString('es-AR')}
                </span>
                <span className={`history-item__status badge badge--${entry.isValid ? 'success' : 'danger'}`}>
                  {entry.isValid ? 'Válido' : 'Inválido'}
                </span>
                <span className="history-item__detail">
                  {entry.ticket
                    ? `${entry.ticket.eventName} — ${entry.ticket.ticketTypeName}`
                    : entry.error}
                </span>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
