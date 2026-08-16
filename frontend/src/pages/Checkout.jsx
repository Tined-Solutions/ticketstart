import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence, useReducedMotion } from 'framer-motion'
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

const inputClass =
  'w-full px-4 py-2.5 bg-surface-elevated border border-white/10 rounded-lg ' +
  'text-text-1 placeholder:text-text-muted ' +
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:border-transparent ' +
  'transition-[border-color,box-shadow] duration-200'

export default function Checkout() {
  const location = useLocation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { user } = useAuth()
  const prefersReducedMotion = useReducedMotion()

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
  const [fieldErrors, setFieldErrors] = useState({})
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

  const clearFieldErrors = (...fields) => {
    setFieldErrors((prev) => {
      if (!fields.some((f) => f in prev)) return prev
      const next = { ...prev }
      for (const f of fields) delete next[f]
      return next
    })
  }

  function validatePurchaserForm() {
    const errors = {}
    const name = purchaserName.trim()
    const email = purchaserEmail.trim()
    const confirmEmailValue = confirmEmail.trim()
    const dni = purchaserDNI.trim()

    if (!name) {
      errors.purchaserName = 'El nombre es obligatorio'
    }

    if (!email) {
      errors.purchaserEmail = 'El email es obligatorio'
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      errors.purchaserEmail = 'Formato de email inválido'
    }

    if (email) {
      if (!confirmEmailValue) {
        errors.confirmEmail = 'El email es obligatorio'
      } else if (email !== confirmEmailValue) {
        errors.confirmEmail = 'Los emails no coinciden'
      }
    }

    if (!dni) {
      errors.purchaserDNI = 'El DNI es obligatorio'
    } else {
      const docValidation = validateDocument(dni, documentCountry)
      if (!docValidation.valid) {
        errors.purchaserDNI = docValidation.error
      }
    }

    if (dni && cleanDocument(dni) !== cleanDocument(confirmDNI)) {
      errors.confirmDNI = 'Los DNIs no coinciden'
    }

    return errors
  }

  function focusFirstError(errors) {
    const order = ['purchaserName', 'purchaserEmail', 'confirmEmail', 'purchaserDNI', 'confirmDNI']
    for (const key of order) {
      if (!errors[key]) continue
      const el = document.getElementById(key)
      if (el) {
        el.focus()
        return
      }
    }
  }

  if (!cart?.selection) {
    return null
  }

  const selection = cart.selection

  const handleCreateReservation = async (event) => {
    event.preventDefault()
    setError('')
    setShakeError(false)

    const errors = validatePurchaserForm()
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      setShakeError(true)
      focusFirstError(errors)
      return
    }

    setLoading(true)

    try {
      const email = purchaserEmail.trim()
      const dni = purchaserDNI.trim()

      const response = reservation
        ? await apiClient.patch(`/reservations/${reservation.id}`, {
            purchaserName: purchaserName.trim(),
            purchaserEmail: email,
            purchaserDNI: dni,
            token: reservation.token,
          })
        : await apiClient.post('/reservations', {
            eventId: cart.eventId,
            ticketTypeId: selection.ticketTypeId,
            quantity: selection.quantity,
            purchaserName: purchaserName.trim(),
            purchaserEmail: email,
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
            Tu reserva ya no es válida. Las entradas fueron liberadas.
          </p>
          <Button variant="gradient" onClick={handleRestart}>
            Volver al catálogo
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
          initial={prefersReducedMotion ? false : { opacity: 0, y: 16 }}
          animate={prefersReducedMotion ? {} : { opacity: 1, y: 0 }}
          exit={prefersReducedMotion ? {} : { opacity: 0, y: -16 }}
          transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
          className="max-w-2xl mx-auto px-4 py-8"
        >
          <Link
            to="/events"
            className="inline-flex items-center gap-1 text-text-2 hover:text-text-1 mb-6 transition-colors"
          >
            ← Volver al catálogo
          </Link>

          <p className="text-sm font-medium text-brand-1 mb-1" aria-hidden="true">
            Paso 1 de 2
          </p>
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
            animate={prefersReducedMotion ? {} : shakeError ? shakeAnim : {}}
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
                    name="purchaserName"
                    type="text"
                    value={purchaserName}
                    onChange={(e) => {
                      setPurchaserName(e.target.value)
                      clearFieldErrors('purchaserName')
                    }}
                    required
                    autoComplete="name"
                    aria-invalid={fieldErrors.purchaserName ? 'true' : undefined}
                    aria-describedby={fieldErrors.purchaserName ? 'purchaserName-error' : undefined}
                    className={inputClass}
                  />
                  {fieldErrors.purchaserName && (
                    <p id="purchaserName-error" role="alert" className="mt-1.5 text-sm text-danger">
                      {fieldErrors.purchaserName}
                    </p>
                  )}
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
                    name="purchaserEmail"
                    type="email"
                    value={purchaserEmail}
                    onChange={(e) => {
                      setPurchaserEmail(e.target.value)
                      setError('')
                      clearFieldErrors('purchaserEmail', 'confirmEmail')
                    }}
                    onPaste={(e) => e.preventDefault()}
                    required
                    autoComplete="email"
                    spellCheck={false}
                    aria-invalid={fieldErrors.purchaserEmail ? 'true' : undefined}
                    aria-describedby={fieldErrors.purchaserEmail ? 'purchaserEmail-error' : undefined}
                    className={inputClass}
                  />
                  {fieldErrors.purchaserEmail && (
                    <p id="purchaserEmail-error" role="alert" className="mt-1.5 text-sm text-danger">
                      {fieldErrors.purchaserEmail}
                    </p>
                  )}
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
                    name="confirmEmail"
                    type="email"
                    value={confirmEmail}
                    onChange={(e) => {
                      setConfirmEmail(e.target.value)
                      setError('')
                      clearFieldErrors('purchaserEmail', 'confirmEmail')
                    }}
                    onPaste={(e) => e.preventDefault()}
                    required
                    autoComplete="off"
                    spellCheck={false}
                    aria-invalid={fieldErrors.confirmEmail ? 'true' : undefined}
                    aria-describedby={fieldErrors.confirmEmail ? 'confirmEmail-error' : undefined}
                    className={inputClass}
                  />
                  {fieldErrors.confirmEmail && (
                    <p id="confirmEmail-error" role="alert" className="mt-1.5 text-sm text-danger">
                      {fieldErrors.confirmEmail}
                    </p>
                  )}
                </div>

                <IdentityDocumentInput
                  id="purchaserDNI"
                  name="purchaserDNI"
                  label="DNI"
                  value={purchaserDNI}
                  onChange={(raw) => {
                    setPurchaserDNI(raw)
                    setError('')
                    clearFieldErrors('purchaserDNI', 'confirmDNI')
                  }}
                  country={documentCountry}
                  onCountryChange={(c) => {
                    setDocumentCountry(c)
                    clearFieldErrors('purchaserDNI', 'confirmDNI')
                  }}
                  required
                  autoComplete="off"
                  error={fieldErrors.purchaserDNI}
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
                    name="confirmDNI"
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
                      clearFieldErrors('confirmDNI', 'purchaserDNI')
                    }}
                    onFocus={() => setConfirmDNIFocused(true)}
                    onBlur={() => setConfirmDNIFocused(false)}
                    onPaste={(e) => e.preventDefault()}
                    required
                    autoComplete="off"
                    aria-invalid={fieldErrors.confirmDNI ? 'true' : undefined}
                    aria-describedby={fieldErrors.confirmDNI ? 'confirmDNI-error' : undefined}
                    className={inputClass}
                  />
                  {fieldErrors.confirmDNI && (
                    <p id="confirmDNI-error" role="alert" className="mt-1.5 text-sm text-danger">
                      {fieldErrors.confirmDNI}
                    </p>
                  )}
                </div>

                {error && (
                  <div>
                    <Badge variant="error" className="px-4 py-2" role="alert">
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
                  {loading ? 'Reservando…' : isEditing ? 'Guardar cambios' : 'Reservar entradas'}
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
        <div className="inline-flex flex-col items-center gap-1 px-6 py-3 rounded-full glass-surface mb-6">
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
          {remainingSeconds <= 30 && (
            <span className="text-rose-400 text-sm font-medium">
              Quedan pocos segundos
            </span>
          )}
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
	            <span>Ubicación</span>
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
	            <Badge variant="error" className="px-4 py-2" role="alert">
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
	            {payLoading ? 'Preparando pago…' : 'Confirmar y proceder al pago'}
	          </Button>
	        </div>
      </GlassCard>
    </div>
  )
}
