import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import apiClient from '../api/client.js'
import { useAuth } from '../context/auth.js'
import { formatEventDate, formatCurrency } from '../lib/format.js'
import { getErrorMessage } from '../lib/apiError.js'

function formatCountdown(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
}

export default function Checkout() {
  const location = useLocation()
  const navigate = useNavigate()
  const { user } = useAuth()

  const cart = location.state

  useEffect(() => {
    if (!cart?.selection) {
      navigate('/events', { replace: true })
    }
  }, [cart, navigate])

  const [purchaserName, setPurchaserName] = useState(user?.name || '')
  const [purchaserEmail, setPurchaserEmail] = useState(user?.email || '')
  const [confirmEmail, setConfirmEmail] = useState('')
  const [purchaserDNI, setPurchaserDNI] = useState('')
  const [reservation, setReservation] = useState(null)
  const [loading, setLoading] = useState(false)
  const [payLoading, setPayLoading] = useState(false)
  const [error, setError] = useState('')

  const [now, setNow] = useState(() => Date.now())
  const timerRef = useRef(null)

  useEffect(() => {
    if (!reservation) return undefined
    timerRef.current = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timerRef.current)
  }, [reservation])

  const remainingSeconds = useMemo(() => {
    if (!reservation) return 0
    const expiresAt = new Date(reservation.expiresAt).getTime()
    return Math.max(0, Math.floor((expiresAt - now) / 1000))
  }, [reservation, now])

  const isExpired = reservation && remainingSeconds <= 0

  useEffect(() => {
    if (isExpired && timerRef.current) {
      clearInterval(timerRef.current)
      timerRef.current = null
    }
  }, [isExpired])

  if (!cart?.selection) {
    return null
  }

  const selection = cart.selection

  const handleCreateReservation = async (event) => {
    event.preventDefault()
    setError('')
    setLoading(true)

    try {
      if (purchaserEmail.trim() !== confirmEmail.trim()) {
        setError('Los emails no coinciden')
        setLoading(false)
        return
      }

      const dni = purchaserDNI.trim()
      if (!dni) {
        setError('El DNI es obligatorio')
        setLoading(false)
        return
      }

      const response = await apiClient.post('/reservations', {
        eventId: cart.eventId,
        ticketTypeId: selection.ticketTypeId,
        quantity: selection.quantity,
        purchaserEmail: purchaserEmail.trim(),
        confirmEmail: confirmEmail.trim(),
        purchaserDNI: dni,
      })
      setReservation(response.data)
    } catch (error) {
      setError(getErrorMessage(error))
    } finally {
      setLoading(false)
    }
  }

  const handlePay = async () => {
    if (!reservation || isExpired) return
    setError('')
    setPayLoading(true)

    try {
      const response = await apiClient.post('/payments/create-preference', {
        reservationId: reservation.id,
        token: reservation.token,
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

  if (!reservation) {
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
          <div className="checkout-selection-row">
            <span>{selection.name}</span>
            <span>x {selection.quantity}</span>
            <span>{formatCurrency(selection.price * selection.quantity)}</span>
          </div>
          <div className="checkout-total">
            <strong>Total: {formatCurrency(cart.totalPrice)}</strong>
          </div>
        </section>

        <form onSubmit={handleCreateReservation} className="checkout-form">
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
              onChange={(e) => { setPurchaserEmail(e.target.value); setError(''); }}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="confirmEmail">Confirmar email</label>
            <input
              id="confirmEmail"
              type="email"
              value={confirmEmail}
              onChange={(e) => { setConfirmEmail(e.target.value); setError(''); }}
              onPaste={(e) => e.preventDefault()}
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
        <div className="checkout-selection-row">
          <span>{selection.name}</span>
          <span>x {reservation.quantity}</span>
          <span>{formatCurrency(selection.price * reservation.quantity)}</span>
        </div>
        <div className="checkout-total">
          <strong>Total: {formatCurrency(cart.totalPrice)}</strong>
        </div>
      </section>

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
