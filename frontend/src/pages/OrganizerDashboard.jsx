import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
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

function DeleteConfirmationDialog({ eventName, onConfirm, onCancel, deleting }) {
  return (
    <div className="delete-dialog-overlay" role="dialog" aria-modal="true" aria-labelledby="delete-dialog-title">
      <div className="delete-dialog">
        <h2 id="delete-dialog-title">Confirmar eliminacion</h2>
        <p>
          Estas seguro que deseas eliminar el evento <strong>{eventName}</strong>?
          Esta accion no se puede deshacer.
        </p>
        <div className="delete-dialog-actions">
          <button
            type="button"
            className="button-secondary"
            onClick={onCancel}
            disabled={deleting}
          >
            Cancelar
          </button>
          <button
            type="button"
            className="button-danger"
            onClick={onConfirm}
            disabled={deleting}
          >
            {deleting ? 'Eliminando...' : 'Eliminar'}
          </button>
        </div>
      </div>
    </div>
  )
}

export default function OrganizerDashboard() {
  const navigate = useNavigate()

  const [metrics, setMetrics] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)
  const [deleting, setDeleting] = useState(false)
  const [feedback, setFeedback] = useState({ type: '', message: '' })

  const loadMetrics = useCallback((controller) => {
    apiClient
      .get('/metrics/organizer', { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) return
        setMetrics(response.data || [])
        setError('')
        setLoading(false)
      })
      .catch((err) => {
        if (controller.signal.aborted) return
        setError(getErrorMessage(err))
        setLoading(false)
      })
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    loadMetrics(controller)
    return () => controller.abort()
  }, [loadMetrics])

  const handleRetry = () => {
    setLoading(true)
    setError('')
    const controller = new AbortController()
    loadMetrics(controller)
  }

  const handleDeleteClick = (event) => {
    setFeedback({ type: '', message: '' })
    setDeleteTarget(event)
  }

  const handleDeleteCancel = () => {
    setDeleteTarget(null)
  }

  const handleDeleteConfirm = async () => {
    if (!deleteTarget) return

    setDeleting(true)
    setFeedback({ type: '', message: '' })

    try {
      await apiClient.delete(`/events/${deleteTarget.eventId}`)
      setFeedback({
        type: 'success',
        message: `Evento "${deleteTarget.eventName}" eliminado correctamente`,
      })

      // Remove from local state immediately
      setMetrics((prev) =>
        prev.filter((m) => m.eventId !== deleteTarget.eventId)
      )
    } catch (err) {
      setFeedback({ type: 'error', message: getErrorMessage(err) })
    } finally {
      setDeleting(false)
      setDeleteTarget(null)
    }
  }

  return (
    <div className="organizer-dashboard-page">
      <header className="page-header">
        <h1>Dashboard</h1>
        <p>Gestiona tus eventos y consulta las metricas</p>
      </header>

      {feedback.message && (
        <div
          className={`feedback-message feedback-message--${feedback.type}`}
          role={feedback.type === 'error' ? 'alert' : 'status'}
        >
          {feedback.message}
        </div>
      )}

      <div className="dashboard-toolbar">
        <button
          type="button"
          className="button-primary"
          onClick={() => navigate('/organizer/events/new')}
        >
          + Crear evento
        </button>
      </div>

      {loading ? (
        <div className="dashboard-loading">
          <p>Cargando metricas...</p>
        </div>
      ) : error ? (
        <div className="error-container" role="alert">
          <p>{error}</p>
          <button type="button" className="button-secondary" onClick={handleRetry}>
            Reintentar
          </button>
        </div>
      ) : metrics.length === 0 ? (
        <div className="empty-state">
          <p>No tenes eventos creados todavia.</p>
          <button
            type="button"
            className="button-primary"
            onClick={() => navigate('/organizer/events/new')}
          >
            Crear tu primer evento
          </button>
        </div>
      ) : (
        <div className="dashboard-table-container">
          <table className="dashboard-table">
            <thead>
              <tr>
                <th>Evento</th>
                <th>Fecha</th>
                <th>Entradas vendidas</th>
                <th>Ingresos</th>
                <th>Inventario</th>
                <th>Escaneados</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {metrics.map((m) => (
                <tr key={m.eventId}>
                  <td data-label="Evento">{m.eventName}</td>
                  <td data-label="Fecha">{formatDate(m.eventDate)}</td>
                  <td data-label="Entradas vendidas">{m.ticketsSold}</td>
                  <td data-label="Ingresos">{formatCurrency(m.totalRevenue)}</td>
                  <td data-label="Inventario">{m.remainingInventory}</td>
                  <td data-label="Escaneados">{m.ticketsScanned}</td>
                  <td data-label="Acciones">
                    <div className="dashboard-actions">
                      <button
                        type="button"
                        className="button-secondary dashboard-action-btn"
                        onClick={() => navigate(`/organizer/events/${m.eventId}`)}
                        aria-label={`Editar ${m.eventName}`}
                      >
                        Editar
                      </button>
                      <button
                        type="button"
                        className="button-secondary dashboard-action-btn"
                        onClick={() => navigate(`/organizer/events/${m.eventId}/metrics`)}
                        aria-label={`Ver metricas de ${m.eventName}`}
                      >
                        Metricas
                      </button>
                      <button
                        type="button"
                        className="button-danger dashboard-action-btn"
                        onClick={() => handleDeleteClick(m)}
                        aria-label={`Eliminar ${m.eventName}`}
                      >
                        Eliminar
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {deleteTarget && (
        <DeleteConfirmationDialog
          eventName={deleteTarget.eventName}
          onConfirm={handleDeleteConfirm}
          onCancel={handleDeleteCancel}
          deleting={deleting}
        />
      )}
    </div>
  )
}
