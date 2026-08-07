import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { useQueryClient } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { useAuth } from '../context/auth.js'
import { formatEventDate, formatCurrency } from '../lib/format.js'
import { getErrorMessage } from '../lib/apiError.js'
import { queryKeys } from '../lib/queryKeys.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import Badge from '../components/ui/Badge.jsx'
import IdentityDocumentInput from '../components/ui/IdentityDocumentInput.jsx'
import { validateDocument, cleanDocument, formatDocument } from '../utils/identityValidation.js'

function formatCountdown(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
}

const shakeAnim = {
  x: [0, -6, 6, -6, 6, 0],
  transition: { duration: 0.35 },
}

export default function Checkout() {
  const location = useLocation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
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
  const [confirmDNI, setConfirmDNI] = useState('')
  const [confirmDNIFocused, setConfirmDNIFocused] = useState(false)
  const [documentCountry, setDocumentCountry] = useState('AR')
  const [isEditing, setIsEditing] = useState(false)
  const [reservation, setReservation] = useState(null)
  const [loading, setLoading] = useState(false)
  const [payLoading, setPayLoading] = useState(false)
  const [error, setError] = useState('')
  const [shakeError, setShakeError] = useState(false)

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
    setShakeError(false)
    setLoading(true)

    try {
      const name = purchaserName.trim()
      if (!name) {
        setError('El nombre es obligatorio')
        setShakeError(true)
        setLoading(false)
        return
      }

      const email = purchaserEmail.trim()
      if (!email) {
        setError('El email es obligatorio')
        setShakeError(true)
        setLoading(false)
        return
      }

      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        setError('Formato de email inválido')
        setShakeError(true)
        setLoading(false)
        return
      }

      if (email !== confirmEmail.trim()) {
        setError('Los emails no coinciden')
        setShakeError(true)
        setLoading(false)
        return
      }

      const dni = purchaserDNI.trim()
      if (!dni) {
        setError('El DNI es obligatorio')
        setShakeError(true)
        setLoading(false)
        return
      }

      const docValidation = validateDocument(dni, documentCountry)
      if (!docValidation.valid) {
        setError(docValidation.error)
        setShakeError(true)
        setLoading(false)
        return
      }

      if (cleanDocument(dni) !== cleanDocument(confirmDNI)) {
        setError('Los DNIs no coinciden')
        setShakeError(true)
        setLoading(false)
        return
      }

      const response = reservation
        ? await apiClient.patch(`/reservations/${reservation.id}`, {
            purchaserEmail: purchaserEmail.trim(),
            purchaserDNI: dni,
            token: reservation.token,
          })
        : await apiClient.post('/reservations', {
            eventId: cart.eventId,
            ticketTypeId: selection.ticketTypeId,
            quantity: selection.quantity,
            purchaserEmail: purchaserEmail.trim(),
            confirmEmail: confirmEmail.trim(),
            purchaserDNI: dni,
          })
      setReservation(response.data)
      setIsEditing(false)

      // A new reservation holds stock → availability changed. Only invalidate
      // on create (PATCH only updates purchaser data, not stock).
      if (!reservation) {
        queryClient.invalidateQueries({ queryKey: queryKeys.events })
        queryClient.invalidateQueries({ queryKey: queryKeys.event(cart.eventId) })
      }
    } catch (error) {
      setError(getErrorMessage(error))
      setShakeError(true)
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

	const handleEditData = () => {
	    setIsEditing(true)
	  }

  const handleRestart = () => {
    // Reservation expired → held stock was released → availability changed.
    queryClient.invalidateQueries({ queryKey: queryKeys.events })
    queryClient.invalidateQueries({ queryKey: queryKeys.event(cart.eventId) })
    navigate('/events', { replace: true })
  }

  // ─── Expired state ──────────────────────────────────────────────────────

  if (isExpired) {
    return (
      <div className="max-w-lg mx-auto px-4 py-20 text-center">
        <GlassCard className="px-6 py-12">
          <h1 className="text-2xl font-display font-bold text-text-1 mb-3">
            Reserva expirada
          </h1>
          <p className="text-text-2 mb-6">
            Tu reserva ya no es valida. Las entradas fueron liberadas.
          </p>
          <Button variant="gradient" onClick={handleRestart}>
            Volver al catalogo
          </Button>
        </GlassCard>
      </div>
    )
  }

  // ─── Phase 1 — Reservation form ─────────────────────────────────────────

  if (!reservation || isEditing) {
    return (
      <AnimatePresence mode="wait">
        <motion.div
          key="phase1"
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -16 }}
          transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
          className="max-w-2xl mx-auto px-4 py-8"
        >
          <Link
            to="/events"
            className="inline-flex items-center gap-1 text-text-2 hover:text-text-1 mb-6 transition-colors"
          >
            ← Volver al catalogo
          </Link>

          <h1 className="text-3xl font-display font-bold text-text-1 mb-6">
            {isEditing ? 'Editar tus datos' : 'Reserva tus entradas'}
          </h1>

          {/* Event summary */}
          <GlassCard className="mb-6 p-6">
            <div className="flex gap-4">
              {cart.eventImageUrl ? (
                <img
                  src={cart.eventImageUrl}
                  alt={cart.eventName}
                  className="w-24 h-24 rounded-lg object-cover flex-shrink-0"
                />
              ) : (
                <div className="w-24 h-24 rounded-lg bg-surface-elevated flex items-center justify-center flex-shrink-0">
                  <span className="text-text-muted text-xs">Sin imagen</span>
                </div>
              )}
              <div className="min-w-0">
                <h2 className="font-heading font-semibold text-text-1 text-lg">
                  {cart.eventName}
                </h2>
                <p className="text-text-2 text-sm">{formatEventDate(cart.eventDate)}</p>
                <p className="text-text-2 text-sm">{cart.eventLocation}</p>
              </div>
            </div>

            <hr className="my-4 border-white/10" />

            <div className="flex justify-between items-center">
              <span className="text-text-2 text-sm">
                {selection.name} x {selection.quantity}
              </span>
              <span className="font-display font-bold text-brand-1 text-lg">
                Total: {formatCurrency(cart.totalPrice)}
              </span>
            </div>
          </GlassCard>

          {/* Purchaser form */}
          <motion.div
            animate={shakeError ? shakeAnim : {}}
            onAnimationComplete={() => setShakeError(false)}
          >
            <GlassCard className="p-6">
              <h2 className="text-xl font-heading font-semibold text-text-1 mb-4">
                Datos del comprador
              </h2>

              <form onSubmit={handleCreateReservation} className="space-y-4" noValidate>
                <div>
                  <label
                    htmlFor="purchaserName"
                    className="block text-sm font-medium text-text-2 mb-1"
                  >
                    Nombre completo
                  </label>
                  <input
                    id="purchaserName"
                    type="text"
                    value={purchaserName}
                    onChange={(e) => setPurchaserName(e.target.value)}
                    required
                    className="w-full px-4 py-2.5 bg-surface-elevated border border-white/10 rounded-lg
                      text-text-1 placeholder:text-text-muted
                      focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                      transition-all duration-200"
                  />
                </div>

                <div>
                  <label
                    htmlFor="purchaserEmail"
                    className="block text-sm font-medium text-text-2 mb-1"
                  >
                    Email
                  </label>
                  <input
                    id="purchaserEmail"
                    type="email"
                    value={purchaserEmail}
                    onChange={(e) => {
                      setPurchaserEmail(e.target.value)
                      setError('')
                    }}
                    onPaste={(e) => e.preventDefault()}
                    required
                    className="w-full px-4 py-2.5 bg-surface-elevated border border-white/10 rounded-lg
                      text-text-1 placeholder:text-text-muted
                      focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                      transition-all duration-200"
                  />
                </div>

                <div>
                  <label
                    htmlFor="confirmEmail"
                    className="block text-sm font-medium text-text-2 mb-1"
                  >
                    Confirmar email
                  </label>
                  <input
                    id="confirmEmail"
                    type="email"
                    value={confirmEmail}
                    onChange={(e) => {
                      setConfirmEmail(e.target.value)
                      setError('')
                    }}
                    onPaste={(e) => e.preventDefault()}
                    required
                    className="w-full px-4 py-2.5 bg-surface-elevated border border-white/10 rounded-lg
                      text-text-1 placeholder:text-text-muted
                      focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                      transition-all duration-200"
                  />
                </div>

                <IdentityDocumentInput
                  id="purchaserDNI"
                  label="DNI"
                  value={purchaserDNI}
                  onChange={(raw) => {
                    setPurchaserDNI(raw)
                    setError('')
                  }}
                  country={documentCountry}
                  onCountryChange={setDocumentCountry}
                  required
                />

                <div>
                  <label
                    htmlFor="confirmDNI"
                    className="block text-sm font-medium text-text-2 mb-1"
                  >
                    Confirmar DNI
                  </label>
                  <input
                    id="confirmDNI"
                    type="text"
                    inputMode="numeric"
                    value={
                      confirmDNIFocused
                        ? confirmDNI
                        : cleanDocument(confirmDNI)
                          ? formatDocument(cleanDocument(confirmDNI), documentCountry)
                          : confirmDNI
                    }
                    onChange={(e) => {
                      setConfirmDNI(e.target.value)
                      setError('')
                    }}
                    onFocus={() => setConfirmDNIFocused(true)}
                    onBlur={() => setConfirmDNIFocused(false)}
                    onPaste={(e) => e.preventDefault()}
                    required
                    className="w-full px-4 py-2.5 bg-surface-elevated border border-white/10 rounded-lg
                      text-text-1 placeholder:text-text-muted
                      focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                      transition-all duration-200"
                  />
                </div>

                {error && (
                  <div>
                    <Badge variant="error" className="px-4 py-2">
                      {error}
                    </Badge>
                  </div>
                )}

                <Button
                  type="submit"
                  variant="gradient"
                  size="lg"
                  loading={loading}
                  className="w-full"
                >
                  {loading ? 'Reservando...' : isEditing ? 'Guardar cambios' : 'Reservar entradas'}
                </Button>
              </form>
            </GlassCard>
          </motion.div>
        </motion.div>
      </AnimatePresence>
    )
  }

  // ─── Phase 2 — Confirmation ─────────────────────────────────────────────

  return (
    <div className="max-w-lg mx-auto px-4 py-12">
      <GlassCard className="text-center p-6">
        <h1 className="text-2xl font-display font-bold text-text-1 mb-2">
          Confirma tu reserva
        </h1>

        {/* Countdown timer */}
        <div className="inline-flex items-center gap-2 px-6 py-3 rounded-full glass-surface mb-6">
          <span className="text-text-2 text-sm">Tiempo restante:</span>
          <span
            className={`font-mono text-lg font-bold tabular-nums ${
              remainingSeconds <= 30 ? 'text-rose-400' : 'text-brand-1'
            }`}
            role="timer"
            aria-live="polite"
          >
            {formatCountdown(remainingSeconds)}
          </span>
        </div>

	        {/* Order summary — event */}
	        <div className="text-left space-y-2 mb-6">
	          <div className="flex justify-between text-text-2 text-sm">
	            <span>Evento</span>
	            <span className="text-text-1 text-right">{cart.eventName}</span>
	          </div>
	          <div className="flex justify-between text-text-2 text-sm">
	            <span>Fecha</span>
	            <span className="text-text-1">{formatEventDate(cart.eventDate)}</span>
	          </div>
	          <div className="flex justify-between text-text-2 text-sm">
	            <span>Ubicacion</span>
	            <span className="text-text-1">{cart.eventLocation}</span>
	          </div>
	          <hr className="my-3 border-white/10" />
	          <div className="flex justify-between text-text-2 text-sm">
	            <span>{selection.name} x {reservation.quantity}</span>
	            <span className="text-text-1">
	              {formatCurrency(selection.price * reservation.quantity)}
	            </span>
	          </div>
	          <div className="flex justify-between font-display font-bold text-text-1 text-lg pt-2 border-t border-white/10">
	            <span>Total:</span>
	            <span className="text-brand-1">{formatCurrency(cart.totalPrice)}</span>
	          </div>
	        </div>

	        {/* Purchaser data review */}
	        <div className="text-left space-y-2 mb-6">
	          <h2 className="text-lg font-heading font-semibold text-text-1 mb-3">
	            Datos del comprador
	          </h2>
	          <div className="flex justify-between text-text-2 text-sm">
	            <span>Nombre</span>
	            <span className="text-text-1">{purchaserName}</span>
	          </div>
	          <div className="flex justify-between text-text-2 text-sm">
	            <span>Email</span>
	            <span className="text-text-1">{purchaserEmail}</span>
	          </div>
	          <div className="flex justify-between text-text-2 text-sm">
	            <span>DNI</span>
	            <span className="text-text-1">{purchaserDNI}</span>
	          </div>
	        </div>

	        {error && (
	          <div className="mb-4">
	            <Badge variant="error" className="px-4 py-2">
	              {error}
	            </Badge>
	          </div>
	        )}

	        <div className="flex gap-3">
	          <Button
	            variant="secondary"
	            size="lg"
	            onClick={handleEditData}
	            className="flex-1"
	          >
	            Editar datos
	          </Button>
	          <Button
	            variant="gradient"
	            size="lg"
	            loading={payLoading}
	            onClick={handlePay}
	            disabled={isExpired}
	            className="flex-1"
	          >
	            {payLoading ? 'Preparando pago...' : 'Confirmar y proceder al pago'}
	          </Button>
	        </div>
      </GlassCard>
    </div>
  )
}
