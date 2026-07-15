import { useState } from 'react'
import { Link } from 'react-router-dom'
import apiClient from '../api/client.js'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatEventDate(dateString) {
  if (!dateString) return 'Fecha por confirmar'
  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return 'Fecha no valida'
  return date.toLocaleDateString('es-AR', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function formatCurrency(amount) {
  if (amount === undefined || amount === null) return '$ --'
  return `$ ${Number(amount).toLocaleString('es-AR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
}

function validateEmail(email) {
  if (!email.trim()) return 'El email es obligatorio'
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email))
    return 'El formato del email no es valido'
  return ''
}

function getErrorMessage(error) {
  if (!error) return 'Ocurrio un error inesperado'
  if (error.response?.data?.error?.message) {
    return error.response.data.error.message
  }
  if (error.response?.data?.error) {
    const backendError = error.response.data.error
    return typeof backendError === 'string'
      ? backendError
      : backendError.title || backendError.detail || 'Ocurrio un error inesperado'
  }
  if (error.response?.data?.message) {
    return error.response.data.message
  }
  return 'Ocurrio un error al buscar entradas'
}

// ---------------------------------------------------------------------------
// Ticket card (info-only — no QR, no download, no print)
// ---------------------------------------------------------------------------

function TicketCard({ ticket }) {
  return (
    <article className="ticket-card">
      <div className="ticket-card-body">
        <div className={`ticket-usage-badge ${ticket.isUsed ? 'used' : 'valid'}`}>
          {ticket.isUsed ? 'Usada' : 'Valida'}
        </div>

        <h3>{ticket.eventName}</h3>
        <p className="ticket-event-date">{formatEventDate(ticket.eventDate)}</p>
        <p className="ticket-event-location">{ticket.eventLocation}</p>

        <div className="ticket-type-info">
          <span className="ticket-type-name">{ticket.ticketTypeName}</span>
          <span className="ticket-type-price">{formatCurrency(ticket.price)}</span>
        </div>

        {ticket.quantity !== undefined && ticket.quantity !== null && (
          <p className="ticket-quantity">
            Cantidad: {ticket.quantity}
          </p>
        )}

        {ticket.isUsed && ticket.usedAt && (
          <p className="ticket-used-at">
            Usada el{' '}
            {new Date(ticket.usedAt).toLocaleDateString('es-AR', {
              day: 'numeric',
              month: 'long',
              year: 'numeric',
              hour: '2-digit',
              minute: '2-digit',
            })}
          </p>
        )}
      </div>
    </article>
  )
}

// ---------------------------------------------------------------------------
// Page component
// ---------------------------------------------------------------------------

export default function TicketLookup() {
  // Lookup form state
  const [email, setEmail] = useState('')
  const [errors, setErrors] = useState({})
  const [loading, setLoading] = useState(false)
  const [tickets, setTickets] = useState(null) // null = not searched yet
  const [error, setError] = useState('')

  // Resend form state
  const [resendEmail, setResendEmail] = useState('')
  const [resendErrors, setResendErrors] = useState({})
  const [resendLoading, setResendLoading] = useState(false)
  const [resendMessage, setResendMessage] = useState('')
  const [resendError, setResendError] = useState('')
  const [captchaChecked, setCaptchaChecked] = useState(false)

  // -- Lookup -----------------------------------------------------------

  function validateLookupForm() {
    return {
      email: validateEmail(email),
    }
  }

  async function handleLookupSubmit(e) {
    e.preventDefault()

    const formErrors = validateLookupForm()
    const hasErrors = formErrors.email
    setErrors(formErrors)
    if (hasErrors) return

    setLoading(true)
    setError('')
    setTickets(null)

    try {
      const response = await apiClient.get('/tickets/lookup', {
        params: { email: email.trim() },
      })
      setTickets(response.data || [])
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  function handleClearLookupError() {
    setError('')
    setTickets(null)
  }

  // -- Resend -----------------------------------------------------------

  function validateResendForm() {
    const resendEmailError = validateEmail(resendEmail)
    return { email: resendEmailError }
  }

  async function handleResendSubmit(e) {
    e.preventDefault()

    const formErrors = validateResendForm()
    setResendErrors(formErrors)
    if (formErrors.email) return

    setResendLoading(true)
    setResendMessage('')
    setResendError('')

    try {
      await apiClient.post('/tickets/resend', {
        email: resendEmail.trim(),
        captchaToken: captchaChecked ? 'placeholder' : '',
      })
      setResendMessage(
        'Si el email esta registrado, recibiras las entradas en tu casilla'
      )
    } catch (err) {
      if (err.response?.status === 429) {
        setResendError(
          'Demasiados intentos. Intenta de nuevo en una hora.'
        )
      } else {
        setResendError(getErrorMessage(err))
      }
    } finally {
      setResendLoading(false)
    }
  }

  // -- Render -----------------------------------------------------------

  return (
    <div className="ticket-lookup-page">
      {/* ── Lookup section ───────────────────────────────────────────── */}

      <header className="page-header">
        <h1>Buscar mis entradas</h1>
        <p>Ingresa tu email para recuperar tus entradas</p>
      </header>

      <form onSubmit={handleLookupSubmit} className="lookup-form" noValidate>
        <div className="form-group">
          <label htmlFor="lookup-email">Email</label>
          <input
            id="lookup-email"
            type="email"
            value={email}
            onChange={(e) => {
              setEmail(e.target.value)
              if (errors.email) setErrors((prev) => ({ ...prev, email: '' }))
            }}
            placeholder="tu@email.com"
            disabled={loading}
            aria-invalid={errors.email ? 'true' : undefined}
          />
          {errors.email && (
            <span className="form-error" role="alert">
              {errors.email}
            </span>
          )}
        </div>

        <button type="submit" className="button-primary" disabled={loading}>
          {loading ? 'Buscando...' : 'Buscar entradas'}
        </button>
      </form>

      {error && (
        <div className="error-container" role="alert">
          <p>{error}</p>
          <button type="button" onClick={handleClearLookupError}>
            Reintentar
          </button>
        </div>
      )}

      {tickets !== null && !error && tickets.length === 0 && (
        <div className="empty-state">
          <p>No se encontraron entradas con ese email.</p>
          <p>Verifica que el email sea correcto.</p>
          <Link to="/events" className="button-link">
            Ver eventos
          </Link>
        </div>
      )}

      {tickets && tickets.length > 0 && (
        <div className="tickets-result">
          <h2>
            {tickets.length === 1
              ? '1 entrada encontrada'
              : `${tickets.length} entradas encontradas`}
          </h2>
          <div className="tickets-grid">
            {tickets.map((ticket) => (
              <TicketCard key={ticket.id} ticket={ticket} />
            ))}
          </div>
        </div>
      )}

      {/* ── Resend section ───────────────────────────────────────────── */}

      <section className="resend-section">
        <header className="page-header">
          <h2>Reenviar entradas</h2>
          <p>Si no encuentras tus entradas, podemos reenviartelas por email</p>
        </header>

        <form onSubmit={handleResendSubmit} className="lookup-form" noValidate>
          <div className="form-group">
            <label htmlFor="resend-email">Email</label>
            <input
              id="resend-email"
              type="email"
              value={resendEmail}
              onChange={(e) => {
                setResendEmail(e.target.value)
                if (resendErrors.email)
                  setResendErrors((prev) => ({ ...prev, email: '' }))
              }}
              placeholder="tu@email.com"
              disabled={resendLoading}
              aria-invalid={resendErrors.email ? 'true' : undefined}
            />
            {resendErrors.email && (
              <span className="form-error" role="alert">
                {resendErrors.email}
              </span>
            )}
          </div>

          <div className="form-group captcha-group">
            <label className="captcha-label">
              <input
                type="checkbox"
                checked={captchaChecked}
                onChange={(e) => setCaptchaChecked(e.target.checked)}
                disabled={resendLoading}
                aria-label="No soy un robot"
              />
              <span className="captcha-text">No soy un robot</span>
            </label>
            <p className="captcha-placeholder-note">
              CAPTCHA — sera reemplazado por Turnstile en el futuro
            </p>
          </div>

          <button
            type="submit"
            className="button-primary"
            disabled={resendLoading || !captchaChecked}
          >
            {resendLoading ? 'Enviando...' : 'Reenviar entradas'}
          </button>
        </form>

        {resendMessage && (
          <div className="resend-message" role="status">
            <p>{resendMessage}</p>
          </div>
        )}

        {resendError && (
          <div className="error-container" role="alert">
            <p>{resendError}</p>
          </div>
        )}
      </section>
    </div>
  )
}
