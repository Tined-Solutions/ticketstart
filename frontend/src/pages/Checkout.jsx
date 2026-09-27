import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence, useReducedMotion } from 'framer-motion'
import { useQueryClient } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { useAuth } from '../context/auth.js'
import { getErrorMessage } from '../lib/apiError.js'
import { queryKeys } from '../lib/queryKeys.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import Badge from '../components/ui/Badge.jsx'
import EventSummaryTicket from '../components/events/EventSummaryTicket.jsx'
import IdentityDocumentInput from '../components/ui/IdentityDocumentInput.jsx'
import { validateDocument, cleanDocument, formatDocument } from '../utils/identityValidation.js'
import {
  buildCartSignature,
  CHECKOUT_RESERVATION_KEY,
  clearCheckoutReservation,
  loadCheckoutReservation,
  saveCheckoutReservation,
  updateCheckoutReservation,
} from '../lib/checkoutReservationStorage.js'

function formatCountdown(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
}

/**
 * Reads the raw stored entry's preferenceId, deliberately skipping the
 * expiry/signature validation of `loadCheckoutReservation`: when the buyer
 * returns from Mercado Pago the 10-minute hold may already have lapsed while
 * the payment is still in flight, and that is exactly when re-verifying
 * matters most. Silent null when there is no entry or storage fails.
 */
function readStoredPreferenceId() {
  try {
    const raw = sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)
    if (!raw) return null
    const entry = JSON.parse(raw)
    return entry && typeof entry === 'object' ? (entry.preferenceId ?? null) : null
  } catch {
    return null
  }
}

const shakeAnim = {
  x: [0, -6, 6, -6, 6, 0],
  transition: { duration: 0.35 },
}

