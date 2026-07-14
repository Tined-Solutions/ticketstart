import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import apiClient from '../api/client.js'

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
      <div className="metrics-page">
        <div className="metrics-loading">
          <p>Cargando metricas...</p>
        </div>
      </div>
    )
  }

  if (error === '404') {
    return (
      <div className="metrics-page">
        <div className="empty-state">
          <p>Evento no encontrado</p>
          <button
            type="button"
            className="button-secondary"
            onClick={() => navigate('/organizer/dashboard')}
          >
            Volver al dashboard
          </button>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="metrics-page">
        <div className="error-container" role="alert">
          <p>{error}</p>
          <button
            type="button"
            className="button-secondary"
            onClick={() => navigate('/organizer/dashboard')}
          >
            Volver al dashboard
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="metrics-page">
      <header className="page-header">
        <h1>{metrics.eventName}</h1>
        <p>Metricas del evento</p>
      </header>

      <div className="metrics-card">
        <dl className="metrics-grid">
          <div className="metrics-item">
            <dt>Fecha del evento</dt>
            <dd>{formatDate(metrics.eventDate)}</dd>
          </div>

          <div className="metrics-item">
            <dt>Entradas vendidas</dt>
            <dd className="metrics-value">{metrics.ticketsSold}</dd>
          </div>

          <div className="metrics-item">
            <dt>Ingresos totales</dt>
            <dd className="metrics-value">{formatCurrency(metrics.totalRevenue)}</dd>
          </div>

          <div className="metrics-item">
            <dt>Inventario restante</dt>
            <dd className="metrics-value">{metrics.remainingInventory}</dd>
          </div>

          <div className="metrics-item">
            <dt>Tickets escaneados</dt>
            <dd className="metrics-value">{metrics.ticketsScanned}</dd>
          </div>
        </dl>
      </div>

      <div className="metrics-actions">
        <button
          type="button"
          className="button-secondary"
          onClick={() => navigate('/organizer/dashboard')}
        >
          Volver al dashboard
        </button>
      </div>
    </div>
  )
}
