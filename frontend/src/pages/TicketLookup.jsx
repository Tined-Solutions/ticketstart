import { useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Turnstile } from '@marsidev/react-turnstile'
import apiClient from '../api/client.js'
import { formatEventDate } from '../lib/format.js'
import { getErrorMessage } from '../lib/apiError.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import EmptyState from '../components/ui/EmptyState.jsx'
import Badge from '../components/ui/Badge.jsx'
import Button from '../components/Button.jsx'
import IdentityDocumentInput from '../components/ui/IdentityDocumentInput.jsx'
import { validateDocument, cleanDocument } from '../utils/identityValidation.js'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function validateEmail(email) {
  if (!email.trim()) return 'El email es obligatorio'
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email))
    return 'El formato del email no es valido'
  return ''
}

// ---------------------------------------------------------------------------
// Ticket card (info-only — one card per event, ticket types with quantities)
// ---------------------------------------------------------------------------

function TicketCard({ group }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
    >
      <GlassCard className="relative p-4 md:p-6">
        <h3 className="font-display font-semibold text-lg text-gris-oscuro mb-2">
          {group.eventName}
        </h3>
        <p className="text-text-2 text-sm">{formatEventDate(group.eventDate)}</p>
        <p className="text-text-2 text-sm mb-3">{group.eventLocation}</p>

        <hr className="border-gris-oscuro/10 my-3" />

        <ul className="space-y-1.5">
          {group.types.map((type) => (
            <li key={type.ticketType}>
              <span className="font-semibold text-gris-oscuro">
                {type.quantity}
              </span>{' '}
              <span className="text-text-2 text-sm">{type.ticketType}</span>
            </li>
          ))}
        </ul>
      </GlassCard>
    </motion.div>
  )
}

function TicketCardSkeleton() {
  return (
    <GlassCard className="p-4 md:p-6">
      <div className="space-y-3">
        <div className="flex justify-between">
          <Skeleton width="60%" height="20px" variant="text" />
          <Skeleton width="60px" height="20px" variant="rectangular" className="rounded-full" />
        </div>
        <Skeleton width="40%" height="14px" variant="text" />
        <Skeleton width="30%" height="14px" variant="text" />
        <Skeleton width="100%" height="1px" variant="text" />
        <div className="flex justify-between pt-2">
          <Skeleton width="30%" height="14px" variant="text" />
          <Skeleton width="50px" height="14px" variant="text" />
        </div>
      </div>
    </GlassCard>
  )
}

// ---------------------------------------------------------------------------
// Page component
// ---------------------------------------------------------------------------

