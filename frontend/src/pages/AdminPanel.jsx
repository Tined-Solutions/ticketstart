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

function roleLabel(role) {
  if (!role) return ''
  const labels = {
    Organizador: 'Organizador',
    Staff: 'Staff',
    Admin: 'Admin',
  }
  return labels[role] || role
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

export default function AdminPanel() {
  const navigate = useNavigate()

  const [events, setEvents] = useState([])
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)
  const [deleting, setDeleting] = useState(false)
  const [feedback, setFeedback] = useState({ type: '', message: '' })

  const loadData = useCallback((controller) => {
    setLoading(true)
    setError('')

    const eventsPromise = apiClient
      .get('/admin/events', { signal: controller.signal, params: { page: 1, pageSize: 200 } })
      .then((response) => (response.data?.items || response.data || []))
      .catch((err) => {
        if (controller.signal.aborted) return []
        throw err
      })

    const usersPromise = apiClient
      .get('/admin/users', { signal: controller.signal, params: { page: 1, pageSize: 200 } })
      .then((response) => (response.data?.items || response.data || []))
      .catch((err) => {
        if (controller.signal.aborted) return []
        throw err
      })

    Promise.all([eventsPromise, usersPromise])
      .then(([eventsData, usersData]) => {
        if (controller.signal.aborted) return
        setEvents(eventsData)
        setUsers(usersData)
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
    // Standard fetch-on-mount pattern: loadData aborts on unmount via the controller.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadData(controller)
    return () => controller.abort()
  }, [loadData])

  const handleRetry = () => {
    const controller = new AbortController()
    loadData(controller)
  }

  const getOrganizerEmail = (organizerId) => {
    if (!users.length || !organizerId) return '—'
    const user = users.find((u) => u.id === organizerId)
    return user ? user.email : '—'
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
      await apiClient.delete(`/events/${deleteTarget.id}`)
      setFeedback({
        type: 'success',
        message: `Evento "${deleteTarget.name}" eliminado correctamente`,
      })

      // Remove from local state immediately
      setEvents((prev) => prev.filter((e) => e.id !== deleteTarget.id))
    } catch (err) {
      setFeedback({ type: 'error', message: getErrorMessage(err) })
    } finally {
      setDeleting(false)
      setDeleteTarget(null)
    }
  }

  return (
    <div className="admin-panel-page">
      <header className="page-header">
        <h1>Panel de administracion</h1>
        <p>Gestiona todos los eventos y usuarios del sistema</p>
      </header>

      {feedback.message && (
        <div
          className={`feedback-message feedback-message--${feedback.type}`}
          role={feedback.type === 'error' ? 'alert' : 'status'}
        >
          {feedback.message}
        </div>
      )}

      {loading ? (
        <div className="dashboard-loading">
          <p>Cargando panel de administracion...</p>
        </div>
      ) : error ? (
        <div className="error-container" role="alert">
          <p>{error}</p>
          <button type="button" className="button-secondary" onClick={handleRetry}>
            Reintentar
          </button>
        </div>
      ) : (
        <>
          {/* ── Events section ─────────────────────────────── */}
          <section className="admin-section">
            <h2>Eventos ({events.length})</h2>

            {events.length === 0 ? (
              <div className="empty-state">
                <p>No hay eventos en el sistema.</p>
              </div>
            ) : (
              <div className="dashboard-table-container">
                <table className="dashboard-table">
                  <thead>
                    <tr>
                      <th>Evento</th>
                      <th>Fecha</th>
                      <th>Ubicacion</th>
                      <th>Organizador</th>
                      <th>Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {events.map((event) => (
                      <tr key={event.id}>
                        <td data-label="Evento">{event.name}</td>
                        <td data-label="Fecha">{formatDate(event.date)}</td>
                        <td data-label="Ubicacion">{event.location || '—'}</td>
                        <td data-label="Organizador">
                          {getOrganizerEmail(event.organizerId)}
                        </td>
                        <td data-label="Acciones">
                          <div className="dashboard-actions">
                            <button
                              type="button"
                              className="button-secondary dashboard-action-btn"
                              onClick={() => navigate(`/organizer/events/${event.id}`)}
                              aria-label={`Editar ${event.name}`}
                            >
                              Editar
                            </button>
                            <button
                              type="button"
                              className="button-danger dashboard-action-btn"
                              onClick={() => handleDeleteClick(event)}
                              aria-label={`Eliminar ${event.name}`}
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
          </section>

          {/* ── Users section ──────────────────────────────── */}
          <section className="admin-section">
            <h2>Usuarios ({users.length})</h2>

            {users.length === 0 ? (
              <div className="empty-state">
                <p>No hay usuarios registrados.</p>
              </div>
            ) : (
              <div className="dashboard-table-container">
                <table className="dashboard-table">
                  <thead>
                    <tr>
                      <th>Email</th>
                      <th>Rol</th>
                      <th>Fecha de registro</th>
                    </tr>
                  </thead>
                  <tbody>
                    {users.map((user) => (
                      <tr key={user.id}>
                        <td data-label="Email">{user.email}</td>
                        <td data-label="Rol">
                          <span className={`badge ${roleBadgeClass(user.role)}`}>
                            {roleLabel(user.role)}
                          </span>
                        </td>
                        <td data-label="Fecha de registro">
                          {formatDate(user.createdAt)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}

      {deleteTarget && (
        <DeleteConfirmationDialog
          eventName={deleteTarget.name}
          onConfirm={handleDeleteConfirm}
          onCancel={handleDeleteCancel}
          deleting={deleting}
        />
      )}
    </div>
  )
}

function roleBadgeClass(role) {
  switch (role) {
    case 'Admin':
      return 'badge--danger'
    case 'Staff':
      return 'badge--success'
    case 'Organizador':
      return 'badge--info'
    default:
      return ''
  }
}
