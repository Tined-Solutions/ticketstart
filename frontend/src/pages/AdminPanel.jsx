import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import PasswordInput from '../components/ui/PasswordInput.jsx'
import Badge from '../components/ui/Badge.jsx'
import Button from '../components/Button.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import AddTicketsModal from '../components/AddTicketsModal.jsx'
import { useDialog } from '../hooks/useDialog.js'
import { fadeIn } from '../lib/motion.js'

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

function roleBadgeVariant(role) {
  switch (role) {
    case 'Admin':
      return 'error'
    case 'Staff':
      return 'success'
    case 'Organizador':
      return 'info'
    default:
      return 'info'
  }
}

function DeleteConfirmationDialog({ eventName, onConfirm, onCancel, deleting }) {
  const dialogRef = useDialog({ onClose: onCancel })
  return (
    <div
      ref={dialogRef}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-5 overscroll-contain"
      role="dialog"
      aria-modal="true"
      aria-labelledby="delete-dialog-title"
    >
      <div className="glass-surface p-8 max-w-md w-full shadow-xl text-left rounded-[--radius-glass]">
        <h2 id="delete-dialog-title" className="text-xl font-display font-semibold text-text-1 mb-3">
          Confirmar Eliminación
        </h2>
        <p className="text-text-2 mb-6 leading-relaxed">
          Estas seguro que deseas eliminar el evento <strong>{eventName}</strong>?
          Esta accion no se puede deshacer.
        </p>
        <div className="flex gap-3 justify-end">
          <Button variant="secondary" onClick={onCancel} disabled={deleting} className="min-h-[44px]">
            Cancelar
          </Button>
          <Button variant="danger" onClick={onConfirm} disabled={deleting} className="min-h-[44px]">
            {deleting ? 'Eliminando…' : 'Eliminar'}
          </Button>
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

  const [addTicketsTarget, setAddTicketsTarget] = useState(null)

  const initialFormData = { name: '', email: '', password: '', role: '' }
  const [formData, setFormData] = useState(initialFormData)
  const [formErrors, setFormErrors] = useState({})
  const [creating, setCreating] = useState(false)
  const [createFeedback, setCreateFeedback] = useState({ type: '', message: '' })

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

  function updateFormField(field, value) {
    setFormData((prev) => ({ ...prev, [field]: value }))
    setFormErrors((prev) => ({ ...prev, [field]: '' }))
    setCreateFeedback({ type: '', message: '' })
  }

  function isValidEmail(email) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
  }

  function validateCreateForm() {
    const errors = {}

    if (!formData.name.trim()) {
      errors.name = 'El nombre es obligatorio'
    }

    if (!formData.email.trim()) {
      errors.email = 'El email es obligatorio'
    } else if (!isValidEmail(formData.email.trim())) {
      errors.email = 'El email no es válido'
    }

    if (!formData.password) {
      errors.password = 'La contraseña es obligatoria'
    } else if (formData.password.length < 8) {
      errors.password = 'La contraseña debe tener al menos 8 caracteres'
    }

    if (!formData.role) {
      errors.role = 'Debes seleccionar un rol'
    }

    setFormErrors(errors)
    return Object.keys(errors).length === 0
  }

  const handleCreateUser = async (event) => {
    event.preventDefault()
    setCreateFeedback({ type: '', message: '' })

    if (!validateCreateForm()) {
      return
    }

    setCreating(true)
    try {
      await apiClient.post('/admin/users', {
        name: formData.name.trim(),
        email: formData.email.trim(),
        password: formData.password,
        role: formData.role,
      })
      setCreateFeedback({
        type: 'success',
        message: 'Usuario creado correctamente',
      })
      setFormData(initialFormData)
      setFormErrors({})

      // Refresh user list
      const controller = new AbortController()
      loadData(controller)
    } catch (err) {
      setCreateFeedback({ type: 'error', message: getErrorMessage(err) })
    } finally {
      setCreating(false)
    }
  }

  return (
    <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      <header className="mb-8">
        <h1 className="text-4xl md:text-5xl font-display font-bold text-text-1 text-center mb-2">
          Panel de administración
        </h1>
        <p className="text-text-2 text-center">Gestiona todos los eventos y usuarios del sistema</p>
      </header>

      {feedback.message && (
        <div
          className={`text-center py-3 px-4 rounded-lg mb-4 font-medium ${
            feedback.type === 'success'
              ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30'
              : 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/30'
          }`}
          role={feedback.type === 'error' ? 'alert' : 'status'}
        >
          {feedback.message}
        </div>
      )}

      {loading ? (
        <GlassCard className="py-12">
          <div className="flex flex-col items-center gap-4" role="status" aria-label="Cargando panel de administración…">
            <Skeleton width="240px" height="18px" />
            <Skeleton width="180px" height="18px" />
            <Skeleton width="120px" height="18px" />
          </div>
        </GlassCard>
      ) : error ? (
        <GlassCard className="text-center py-12" role="alert">
          <p className="text-text-1 mb-3">{error}</p>
          <Button variant="secondary" onClick={handleRetry}>
            Reintentar
          </Button>
        </GlassCard>
      ) : (
        <>
          {/* ── Events section ─────────────────────────────── */}
          <GlassCard className="p-6 mb-12">
            <h2 className="text-xl font-display font-semibold text-text-1 text-left mb-4 pb-2 border-b border-border">
              Eventos ({events.length})
            </h2>

            {events.length === 0 ? (
              <p className="text-text-2 text-center py-8">No hay eventos en el sistema.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="admin-table w-full border-collapse text-left text-sm">
                  <thead>
                    <tr className="border-b-2 border-border">
                      <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Evento</th>
                      <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Fecha</th>
                      <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Ubicación</th>
                      <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Organizador</th>
                      <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {events.map((event) => (
                      <tr key={event.id} className="border-b border-border hover:bg-surface-elevated transition-colors">
                        <td className="py-3.5 px-4 text-text-1 align-middle" data-label="Evento">{event.name}</td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Fecha">{formatDate(event.date)}</td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Ubicación">{event.location || '\u2014'}</td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Organizador">
                          {getOrganizerEmail(event.organizerId)}
                        </td>
                        <td className="py-3.5 px-4 align-middle" data-label="Acciones">
                          <div className="flex gap-2 flex-nowrap">
                            <Button
                              variant="primary"
                              size="sm"
                              onClick={() => setAddTicketsTarget(event)}
                              aria-label={`Agregar entradas a ${event.name}`}
                              className="min-h-[44px]"
                            >
                              Agregar entradas
                            </Button>
                            <Button
                              variant="secondary"
                              size="sm"
                              onClick={() => navigate(`/organizer/events/${event.id}`)}
                              aria-label={`Editar ${event.name}`}
                              className="min-h-[44px]"
                            >
                              Editar
                            </Button>
                            <Button
                              variant="danger"
                              size="sm"
                              onClick={() => handleDeleteClick(event)}
                              aria-label={`Eliminar ${event.name}`}
                              className="min-h-[44px]"
                            >
                              Eliminar
                            </Button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </GlassCard>

          {/* ── Users section ──────────────────────────────── */}
          <GlassCard className="p-6 mb-12">
            <h2 className="text-xl font-display font-semibold text-text-1 text-left mb-4 pb-2 border-b border-border">
              Usuarios ({users.length})
            </h2>

            {users.length === 0 ? (
              <p className="text-text-2 text-center py-8">No hay usuarios registrados.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="admin-table w-full border-collapse text-left text-sm">
                  <thead>
                    <tr className="border-b-2 border-border">
                      <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Email</th>
                      <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Rol</th>
                      <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Fecha de registro</th>
                    </tr>
                  </thead>
                  <tbody>
                    {users.map((user) => (
                      <tr key={user.id} className="border-b border-border hover:bg-surface-elevated transition-colors">
                        <td className="py-3.5 px-4 text-text-1 align-middle" data-label="Email">{user.email}</td>
                        <td className="py-3.5 px-4 align-middle" data-label="Rol">
                          <Badge variant={roleBadgeVariant(user.role)}>
                            {roleLabel(user.role)}
                          </Badge>
                        </td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Fecha de registro">
                          {formatDate(user.createdAt)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </GlassCard>

          {/* ── User Creation section ─────────────────────── */}
          <GlassCard className="p-6">
            <h2 className="text-xl font-display font-semibold text-text-1 text-left mb-4 pb-2 border-b border-border">
              Crear usuario
            </h2>

            {createFeedback.message && (
              <div
                className={`text-center py-3 px-4 rounded-lg mb-4 font-medium ${
                  createFeedback.type === 'success'
                    ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30'
                    : 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/30'
                }`}
                role={createFeedback.type === 'error' ? 'alert' : 'status'}
              >
                {createFeedback.message}
              </div>
            )}

            <form onSubmit={handleCreateUser} noValidate>
              <div className="form-group">
                <label htmlFor="new-user-name">Nombre</label>
                <input
                  id="new-user-name"
                  type="text"
                  value={formData.name}
                  onChange={(e) => updateFormField('name', e.target.value)}
                  disabled={creating}
                  autoComplete="name"
                />
                {formErrors.name && (
                  <span className="form-error">{formErrors.name}</span>
                )}
              </div>

              <div className="form-group">
                <label htmlFor="new-user-email">Email</label>
                <input
                  id="new-user-email"
                  type="email"
                  value={formData.email}
                  onChange={(e) => updateFormField('email', e.target.value)}
                  disabled={creating}
                  autoComplete="email"
                />
                {formErrors.email && (
                  <span className="form-error">{formErrors.email}</span>
                )}
              </div>

              <div className="form-group">
                <label htmlFor="new-user-password">Contraseña</label>
                <PasswordInput
                  id="new-user-password"
                  value={formData.password}
                  onChange={(e) => updateFormField('password', e.target.value)}
                  disabled={creating}
                  autoComplete="new-password"
                />
                {formErrors.password && (
                  <span className="form-error">{formErrors.password}</span>
                )}
              </div>

              <div className="form-group">
                <label htmlFor="new-user-role">Rol</label>
                <select
                  id="new-user-role"
                  value={formData.role}
                  onChange={(e) => updateFormField('role', e.target.value)}
                  disabled={creating}
                >
                  <option value="">Seleccionar rol</option>
                  <option value="Organizador">Organizador</option>
                  <option value="Staff">Staff</option>
                </select>
                {formErrors.role && (
                  <span className="form-error">{formErrors.role}</span>
                )}
              </div>

              <Button type="submit" variant="primary" disabled={creating}>
                {creating ? 'Creando…' : 'Crear usuario'}
              </Button>
            </form>
          </GlassCard>
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

      {addTicketsTarget && (
        <AddTicketsModal
          eventId={addTicketsTarget.id}
          eventName={addTicketsTarget.name}
          onClose={() => setAddTicketsTarget(null)}
          onSuccess={() => {
            setAddTicketsTarget(null)
            // ATS-007: the modal already invalidated ['event', id] + ['events'];
            // re-run the manual admin list fetch to reflect the new stock.
            const controller = new AbortController()
            loadData(controller)
          }}
        />
      )}
    </motion.div>
  )
}
