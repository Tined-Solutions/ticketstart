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

function validateDNI(dni) {
  if (!dni.trim()) return 'El DNI es obligatorio'
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
// Ticket card
// ---------------------------------------------------------------------------

function TicketCard({ ticket, onDownload }) {
  const qrSrc = `data:image/png;base64,${ticket.qrCodeImage}`

  function handleDownload() {
    onDownload(ticket.qrCodeImage, ticket.id)
  }

  function handlePrint() {
    const printWindow = window.open('', '_blank')
    if (!printWindow) return

    printWindow.document.write(`
      <!DOCTYPE html>
      <html>
        <head>
          <title>Entrada - ${ticket.eventName}</title>
          <style>
            body { font-family: system-ui, sans-serif; text-align: center; padding: 20px; }
            img { max-width: 300px; height: auto; }
            h2 { margin-bottom: 4px; }
            p { margin: 2px 0; color: #555; }
          </style>
        </head>
        <body>
          <h2>${ticket.eventName}</h2>
          <p>${formatEventDate(ticket.eventDate)}</p>
          <p>${ticket.eventLocation}</p>
          <hr />
          <p><strong>${ticket.ticketTypeName}</strong> — ${formatCurrency(ticket.price)}</p>
          <img src="${qrSrc}" alt="Codigo QR de la entrada" />
          <p style="font-size:0.8em;margin-top:8px;">${ticket.qrCodeData.slice(0, 30)}...</p>
        </body>
      </html>
    `)
    printWindow.document.close()
    printWindow.focus()
    printWindow.print()
    printWindow.close()
  }

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

        <div className="ticket-qr-container">
          <img
            src={qrSrc}
            alt={`Codigo QR de ${ticket.eventName}`}
            className="ticket-qr-image"
          />
        </div>

        <div className="ticket-actions">
          <button
            type="button"
            className="button-secondary"
            onClick={handleDownload}
            aria-label={`Descargar QR de ${ticket.eventName}`}
          >
            Descargar QR
          </button>
          <button
            type="button"
            className="button-secondary"
            onClick={handlePrint}
            aria-label={`Imprimir entrada de ${ticket.eventName}`}
          >
            Imprimir entrada
          </button>
        </div>

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
  const [email, setEmail] = useState('')
  const [dni, setDni] = useState('')
  const [errors, setErrors] = useState({})
  const [loading, setLoading] = useState(false)
  const [tickets, setTickets] = useState(null) // null = not searched yet
  const [error, setError] = useState('')

  function validateForm() {
    return {
      email: validateEmail(email),
      dni: validateDNI(dni),
    }
  }

  async function handleSubmit(e) {
    e.preventDefault()

    const formErrors = validateForm()
    const hasErrors = formErrors.email || formErrors.dni
    setErrors(formErrors)
    if (hasErrors) return

    setLoading(true)
    setError('')
    setTickets(null)

    try {
      const response = await apiClient.get('/tickets/lookup', {
        params: { email: email.trim(), dni: dni.trim() },
      })
      setTickets(response.data || [])
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  function handleDownload(qrCodeImage, ticketId) {
    const link = document.createElement('a')
    link.href = `data:image/png;base64,${qrCodeImage}`
    link.download = `qr-entrada-${ticketId.slice(0, 8)}.png`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
  }

  function handleClearError() {
    setError('')
    setTickets(null)
  }

  return (
    <div className="ticket-lookup-page">
      <header className="page-header">
        <h1>Buscar mis entradas</h1>
        <p>Ingresa tu email y DNI para recuperar tus entradas</p>
      </header>

      <form onSubmit={handleSubmit} className="lookup-form" noValidate>
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

        <div className="form-group">
          <label htmlFor="lookup-dni">DNI</label>
          <input
            id="lookup-dni"
            type="text"
            inputMode="numeric"
            value={dni}
            onChange={(e) => {
              setDni(e.target.value)
              if (errors.dni) setErrors((prev) => ({ ...prev, dni: '' }))
            }}
            placeholder="12345678"
            disabled={loading}
            aria-invalid={errors.dni ? 'true' : undefined}
          />
          {errors.dni && (
            <span className="form-error" role="alert">
              {errors.dni}
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
          <button type="button" onClick={handleClearError}>
            Reintentar
          </button>
        </div>
      )}

      {tickets !== null && !error && tickets.length === 0 && (
        <div className="empty-state">
          <p>No se encontraron entradas con esos datos.</p>
          <p>Verifica que el email y DNI sean correctos.</p>
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
              <TicketCard
                key={ticket.id}
                ticket={ticket}
                onDownload={handleDownload}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
