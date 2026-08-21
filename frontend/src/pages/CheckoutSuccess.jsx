import { useEffect, useState, useRef, useCallback } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import apiClient from '../api/client.js'
import { queryKeys } from '../lib/queryKeys.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import Badge from '../components/ui/Badge.jsx'

function CheckmarkIcon() {
  return (
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
  )
}

function ClockIcon() {
  return (
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
  )
}

function ErrorIcon() {
  return (
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
  )
}

const stateConfig = {
  confirming: {
    icon: <ClockIcon />,
    title: 'Confirmando tu pago…',
    message: 'Espera un momento mientras verificamos tu pago.',
  },
  confirmed: {
    icon: <CheckmarkIcon />,
    title: '¡Pago confirmado!',
    message: 'Tus entradas fueron enviadas a tu email.',
    badgeVariant: 'success',
    badgeLabel: 'Confirmado',
  },
  pending: {
    icon: <ClockIcon />,
    title: 'Pago pendiente',
    message: 'Tu pago esta siendo procesado. Te notificaremos por email.',
    badgeVariant: 'warning',
    badgeLabel: 'Pendiente',
  },
  error: {
    icon: <ErrorIcon />,
    title: 'No pudimos confirmar el pago',
    message: 'Ocurrió un error al verificar tu pago.',
    badgeVariant: 'error',
    badgeLabel: 'Error',
  },
}

export default function CheckoutSuccess() {
  const [searchParams] = useSearchParams()
  const queryClient = useQueryClient()
  const preferenceId = searchParams.get('preference_id')
  const [state, setState] = useState(preferenceId ? 'confirming' : 'error')
  const [errorMsg, setErrorMsg] = useState('')
  const calledRef = useRef(false)

  const confirmPayment = useCallback(async () => {
    if (!preferenceId || calledRef.current) return
    calledRef.current = true

    try {
      const response = await apiClient.post('/payments/confirm', { preferenceId })

      if (response.data?.status === 'confirmed') {
        setState('confirmed')
        // Payment confirmed → tickets sold → availability changed. The affected
        // event id is unknown from preference_id, so invalidate every event
        // detail plus the catalog list.
        queryClient.invalidateQueries({ queryKey: queryKeys.events })
        queryClient.invalidateQueries({ queryKey: ['event'] })
      } else {
        setState('pending')
        setErrorMsg(response.data?.error || '')
      }
    } catch {
      setState('error')
      setErrorMsg('No se pudo conectar con el servidor. Reintentá o volvé a intentar en unos minutos.')
    }
  }, [preferenceId, queryClient])

  useEffect(() => {
    // Confirming the payment is a one-time side effect on mount that only
    // updates state after the async request resolves (not synchronously), so
    // the effect is a legitimate external-system sync.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    confirmPayment()
  }, [confirmPayment])

  const handleRetry = () => {
    calledRef.current = false
    setState('confirming')
    setErrorMsg('')
    confirmPayment()
  }

  const config = stateConfig[state]

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
          {config.badgeVariant && (
            <div className="mb-3">
              <Badge variant={config.badgeVariant}>{config.badgeLabel}</Badge>
            </div>
          )}

          <h1 className="text-2xl font-display font-bold text-text-1 mb-3">
            {config.title}
          </h1>

          <p className="text-text-2 mb-4 max-w-sm mx-auto text-sm leading-relaxed">
            {config.message}
          </p>
        </div>

        {errorMsg && (
          <p role="alert" className="text-text-muted text-xs mb-4 max-w-xs mx-auto">
            {errorMsg}
          </p>
        )}

        {state === 'error' && (
          <div className="mb-5 flex flex-col gap-3 items-center">
            <Button variant="secondary" onClick={handleRetry}>
              Reintentar
            </Button>
          </div>
        )}

        {state === 'confirmed' && (
          <p className="text-text-muted text-xs mb-6 max-w-xs mx-auto">
            Revisá tu casilla de correo (incluyendo spam) para encontrar tus entradas con los códigos QR.
          </p>
        )}

        <div className="flex flex-col gap-3 items-center">
          <Link to="/events">
            <Button variant={state === 'confirmed' ? 'gradient' : 'secondary'}>
              Volver al catálogo
            </Button>
          </Link>
          <Link to="/tickets/lookup">
            <Button variant="ghost" size="sm">
              Buscar mis entradas
            </Button>
          </Link>
        </div>
      </GlassCard>
    </motion.div>
  )
}
