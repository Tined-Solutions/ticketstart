import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import apiClient from '../api/client.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import { fadeIn } from '../lib/motion.js'

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

function formatDate(dateString) {
  if (!dateString) return ''
  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleDateString('es-AR', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
}

function formatCurrency(amount) {
  return Number(amount).toLocaleString('es-AR', {
    style: 'currency',
    currency: 'ARS',
    minimumFractionDigits: 2,
  })
}

export default function OrganizerEventMetrics() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [metrics, setMetrics] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const loadMetrics = useCallback(
    (controller) => {
      apiClient
        .get(`/metrics/events/${id}`, { signal: controller.signal })
        .then((response) => {
          if (controller.signal.aborted) return
          setMetrics(response.data)
          setError('')
          setLoading(false)
        })
        .catch((err) => {
          if (controller.signal.aborted) return
          if (err.response?.status === 404) {
            setError('404')
          } else {
            setError(getErrorMessage(err))
          }
          setLoading(false)
        })
    },
    [id],
  )

  useEffect(() => {
    const controller = new AbortController()
    loadMetrics(controller)
    return () => controller.abort()
  }, [loadMetrics])

  if (loading) {
    return (
      <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-[800px] mx-auto px-5 py-10">
        <GlassCard className="p-8 space-y-4">
          <Skeleton width="60%" height="36px" variant="text" className="mx-auto" />
          <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-6">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="flex flex-col gap-2">
                <Skeleton width="60%" height="14px" variant="text" />
                <Skeleton width="40%" height="28px" variant="text" />
              </div>
            ))}
          </div>
        </GlassCard>
      </motion.div>
    )
  }

  if (error === '404') {
    return (
      <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-[800px] mx-auto px-5 py-10">
        <GlassCard className="text-center py-12">
          <p className="text-text-2 mb-4">Evento no encontrado</p>
          <Button variant="secondary" onClick={() => navigate('/organizer/dashboard')}>
            Volver al dashboard
          </Button>
        </GlassCard>
      </motion.div>
    )
  }

  if (error) {
    return (
      <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-[800px] mx-auto px-5 py-10">
        <GlassCard className="text-center py-12" role="alert">
          <p className="text-text-1 mb-3">{error}</p>
          <Button variant="secondary" onClick={() => navigate('/organizer/dashboard')}>
            Volver al dashboard
          </Button>
        </GlassCard>
      </motion.div>
    )
  }

  return (
    <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-[800px] mx-auto px-5 py-10">
      <header className="mb-8">
        <h1 className="text-4xl font-display font-bold text-text-1 text-center mb-2">
          {metrics.eventName}
        </h1>
        <p className="text-text-2 text-center">Metricas del evento</p>
      </header>

      <GlassCard className="p-8 mb-6">
        <dl className="grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-6 m-0">
          <div className="flex flex-col gap-1">
            <dt className="text-sm font-medium text-text-2">Fecha del evento</dt>
            <dd className="text-text-1 m-0">{formatDate(metrics.eventDate)}</dd>
          </div>

          <div className="flex flex-col gap-1">
            <dt className="text-sm font-medium text-text-2">Entradas vendidas</dt>
            <dd className="text-2xl font-semibold text-text-1 font-mono m-0">{metrics.ticketsSold}</dd>
          </div>

          <div className="flex flex-col gap-1">
            <dt className="text-sm font-medium text-text-2">Ingresos totales</dt>
            <dd className="text-2xl font-semibold text-text-1 font-mono m-0">{formatCurrency(metrics.totalRevenue)}</dd>
          </div>

          <div className="flex flex-col gap-1">
            <dt className="text-sm font-medium text-text-2">Inventario restante</dt>
            <dd className="text-2xl font-semibold text-text-1 font-mono m-0">{metrics.remainingInventory}</dd>
          </div>

          <div className="flex flex-col gap-1">
            <dt className="text-sm font-medium text-text-2">Tickets escaneados</dt>
            <dd className="text-2xl font-semibold text-text-1 font-mono m-0">{metrics.ticketsScanned}</dd>
          </div>
        </dl>
      </GlassCard>

      <div className="flex justify-center">
        <Button variant="secondary" onClick={() => navigate('/organizer/dashboard')}>
          Volver al dashboard
        </Button>
      </div>
    </motion.div>
  )
}