export default function TicketLookup() {
  // Lookup form state
  const [email, setEmail] = useState('')
  const [dni, setDni] = useState('')
  const [documentCountry, setDocumentCountry] = useState('AR')
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
  const [turnstileToken, setTurnstileToken] = useState('')

  // -- Lookup -----------------------------------------------------------

  function validateDni() {
    const result = validateDocument(dni, documentCountry)
    if (!result.valid) return result.error
    return ''
  }

  function validateLookupForm() {
    return {
      email: validateEmail(email),
      dni: validateDni(),
    }
  }

  async function handleLookupSubmit(e) {
    e.preventDefault()

    const formErrors = validateLookupForm()
    const hasErrors = formErrors.email || formErrors.dni
    setErrors(formErrors)
    if (hasErrors) return

    setLoading(true)
    setError('')
    setTickets(null)

    try {
      const response = await apiClient.get('/tickets/lookup', {
        params: { email: email.trim(), dni: cleanDocument(dni) },
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
        turnstileToken: turnstileToken,
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

  // -- Derived: group response items by event (one card per event) ----------

  // Group by event name + date; each group carries the info-only fields and a
  // list of ticket types with their quantities.
  const groupedByEvent = tickets
    ? tickets.reduce((groups, item) => {
        const groupKey = `${item.eventName}|${item.eventDate}`
        const existing = groups.find(
          (g) => `${g.eventName}|${g.eventDate}` === groupKey
        )
        if (existing) {
          existing.types.push({
            ticketType: item.ticketType,
            quantity: item.quantity,
          })
        } else {
          groups.push({
            eventName: item.eventName,
            eventDate: item.eventDate,
            eventLocation: item.eventLocation,
            types: [{ ticketType: item.ticketType, quantity: item.quantity }],
          })
        }
        return groups
      }, [])
    : []

  // Total count is the SUM of quantities, not the number of response items.
  const totalTickets = tickets
    ? tickets.reduce((sum, item) => sum + item.quantity, 0)
    : 0

  // -- Render -----------------------------------------------------------

  return (
    <div className="relative -mt-16 bg-gradient-to-b from-cian/10 via-canvas to-amarillo/10">
      {/* Gradient background identical to the "Eventos destacados" section on
          the home page. It starts at the very top, behind the fixed navbar
          (which is translucent), so there is no white gap between the navbar
          and the page background. Applied directly on the container (no
          negative z-index) so it paints above the white body background. */}
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
        className="relative max-w-3xl mx-auto px-4 sm:px-6 pt-28 pb-12 space-y-10"
      >
      {/* ── Lookup section ──────────────────────────────────────────── */}

      <section>
        <header className="text-center mb-6">
          <h1 className="text-3xl font-display font-bold text-gris-oscuro mb-2">
            Mis Entradas
          </h1>
          <p className="text-text-2">
            Recuperá tus entradas y solicitá que te las reenvíen a tu email.
          </p>
        </header>

        <GlassCard className="p-4 md:p-6">
          <header className="mb-4">
            <h2 className="text-xl font-display font-bold text-gris-oscuro mb-1">
              Buscar mis entradas
            </h2>
            <p className="text-text-2 text-sm">
              Ingresa tu email y tu DNI para recuperar tus entradas
            </p>
          </header>
          <form onSubmit={handleLookupSubmit} noValidate className="space-y-4">
            <div>
              <label htmlFor="lookup-email" className="sr-only">
                Email
              </label>
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
                className={`w-full px-4 py-2.5 bg-white/60 border rounded-lg backdrop-blur-sm
                  text-gris-oscuro placeholder:text-text-muted
                  focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                  transition-all duration-200
                  disabled:opacity-60 disabled:cursor-not-allowed
                  ${errors.email ? 'border-rose-400' : 'border-gris-oscuro/15'}`}
              />
              {errors.email && (
                <p
                  className="text-rose-600 text-xs mt-1"
                  role="alert"
                >
                  {errors.email}
                </p>
              )}
            </div>

            <div>
              <IdentityDocumentInput
                id="lookup-dni"
                label="DNI"
                value={dni}
                onChange={(raw) => {
                  setDni(raw)
                  if (errors.dni) setErrors((prev) => ({ ...prev, dni: '' }))
                }}
                country={documentCountry}
                onCountryChange={(country) => {
                  setDocumentCountry(country)
                  if (errors.dni) setErrors((prev) => ({ ...prev, dni: '' }))
                }}
                disabled={loading}
              />
              {errors.dni && (
                <p
                  className="text-rose-600 text-xs mt-1"
                  role="alert"
                >
                  {errors.dni}
                </p>
              )}
            </div>

            <div className="flex justify-center pt-1">
              <Button
                type="submit"
                variant="glass"
                size="md"
                loading={loading}
              >
                {loading ? 'Buscando...' : 'Buscar entradas'}
              </Button>
            </div>
          </form>
        </GlassCard>

        {/* Lookup error */}
        {error && (
          <div className="mt-4">
            <GlassCard className="text-center px-4 py-6 md:px-6">
              <p className="text-text-1 mb-3">{error}</p>
              <Button variant="glass" onClick={handleClearLookupError}>
                Reintentar
              </Button>
            </GlassCard>
          </div>
        )}

        {/* Loading skeleton */}
        {loading && (
          <div className="mt-6 space-y-4">
            {Array.from({ length: 2 }).map((_, i) => (
              <TicketCardSkeleton key={i} />
            ))}
          </div>
        )}

        {/* Empty results */}
        {tickets !== null && !error && tickets.length === 0 && (
          <motion.div
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            className="mt-6"
          >
            <EmptyState
              icon="🎫"
              title="No se encontraron entradas"
              description="No se encontraron entradas con ese email y DNI. Verifica que los datos sean correctos."
              action={
                <Link to="/events">
                  <Button variant="glass">
                    Ver eventos{' '}
                    <span aria-hidden="true" className="text-purpura-dark transition-transform duration-300 group-hover:translate-x-1 motion-reduce:transition-none">→</span>
                  </Button>
                </Link>
              }
            />
          </motion.div>
        )}

        {/* Ticket results */}
        {tickets && tickets.length > 0 && (
          <div className="mt-6">
            <h2 className="text-xl font-display font-bold text-gris-oscuro mb-4">
              {totalTickets === 1
                ? '1 entrada encontrada'
                : `${totalTickets} entradas encontradas`}
            </h2>
            <div className="space-y-4">
              {groupedByEvent.map((group) => (
                <TicketCard
                  key={`${group.eventName}|${group.eventDate}`}
                  group={group}
                />
              ))}
            </div>
          </div>
        )}
      </section>

      {/* ── Resend section ──────────────────────────────────────────── */}

      <section>
        <GlassCard className="p-4 md:p-6">
          <header className="mb-4">
            <h2 className="text-xl font-display font-bold text-gris-oscuro mb-1">
              Reenviar entradas
            </h2>
            <p className="text-text-2 text-sm">
              Si no encuentras tus entradas, podemos reenviartelas por email
            </p>
          </header>

          <form onSubmit={handleResendSubmit} noValidate className="space-y-4">
            <div>
              <label htmlFor="resend-email" className="sr-only">
                Email
              </label>
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
                className={`w-full px-4 py-2.5 bg-white/60 border rounded-lg backdrop-blur-sm
                  text-gris-oscuro placeholder:text-text-muted
                  focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                  transition-all duration-200
                  disabled:opacity-60 disabled:cursor-not-allowed
                  ${resendErrors.email ? 'border-rose-400' : 'border-gris-oscuro/15'}`}
              />
              {resendErrors.email && (
                <p
                  className="text-rose-600 text-xs mt-1"
                  role="alert"
                >
                  {resendErrors.email}
                </p>
              )}
            </div>

            {/* Turnstile CAPTCHA */}
            <div>
              <Turnstile
                siteKey={import.meta.env.VITE_TURNSTILE_SITE_KEY || '1x00000000000000000000AA'}
                options={{ theme: 'dark', size: 'invisible' }}
                onSuccess={(token) => setTurnstileToken(token)}
                onError={() => setResendError('CAPTCHA verification failed. Please try again.')}
                onExpire={() => setTurnstileToken('')}
              />
              {turnstileToken && (
                <p className="text-success text-xs mt-2">✓ Verified</p>
              )}
            </div>

            <div className="flex justify-center pt-1">
              <Button
                type="submit"
                variant="glass"
                size="md"
                loading={resendLoading}
                disabled={resendLoading || !turnstileToken}
              >
                {resendLoading ? 'Enviando...' : 'Reenviar entradas'}
              </Button>
            </div>
          </form>

          {/* Resend feedback */}
          {resendMessage && (
            <div className="mt-4">
              <Badge variant="success" className="w-full justify-center px-4 py-2">
                {resendMessage}
              </Badge>
            </div>
          )}

          {resendError && (
            <div className="mt-4">
              <Badge variant="error" className="w-full justify-center px-4 py-2">
                {resendError}
              </Badge>
            </div>
          )}
        </GlassCard>
      </section>
      </motion.div>
    </div>
  )
}
