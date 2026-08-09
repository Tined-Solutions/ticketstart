import { Link, useSearchParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import Badge from '../components/ui/Badge.jsx'

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
    linkTo: '/events',
    linkLabel: '← Volver al catálogo',
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
    linkTo: '/events',
    linkLabel: '← Volver al catálogo',
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
    linkTo: '/events',
    linkLabel: '← Volver al catálogo',
  },
  unknown: {
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
    title: 'Resultado del pago',
    message: 'No pudimos determinar el estado del pago. Si ya pagaste, tus entradas serán enviadas a tu email en los próximos minutos.',
    badgeVariant: 'info',
    badgeLabel: 'Desconocido',
    linkTo: '/events',
    linkLabel: '← Volver al catálogo',
  },
}

function resolveStatus(searchParams) {
  const raw = searchParams.get('status')
  const normalized = (raw || '').toLowerCase()

  if (normalized === 'approved' || normalized === 'success') return 'success'
  if (normalized === 'pending' || normalized === 'in_process') return 'pending'
  if (normalized === 'failure' || normalized === 'rejected') return 'error'
  return 'unknown'
}

export default function CheckoutReturn() {
  const [searchParams] = useSearchParams()
  const status = resolveStatus(searchParams)
  const paymentId = searchParams.get('payment_id')
  const externalReference = searchParams.get('external_reference')

  const config = statusConfig[status]

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
      className="max-w-md mx-auto px-4 py-16"
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

        {paymentId && (
          <p className="text-text-muted text-xs mb-1 font-mono">
            ID de pago: <code>{paymentId}</code>
          </p>
        )}
        {externalReference && (
          <p className="text-text-muted text-xs mb-4 font-mono">
            Referencia: <code>{externalReference}</code>
          </p>
        )}

        <p className="text-text-muted text-xs mb-6 max-w-xs mx-auto">
          Revisá tu casilla de correo (incluyendo spam) para encontrar tus entradas con los códigos QR.
        </p>

        <Link to={config.linkTo}>
          <Button variant={status === 'success' ? 'gradient' : 'secondary'}>
            {config.linkLabel}
          </Button>
        </Link>
      </GlassCard>
    </motion.div>
  )
}
