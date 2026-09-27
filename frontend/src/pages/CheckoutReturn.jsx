import { useEffect } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import Badge from '../components/ui/Badge.jsx'
import { clearCheckoutReservation } from '../lib/checkoutReservationStorage.js'

const statusConfig = {
  success: {
    icon: (
      <svg className="w-16 h-16 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <motion.path
          initial={{ pathLength: 0 }}
          animate={{ pathLength: 1 }}
          transition={{ duration: 0.6, delay: 0.2, ease: [0.4, 0, 0.2, 1] }}
          strokeLinecap="round"
          strokeLinejoin="round"
          d="M5 13l4 4L19 7"
        />
      </svg>
    ),
    title: '¡Pago confirmado!',
    message: 'Si la compra fue exitosa, recibirás un email con tus entradas en la casilla indicada.',
    badgeVariant: 'success',
    badgeLabel: 'Exitoso',
  },
  pending: {
    icon: (
      <motion.svg
        className="w-16 h-16 text-amber-400"
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
        strokeWidth={2}
        animate={{ rotate: 360 }}
        transition={{ duration: 8, repeat: Infinity, ease: 'linear' }}
      >
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
      </motion.svg>
    ),
    title: 'Pago pendiente',
    message: 'Te avisaremos cuando se confirme.',
    badgeVariant: 'warning',
    badgeLabel: 'Pendiente',
  },
  error: {
    icon: (
      <motion.svg
        className="w-16 h-16 text-rose-400"
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
        strokeWidth={2}
        initial={{ scale: 0 }}
        animate={{ scale: 1 }}
        transition={{ duration: 0.4, delay: 0.2, ease: [0, 0.6, 0.2, 1] }}
      >
        <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
      </motion.svg>
    ),
    title: 'Pago rechazado',
    message: 'El pago fue rechazado. Intenta nuevamente.',
    badgeVariant: 'error',
    badgeLabel: 'Rechazado',
  },
  incomplete: {
    icon: (
      <motion.svg
        className="w-16 h-16 text-text-muted"
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
        strokeWidth={2}
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ duration: 0.4 }}
      >
        <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </motion.svg>
    ),
    title: 'No completaste el pago',
    message: (
      <>
        No se realizó ningún cobro.{' '}
        <br />
        Podés intentar de nuevo cuando quieras.
      </>
    ),
    badgeVariant: 'info',
    badgeLabel: 'No completado',
  },
}

// Mercado Pago may append a literal "null" (or nothing at all) to the back
// URL. Both mean "absent", not a payment result, so they must not be read as
// a rejection.
function normalizeParam(value) {
  const normalized = (value || '').trim().toLowerCase()
  return normalized === 'null' ? '' : normalized
}

function resolveStatus(searchParams) {
  // MP's status is the primary signal on every back URL: approved/success →
  // success, pending/in_process → pending, rejected → error. A failure return
  // WITHOUT status intentionally falls through to `incomplete`: MP only omits
  // it when the flow was not completed (cancel/abandonment), while a real
  // rejection always arrives as status=rejected.
  const status = normalizeParam(searchParams.get('status'))

  if (status === 'approved' || status === 'success') return 'success'
  if (status === 'pending' || status === 'in_process') return 'pending'
  if (status === 'rejected') return 'error'

  // MP may omit status entirely (e.g. abandoned flow). The origin marker is
  // our own param and only used as a fallback — it never shadows MP's status.
  const origin = normalizeParam(searchParams.get('origin'))
  if (origin === 'pending') return 'pending'

  return 'incomplete'
}

export default function CheckoutReturn() {
  const [searchParams] = useSearchParams()

  // Any return from Mercado Pago (success, failure or pending) ends the local
  // checkout session: the reservation may already be paid or have a payment in
  // flight, so the stored copy must never be resurrected (double-payment risk).
  useEffect(() => {
    clearCheckoutReservation()
  }, [])

  const status = resolveStatus(searchParams)
  const eventId = normalizeParam(searchParams.get('event')) || null
  const config = statusConfig[status]
  const canRetry = status === 'error' || status === 'incomplete'

  return (
    <div className="flex min-h-[calc(100svh-56px)] items-center justify-center bg-gradient-to-b from-purpura/15 via-transparent to-naranja/15 px-4 py-12">
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
        className="w-full max-w-md"
      >
        <GlassCard className="text-center py-10">
          <motion.div
            className="flex justify-center mb-6"
            initial={{ scale: 0 }}
            animate={{ scale: 1 }}
            transition={{ duration: 0.5, delay: 0.1, ease: [0, 0.6, 0.2, 1] }}
          >
            {config.icon}
          </motion.div>

          <div role="status">
            <div className="mb-3">
              <Badge variant={config.badgeVariant}>
                {config.badgeLabel}
              </Badge>
            </div>

            <h1 className="text-2xl font-display font-bold text-text-1 mb-3">
              {config.title}
            </h1>

            <p className="text-text-2 mb-6 max-w-sm mx-auto text-sm leading-relaxed">
              {config.message}
            </p>
          </div>

          {status === 'success' && (
            <p className="text-text-muted text-xs mb-6 max-w-xs mx-auto">
              Revisá tu casilla de correo (incluyendo spam) para encontrar tus entradas con los códigos QR.
            </p>
          )}

          <div className="flex flex-col gap-3 items-center">
            {canRetry && eventId && (
              <Link to={`/events/${eventId}`}>
                <Button variant="accent">
                  Reintentar pago
                </Button>
              </Link>
            )}
            <Link to="/events">
              <Button variant="glass" size={canRetry ? 'sm' : 'md'}>
                Volver al catálogo
              </Button>
            </Link>
          </div>
        </GlassCard>
      </motion.div>
    </div>
  )
}
