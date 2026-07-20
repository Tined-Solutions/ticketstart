import { useState } from 'react'
import { Link } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { Turnstile } from '@marsidev/react-turnstile'
import apiClient from '../api/client.js'
import { formatEventDate, formatCurrency } from '../lib/format.js'
import { getErrorMessage } from '../lib/apiError.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import EmptyState from '../components/ui/EmptyState.jsx'
import Badge from '../components/ui/Badge.jsx'
import Button from '../components/Button.jsx'

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
// Ticket card (info-only — no QR, no download, no print)
// ---------------------------------------------------------------------------

function TicketCard({ ticket }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
    >
      <GlassCard className="relative p-4 md:p-6">
        <div className="absolute top-3 right-3">
          <Badge variant={ticket.isUsed ? 'error' : 'success'}>
            {ticket.isUsed ? 'Usada' : 'Valida'}
          </Badge>
        </div>

        <h3 className="font-display font-semibold text-lg text-text-1 mb-2 pr-20">
          {ticket.eventName}
        </h3>
        <p className="text-text-2 text-sm">{formatEventDate(ticket.eventDate)}</p>
        <p className="text-text-2 text-sm mb-3">{ticket.eventLocation}</p>

        <hr className="border-white/10 my-3" />

        <div className="flex justify-between items-center">
          <span className="text-text-2 text-sm">{ticket.ticketTypeName}</span>
          <span className="font-semibold text-brand-1">{formatCurrency(ticket.price)}</span>
        </div>

        {ticket.quantity !== undefined && ticket.quantity !== null && (
          <p className="text-text-muted text-xs mt-2">
            Cantidad: {ticket.quantity}
          </p>
        )}

        {ticket.isUsed && ticket.usedAt && (
          <p className="text-text-muted text-xs mt-1 ticket-used-at">
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

  // -- Render -----------------------------------------------------------

  return (
    <motion.div
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
      className="max-w-3xl mx-auto px-4 sm:px-6 py-8 space-y-10"
    >
      {/* ── Lookup section ──────────────────────────────────────────── */}

      <section>
        <header className="text-center mb-6">
          <h1 className="text-3xl font-display font-bold text-text-1 mb-2">
            Buscar mis entradas
          </h1>
          <p className="text-text-2">
            Ingresa tu email para recuperar tus entradas
          </p>
        </header>

        <GlassCard className="p-4 md:p-6">
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
                className={`w-full px-4 py-2.5 bg-surface-elevated border rounded-lg
                  text-text-1 placeholder:text-text-muted
                  focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                  transition-all duration-200
                  disabled:opacity-60 disabled:cursor-not-allowed
                  ${errors.email ? 'border-rose-400' : 'border-white/10'}`}
              />
              {errors.email && (
                <p
                  className="text-rose-400 text-xs mt-1"
                  role="alert"
                >
                  {errors.email}
                </p>
              )}
            </div>

            <Button
              type="submit"
              variant="gradient"
              size="lg"
              loading={loading}
              className="w-full"
            >
              {loading ? 'Buscando...' : 'Buscar entradas'}
            </Button>
          </form>
        </GlassCard>

        {/* Lookup error */}
        {error && (
          <div className="mt-4">
            <GlassCard className="text-center px-4 py-6 md:px-6">
              <p className="text-text-1 mb-3">{error}</p>
              <Button variant="secondary" onClick={handleClearLookupError}>
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
              description="No se encontraron entradas con ese email. Verifica que el email sea correcto."
              action={
                <Link to="/events">
                  <Button variant="secondary">Ver eventos</Button>
                </Link>
              }
            />
          </motion.div>
        )}

        {/* Ticket results */}
        {tickets && tickets.length > 0 && (
          <div className="mt-6">
            <h2 className="text-xl font-heading font-semibold text-text-1 mb-4">
              {tickets.length === 1
                ? '1 entrada encontrada'
                : `${tickets.length} entradas encontradas`}
            </h2>
            <div className="space-y-4">
              {tickets.map((ticket) => (
                <TicketCard key={ticket.id} ticket={ticket} />
              ))}
            </div>
          </div>
        )}
      </section>

      {/* ── Resend section ──────────────────────────────────────────── */}

      <section>
        <GlassCard className="p-4 md:p-6">
          <header className="mb-4">
            <h2 className="text-xl font-heading font-semibold text-text-1 mb-1">
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
                className={`w-full px-4 py-2.5 bg-surface-elevated border rounded-lg
                  text-text-1 placeholder:text-text-muted
                  focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                  transition-all duration-200
                  disabled:opacity-60 disabled:cursor-not-allowed
                  ${resendErrors.email ? 'border-rose-400' : 'border-white/10'}`}
              />
              {resendErrors.email && (
                <p
                  className="text-rose-400 text-xs mt-1"
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

            <Button
              type="submit"
              variant="secondary"
              size="lg"
              loading={resendLoading}
              disabled={resendLoading || !turnstileToken}
              className="w-full"
            >
              {resendLoading ? 'Enviando...' : 'Reenviar entradas'}
            </Button>
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
  )
}
