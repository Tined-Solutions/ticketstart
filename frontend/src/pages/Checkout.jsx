import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import apiClient from '../api/client.js'
import { useAuth } from '../context/auth.js'

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

function formatCountdown(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
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
  if (error.response?.data?.detail) {
    return error.response.data.detail
  }
  if (error.message) {
    return error.message
  }
  return 'Ocurrio un error inesperado'
}

export default function Checkout() {
  const location = useLocation()
  const navigate = useNavigate()
  const { user } = useAuth()

  const cart = location.state

  useEffect(() => {
    if (!cart?.selections || cart.selections.length === 0) {
      navigate('/events', { replace: true })
    }
  }, [cart, navigate])

  const [purchaserName, setPurchaserName] = useState(user?.name || '')
  const [purchaserEmail, setPurchaserEmail] = useState(user?.email || '')
  const [purchaserDNI, setPurchaserDNI] = useState('')
  const [reservations, setReservations] = useState([])
  const [loading, setLoading] = useState(false)
  const [payLoading, setPayLoading] = useState(false)
  const [error, setError] = useState('')

  const [now, setNow] = useState(() => Date.now())
  const timerRef = useRef(null)

  useEffect(() => {
    if (reservations.length === 0) return undefined
    timerRef.current = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timerRef.current)
  }, [reservations.length])

  const remainingSeconds = useMemo(() => {
    if (reservations.length === 0) return 0
    const earliest = Math.min(...reservations.map((r) => new Date(r.expiresAt).getTime()))
    return Math.max(0, Math.floor((earliest - now) / 1000))
  }, [reservations, now])

  const isExpired = reservations.length > 0 && remainingSeconds <= 0

  useEffect(() => {
    if (isExpired && timerRef.current) {
      clearInterval(timerRef.current)
      timerRef.current = null
    }
  }, [isExpired])

  if (!cart?.selections || cart.selections.length === 0) {
    return null
  }

  const handleCreateReservations = async (event) => {
    event.preventDefault()
    setError('')
    setLoading(true)

    try {
      const dni = purchaserDNI.trim()
      if (!dni) {
        setError('El DNI es obligatorio')
        setLoading(false)
        return
      }

      const created = await Promise.all(
        cart.selections.map((selection) =>
          apiClient
            .post('/reservations', {
              eventId: cart.eventId,
              ticketTypeId: selection.ticketTypeId,
              quantity: selection.quantity,
              purchaserDNI: dni,
            })
            .then((response) => response.data)
        )
      )
      setReservations(created)
    } catch (error) {
      setError(getErrorMessage(error))
    } finally {
      setLoading(false)
    }
  }

  const handlePay = async () => {
    if (reservations.length === 0 || isExpired) return
    setError('')
    setPayLoading(true)

    try {
      const response = await apiClient.post('/payments/create-preference', {
        reservationId: reservations[0].id,
      })
      const { checkoutUrl } = response.data
      window.location.href = checkoutUrl
    } catch (error) {
      setError(getErrorMessage(error))
      setPayLoading(false)
    }
  }

  const handleRestart = () => {
    navigate('/events', { replace: true })
  }

  if (isExpired) {
    return (
      <div className="checkout-page">
        <h1>Reserva expirada</h1>
        <p>Tu reserva ya no es valida. Las entradas fueron liberadas.</p>
        <button type="button" onClick={handleRestart}>
          Volver al catalogo
        </button>
      </div>
    )
  }

  if (reservations.length === 0) {
    return (
      <div className="checkout-page">
        <Link to="/events" className="back-link">
          ← Volver al catalogo
        </Link>

        <h1>Reserva tus entradas</h1>

        <section className="checkout-event-summary">
          {cart.eventImageUrl ? (
            <img
              src={cart.eventImageUrl}
              alt={cart.eventName}
              className="checkout-event-image"
            />
          ) : (
            <div className="checkout-event-image checkout-event-image-placeholder">
              Sin imagen
            </div>
          )}
          <div>
            <h2>{cart.eventName}</h2>
            <p>{formatEventDate(cart.eventDate)}</p>
            <p>{cart.eventLocation}</p>
          </div>
        </section>

        <section className="checkout-selections">
          <h2>Entradas seleccionadas</h2>
          {cart.selections.map((selection) => (
            <div key={selection.ticketTypeId} className="checkout-selection-row">
              <span>{selection.name}</span>
              <span>x {selection.quantity}</span>
              <span>{formatCurrency(selection.price * selection.quantity)}</span>
            </div>
          ))}
          <div className="checkout-total">
            <strong>Total: {formatCurrency(cart.totalPrice)}</strong>
          </div>
        </section>

        <form onSubmit={handleCreateReservations} className="checkout-form">
          <h2>Datos del comprador</h2>

          <div className="form-group">
            <label htmlFor="purchaserName">Nombre completo</label>
            <input
              id="purchaserName"
              type="text"
              value={purchaserName}
              onChange={(e) => setPurchaserName(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="purchaserEmail">Email</label>
            <input
              id="purchaserEmail"
              type="email"
              value={purchaserEmail}
              onChange={(e) => setPurchaserEmail(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="purchaserDNI">DNI</label>
            <input
              id="purchaserDNI"
              type="text"
              value={purchaserDNI}
              onChange={(e) => setPurchaserDNI(e.target.value)}
              required
              maxLength={50}
            />
          </div>

          {error && (
            <div className="error-container" role="alert">
              <p>{error}</p>
            </div>
          )}

          <button type="submit" className="reserve-button" disabled={loading}>
            {loading ? 'Reservando...' : 'Reservar entradas'}
          </button>
        </form>
      </div>
    )
  }

  return (
    <div className="checkout-page">
      <h1>Confirma tu reserva</h1>

      <div className="reservation-timer" role="timer" aria-live="polite">
        Tiempo restante: {formatCountdown(remainingSeconds)}
      </div>

      <section className="checkout-event-summary">
        <h2>{cart.eventName}</h2>
        <p>{formatEventDate(cart.eventDate)}</p>
        <p>{cart.eventLocation}</p>
      </section>

      <section className="checkout-selections">
        <h2>Resumen</h2>
        {reservations.map((reservation) => {
          const selection = cart.selections.find(
            (s) => s.ticketTypeId === reservation.ticketTypeId
          )
          return (
            <div key={reservation.id} className="checkout-selection-row">
              <span>{selection?.name || 'Entrada'}</span>
              <span>x {reservation.quantity}</span>
              <span>{formatCurrency((selection?.price || 0) * reservation.quantity)}</span>
            </div>
          )
        })}
        <div className="checkout-total">
          <strong>Total: {formatCurrency(cart.totalPrice)}</strong>
        </div>
      </section>

      {reservations.length > 1 && (
        <p className="checkout-note">
          Nota: el pago se procesa de a una reserva a la vez.
        </p>
      )}

      {error && (
        <div className="error-container" role="alert">
          <p>{error}</p>
        </div>
      )}

      <button
        type="button"
        className="pay-button"
        onClick={handlePay}
        disabled={payLoading}
      >
        {payLoading ? 'Preparando pago...' : 'Pagar con Mercado Pago'}
      </button>
    </div>
  )
}