const inputClass =
  'w-full px-4 py-2 bg-surface-elevated border border-white/10 rounded-lg ' +
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

  // A reservation persisted before a history navigation (Back/Forward remounts
  // this component and would otherwise lose the hold). The lazy initializer is
  // idempotent: the storage helper discards mismatched/expired/corrupt entries
  // itself, so a re-run (StrictMode) resolves to the same value.
  const [storedReservation] = useState(() =>
    cart?.selection
      ? loadCheckoutReservation({
          eventId: cart.eventId,
          ticketTypeId: cart.selection.ticketTypeId,
          quantity: cart.selection.quantity,
        })
      : null
  )

  // Return-from-Mercado-Pago verification state. Initialized to 'verifying'
  // when the restored entry already carries a preferenceId: a bfcache restore
  // or reload must never render an actionable pay button before re-checking
  // whether the payment went through (double-charge window).
  const [paymentReturn, setPaymentReturn] = useState(() =>
    storedReservation?.preferenceId ? 'verifying' : null
  )

  const [purchaserName, setPurchaserName] = useState(
    () => storedReservation?.purchaserName || user?.name || ''
  )
  const [purchaserEmail, setPurchaserEmail] = useState(
    () => storedReservation?.purchaserEmail || user?.email || ''
  )
  const [confirmEmail, setConfirmEmail] = useState(
    () => storedReservation?.purchaserEmail || ''
  )
  const [purchaserDNI, setPurchaserDNI] = useState(
    () => storedReservation?.purchaserDNI || ''
  )
  const [confirmDNI, setConfirmDNI] = useState(
    () => storedReservation?.purchaserDNI || ''
  )
  const [confirmDNIFocused, setConfirmDNIFocused] = useState(false)
  const [documentCountry, setDocumentCountry] = useState(
    () => storedReservation?.documentCountry || 'AR'
  )
  const [isEditing, setIsEditing] = useState(false)
  const [reservation, setReservation] = useState(() =>
    storedReservation
      ? {
          id: storedReservation.id,
          token: storedReservation.token,
          expiresAt: storedReservation.expiresAt,
          quantity: storedReservation.quantity,
        }
      : null
  )
  const [loading, setLoading] = useState(false)
  const [payLoading, setPayLoading] = useState(false)
  const [error, setError] = useState('')
  const [fieldErrors, setFieldErrors] = useState({})
  const [shakeError, setShakeError] = useState(false)

  const [now, setNow] = useState(() => Date.now())
  const timerRef = useRef(null)

  useEffect(() => {
    if (!reservation) return undefined
    // Sync the clock the moment the reservation starts. `now` was frozen at
    // page mount; the countdown must measure from reservation creation, not
    // from how long the page has been open (otherwise it starts at 11:00+).
    setNow(Date.now())
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
    if (isExpired) {
      // The hold is gone server-side → drop the persisted copy so a remount
      // cannot restore a dead reservation.
      clearCheckoutReservation()
      if (timerRef.current) {
        clearInterval(timerRef.current)
        timerRef.current = null
      }
    }
  }, [isExpired])

  // ─── Return-from-Mercado-Pago verification ──────────────────────────────
  // The pay button navigates away to Mercado Pago; on return (bfcache restore
  // or a reload that restores the stored hold) the payment must be re-checked
  // before the buyer can pay again.

  const mountVerifiedRef = useRef(false)

  const verifyPayment = useCallback(
    async (preferenceId) => {
      if (!preferenceId) return

      try {
        const response = await apiClient.post('/payments/confirm', { preferenceId })
        const { status, reason } = response.data || {}

        if (status === 'confirmed') {
          setPaymentReturn('confirmed')
          // Tickets were sold → availability changed. The affected event id is
          // unknown from the preference, so invalidate every event detail plus
          // the catalog list (mirrors CheckoutSuccess).
          clearCheckoutReservation()
          queryClient.invalidateQueries({ queryKey: queryKeys.events })
          queryClient.invalidateQueries({ queryKey: ['event'] })
        } else if (status === 'pending' && reason === 'payment_pending') {
          setPaymentReturn('pending')
        } else if (status === 'pending' && reason === 'no_payment') {
          setPaymentReturn('no_payment')
        } else {
          setPaymentReturn('unverified')
        }
      } catch {
        setPaymentReturn('unverified')
      }
    },
    [queryClient]
  )

  useEffect(() => {
    // One-shot on mount (StrictMode runs effects twice in dev). A stored
    // preferenceId means a payment attempt may already exist — verify before
    // offering the pay button again. `paymentReturn` already starts as
    // 'verifying' from the lazy initializer, so no sync setState here.
    if (mountVerifiedRef.current) return
    if (!storedReservation?.preferenceId) return
    mountVerifiedRef.current = true
    // One-time external-system sync on mount: state only changes after the
    // request resolves (same pattern as CheckoutSuccess).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    verifyPayment(storedReservation.preferenceId)
  }, [storedReservation, verifyPayment])

  useEffect(() => {
    const handlePageShow = (event) => {
      // bfcache restore: React state is frozen from before the redirect to
      // Mercado Pago, so the pay button may still be stuck on "Preparando
      // pago…" (payLoading frozen true). Reset it and re-verify the payment.
      if (!event.persisted) return
      setPayLoading(false)
      const preferenceId = readStoredPreferenceId()
      if (preferenceId) {
        setPaymentReturn('verifying')
        verifyPayment(preferenceId)
      }
    }

    window.addEventListener('pageshow', handlePageShow)
    return () => window.removeEventListener('pageshow', handlePageShow)
  }, [verifyPayment])

  const handleVerifyAgain = () => {
    const preferenceId = readStoredPreferenceId()
    if (preferenceId) {
      setPaymentReturn('verifying')
      verifyPayment(preferenceId)
    }
  }

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
      } else if (email.toLowerCase() !== confirmEmailValue.toLowerCase()) {
        errors.confirmEmail = 'Los emails no coinciden'
      }
    }

    if (!dni) {
      errors.purchaserDNI = 'El documento es obligatorio'
    } else {
      const docValidation = validateDocument(dni, documentCountry)
      if (!docValidation.valid) {
        errors.purchaserDNI = docValidation.error
      }
    }

    if (dni && cleanDocument(dni) !== cleanDocument(confirmDNI)) {
      errors.confirmDNI = 'Los documentos no coinciden'
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

  // Persist the hold so a Back/Forward remount can restore phase 2 instead of
  // creating a second reservation. Used by both create (POST) and edit (PATCH):
  // the edit only refreshes the purchaser fields, id/token/expiresAt are the
  // same reservation response in both cases.
  const persistReservation = (reservationData, purchaser) => {
    saveCheckoutReservation({
      signature: buildCartSignature({
        eventId: cart.eventId,
        ticketTypeId: selection.ticketTypeId,
        quantity: selection.quantity,
      }),
      id: reservationData.id,
      token: reservationData.token,
      expiresAt: reservationData.expiresAt,
      quantity: reservationData.quantity,
      purchaserName: purchaser.name,
      purchaserEmail: purchaser.email,
      purchaserDNI: purchaser.dni,
      documentCountry: purchaser.country,
    })
  }

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
      persistReservation(response.data, {
        name: purchaserName.trim(),
        email,
        dni,
        country: documentCountry,
      })

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
      const { checkoutUrl, preferenceId } = response.data
      if (preferenceId) {
        // Persist BEFORE navigating: on return (bfcache or F5) the page can
        // re-verify this payment instead of offering a second one while the
        // first may already be approved (double-charge window).
        updateCheckoutReservation({ preferenceId })
      }
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
    clearCheckoutReservation()
    // Reservation expired → held stock was released → availability changed.
    queryClient.invalidateQueries({ queryKey: queryKeys.events })
    queryClient.invalidateQueries({ queryKey: queryKeys.event(cart.eventId) })
    navigate('/events', { replace: true })
  }

  // ─── Payment return panels (verifying / confirmed / pending / unverified) ─

  // Rendered before the expired panel: a payment may be in flight — or already
  // approved — even when the 10-minute hold has lapsed.
  if (paymentReturn && paymentReturn !== 'no_payment') {
    const panel = {
      verifying: {
        badgeVariant: 'info',
        badgeLabel: 'Verificando',
        title: 'Verificando el estado de tu pago…',
        message: 'Espera un momento mientras consultamos Mercado Pago.',
      },
      confirmed: {
        badgeVariant: 'success',
        badgeLabel: 'Confirmado',
        title: '¡Pago confirmado!',
        message: 'Tus entradas fueron enviadas a tu email.',
        muted:
          'Revisá tu casilla de correo (incluyendo spam) para encontrar tus entradas con los códigos QR.',
      },
      pending: {
        badgeVariant: 'warning',
        badgeLabel: 'Pendiente',
        title: 'Pago pendiente',
        message: 'Tu pago está siendo procesado. Te notificaremos por email.',
        muted: 'No hace falta que pagues de nuevo.',
      },
      unverified: {
        badgeVariant: 'error',
        badgeLabel: 'Error',
        title: 'No pudimos verificar tu pago',
        message: 'No pudimos consultar el estado de tu pago. Reintentá en unos segundos.',
      },
    }[paymentReturn]

    const canRetry = paymentReturn === 'pending' || paymentReturn === 'unverified'

    return (
      <div className="relative -mt-16 bg-gradient-to-b from-cian/10 via-canvas to-amarillo/10">
        {/* Full-bleed gradient background like the FAQ / ticket lookup pages. It
            starts behind the translucent fixed navbar (-mt-16 + pt-28) so there
            is no white gap between the navbar and the page background. */}
        <div className="mx-auto flex min-h-[100svh] w-full max-w-lg flex-col items-center justify-center px-4 pb-16 pt-28 text-center sm:px-6">
          <GlassCard className="w-full px-6 py-10 sm:px-8">
            <div role="status">
              <div className="mb-3">
                <Badge variant={panel.badgeVariant}>{panel.badgeLabel}</Badge>
              </div>
              <h1 className="text-2xl font-display font-bold text-text-1 mb-3">{panel.title}</h1>
              <p className="text-text-2 mb-4 max-w-sm mx-auto text-sm leading-relaxed">
                {panel.message}
              </p>
              {panel.muted && (
                <p className="text-text-muted text-xs mb-4 max-w-xs mx-auto">{panel.muted}</p>
              )}
            </div>

            {canRetry && (
              <div className="mb-5 flex flex-col items-center">
                <Button variant="accent" onClick={handleVerifyAgain}>
                  Verificar de nuevo
                </Button>
              </div>
            )}

            {paymentReturn !== 'verifying' && (
              <div className="flex flex-col gap-3 items-center">
                <Link to="/events">
                  <Button variant={paymentReturn === 'confirmed' ? 'accent' : 'glass'}>
                    Volver al catálogo
                  </Button>
                </Link>
                <Link to="/tickets/lookup">
                  <Button variant="ghost" size="sm">
                    Buscar mis entradas
                  </Button>
                </Link>
              </div>
            )}
          </GlassCard>
        </div>
      </div>
    )
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
          <Button variant="accent" onClick={handleRestart}>
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
          className="bg-gradient-to-b from-purpura/15 via-transparent to-naranja/15"
        >
          <Link
            to="/events"
            className="inline-flex items-center gap-1 px-4 pt-6 text-text-2 hover:text-text-1 transition-colors sm:px-6"
          >
            ← Volver al catálogo
          </Link>

<div className="max-w-5xl mx-auto px-4 pt-4 pb-6">
          {/* Stepper — progress indicator for the 2-step flow, centered on the page */}
          <div className="flex flex-col items-center gap-2">
            <h3 className="text-sm font-semibold text-text-1">Paso 1 de 2</h3>
            <div className="flex space-x-3" aria-hidden="true">
              <span className="h-2 w-12 rounded-full bg-brand-1" />
              <span className="h-2 w-12 rounded-full bg-gris-oscuro/25" />
            </div>
          </div>
          <h1 className="mt-3 mb-4 text-center text-2xl font-display font-bold text-text-1">
            {isEditing ? 'Editar tus datos' : 'Reserva tus entradas'}
          </h1>

          {/* Two-column layout: event summary on the left, purchaser form on the
              right. Stacks to one column on small screens. */}
<div className="grid grid-cols-1 gap-6 lg:grid-cols-2 lg:items-start">
          {/* Event summary — shared ticket component (same card in step 1 and step 2) */}
          <EventSummaryTicket
            event={{
              name: cart.eventName,
              imageUrl: cart.eventImageUrl,
              date: cart.eventDate,
              location: cart.eventLocation,
            }}
            selectionName={selection.name}
            quantity={selection.quantity}
            totalPrice={cart.totalPrice}
          />

          {/* Purchaser form */}
          <motion.div
            animate={prefersReducedMotion ? {} : shakeError ? shakeAnim : {}}
            onAnimationComplete={() => setShakeError(false)}
          >
            <GlassCard
              className="p-5"
              style={{ boxShadow: '0 12px 32px rgba(74,74,74,0.16)', borderColor: 'rgba(74,74,74,0.3)' }}
            >
              <h2 className="text-lg font-heading font-semibold text-text-1 mb-3">
                Datos del comprador
              </h2>

              <form onSubmit={handleCreateReservation} className="space-y-3.5" noValidate>
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
                    {documentCountry === 'AR' ? 'Confirmar DNI' : 'Confirmar Cédula'}
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
                      setConfirmDNI(cleanDocument(e.target.value))
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

                <div className="flex justify-center pt-1">
                  <button
                    type="submit"
                    disabled={loading}
                    className="inline-flex w-full items-center justify-center gap-2 rounded-full border border-purpura/30 bg-purpura/15 px-6 py-2.5 text-sm font-semibold text-purpura-dark transition-colors duration-200 hover:bg-purpura/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                  >
                    {loading && (
                      <svg
                        className="animate-spin -ml-1 mr-2 h-4 w-4"
                        fill="none"
                        viewBox="0 0 24 24"
                        aria-hidden="true"
                      >
                        <circle
                          className="opacity-25"
                          cx="12"
                          cy="12"
                          r="10"
                          stroke="currentColor"
                          strokeWidth="4"
                        />
                        <path
                          className="opacity-75"
                          fill="currentColor"
                          d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                        />
                      </svg>
                    )}
                    {loading ? 'Reservando…' : isEditing ? 'Guardar cambios' : 'Reservar entradas'}
                  </button>
                </div>
              </form>
            </GlassCard>
          </motion.div>
          </div>
          </div>
        </motion.div>
      </AnimatePresence>
    )
  }

  // ─── Phase 2 — Confirmation ─────────────────────────────────────────────

  return (
    <div className="flex min-h-[calc(100svh-56px)] flex-col bg-gradient-to-b from-purpura/15 via-transparent to-naranja/15">
      <Link
        to="/events"
        className="inline-flex items-center gap-1 px-4 pt-6 text-text-2 hover:text-text-1 transition-colors sm:px-6"
      >
        ← Volver al catálogo
      </Link>

<div className="mx-auto flex w-full max-w-5xl flex-1 flex-col px-4 pt-3 pb-6">
        {/* Stepper — progress indicator for the 2-step flow, centered on the page */}
        <div className="flex flex-col items-center gap-2">
          <h3 className="text-sm font-semibold text-text-1">Paso 2 de 2</h3>
          <div className="flex space-x-3" aria-hidden="true">
            <span className="h-2 w-12 rounded-full bg-brand-1" />
            <span className="h-2 w-12 rounded-full bg-brand-1" />
          </div>
        </div>
        <h1 className="mt-3 mb-4 text-center text-2xl font-display font-bold text-text-1">
          Confirma tu reserva
        </h1>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 lg:flex-1 lg:items-stretch">
          {/* Left column: event ticket — shared component, never stretched (self-start) */}
          <EventSummaryTicket
            className="lg:self-start"
            event={{
              name: cart.eventName,
              imageUrl: cart.eventImageUrl,
              date: cart.eventDate,
              location: cart.eventLocation,
            }}
            selectionName={selection.name}
            quantity={reservation.quantity}
            totalPrice={cart.totalPrice}
          />

          {/* Right column: single glass card — countdown + purchaser data + actions */}
          <GlassCard className="flex flex-col p-5">
            {paymentReturn === 'no_payment' && (
              <div
                role="status"
                className="mb-4 rounded-lg bg-cian/15 px-4 py-2 text-sm text-cian-dark"
              >
                No registramos un pago todavía. Si no completaste el pago en Mercado Pago,
                podés intentarlo de nuevo.
              </div>
            )}
            {/* Countdown */}
            <div className="flex flex-col items-center">
              <p className="text-sm text-text-2">Tiempo restante</p>
              <span
                className={`font-display text-2xl font-bold tabular-nums ${
                  remainingSeconds <= 30 ? 'text-rose-400' : 'text-brand-1'
                }`}
                role="timer"
                aria-live="polite"
              >
                {formatCountdown(remainingSeconds)}
              </span>
              {remainingSeconds <= 30 && (
                <span className="mt-1 text-sm font-medium text-rose-400">
                  Quedan pocos segundos
                </span>
              )}
            </div>

            {/* Purchaser data review */}
            <div className="mt-4 border-t border-gris-oscuro/10 pt-4">
              <h2 className="mb-3 font-display text-base font-semibold text-text-1">
                Datos del comprador
              </h2>
              <div className="space-y-2 text-sm">
                <div className="flex items-center justify-between gap-3">
                  <span className="text-text-2">Nombre</span>
                  <span className="truncate font-semibold text-text-1">{purchaserName}</span>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className="text-text-2">Email</span>
                  <span className="truncate font-semibold text-text-1">{purchaserEmail}</span>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className="text-text-2">
                    {documentCountry === 'AR' ? 'DNI' : 'Cédula'}
                  </span>
                  <span className="truncate font-semibold text-text-1">{purchaserDNI}</span>
                </div>
              </div>
            </div>

            {/* Actions — custom buttons, no lift; soft violet primary + neutral ghost */}
            <div className="mt-auto flex flex-col gap-2.5 pt-4">
              <button
                type="button"
                onClick={handleEditData}
                className="inline-flex w-full items-center justify-center gap-2 rounded-full border border-gris-oscuro/20 bg-white/60 px-6 py-2 text-sm font-semibold text-text-2 transition-colors duration-200 hover:bg-gris-oscuro/10 hover:text-text-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2"
              >
                Editar datos
              </button>
              <button
                type="button"
                onClick={handlePay}
                disabled={isExpired || payLoading}
                className="inline-flex w-full items-center justify-center gap-2 rounded-full border border-purpura/30 bg-purpura/15 px-6 py-2 text-sm font-semibold text-purpura-dark transition-colors duration-200 hover:bg-purpura/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {payLoading && (
                  <svg
                    className="animate-spin h-4 w-4"
                    fill="none"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                    />
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                    />
                  </svg>
                )}
                {payLoading ? 'Preparando pago…' : 'Confirmar y proceder al pago'}
              </button>
            </div>
            {error && (
              <div className="mt-4">
                <Badge variant="error" className="px-4 py-2 w-full" role="alert">
                  {error}
                </Badge>
              </div>
            )}
          </GlassCard>
        </div>
      </div>
    </div>
  )
}
