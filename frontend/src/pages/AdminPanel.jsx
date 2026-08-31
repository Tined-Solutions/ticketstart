import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import { statusBadgeVariant, statusLabel } from '../lib/eventStatus.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import PasswordInput from '../components/ui/PasswordInput.jsx'
import Badge from '../components/ui/Badge.jsx'
import DropdownMenu from '../components/ui/DropdownMenu.jsx'
import Button from '../components/Button.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import AddTicketsModal from '../components/AddTicketsModal.jsx'
import DeleteConfirmationDialog from '../components/DeleteConfirmationDialog.jsx'
import { fadeIn } from '../lib/motion.js'

// Shared hover treatment for the events action buttons: grow a soft shadow on
// hover (combined with the base lift) and honor prefers-reduced-motion by
// disabling the movement/shadow transition. Tailwind emits motion-reduce in a
// later @media block, so it reliably overrides the base hover transforms.
const ACTION_HOVER =
  'hover:shadow-[0_8px_20px_rgba(74,74,74,0.18)] motion-reduce:transition-none motion-reduce:hover:translate-y-0 motion-reduce:hover:shadow-none'

const USERS_PER_PAGE = 10

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

export default function AdminPanel() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [events, setEvents] = useState([])
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)
  const [deleting, setDeleting] = useState(false)
  const [feedback, setFeedback] = useState({ type: '', message: '' })

  const [addTicketsTarget, setAddTicketsTarget] = useState(null)

  // EA-008: id of the event whose approve/reject request is in flight (disables
  // that row's action buttons while the request runs).
  const [busyApprovalId, setBusyApprovalId] = useState(null)

  const initialFormData = { name: '', email: '', password: '', role: '' }
  const [formData, setFormData] = useState(initialFormData)
  const [formErrors, setFormErrors] = useState({})
  const [creating, setCreating] = useState(false)
  const [createFeedback, setCreateFeedback] = useState({ type: '', message: '' })

  // Users section: client-side filter + pagination over the fetched `users` array.
  const [userSearch, setUserSearch] = useState('')
  const [userRole, setUserRole] = useState('')
  const [userPage, setUserPage] = useState(1)

  // Toggles the create-user form inside the Users section (list ⇄ form).
  const [showCreateUser, setShowCreateUser] = useState(false)

  // Top-level section switch: only one section renders at a time inside the
  // single container (Eventos | Usuarios), so the page never scrolls vertically.
  const [activeSection, setActiveSection] = useState('eventos')

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

  // EA-008: moderation actions. Success → invalidate catalog/detail queries
  // (D-6) + re-run the manual admin list fetch; failure → feedback, no mutation.
  const handleApprove = async (event) => {
    setFeedback({ type: '', message: '' })
    setBusyApprovalId(event.id)
    try {
      await apiClient.post(`/admin/events/${event.id}/approve`)
      setFeedback({
        type: 'success',
        message: `Evento "${event.name}" aprobado correctamente`,
      })
      queryClient.invalidateQueries(['events'])
      queryClient.invalidateQueries(['event', event.id])
      const controller = new AbortController()
      loadData(controller)
    } catch (err) {
      setFeedback({ type: 'error', message: getErrorMessage(err) })
    } finally {
      setBusyApprovalId(null)
    }
  }

  const handleReject = async (event) => {
    setFeedback({ type: '', message: '' })
    setBusyApprovalId(event.id)
    try {
      await apiClient.post(`/admin/events/${event.id}/reject`)
      setFeedback({
        type: 'success',
        message: `Evento "${event.name}" rechazado correctamente`,
      })
      queryClient.invalidateQueries(['events'])
      queryClient.invalidateQueries(['event', event.id])
      const controller = new AbortController()
      loadData(controller)
    } catch (err) {
      setFeedback({ type: 'error', message: getErrorMessage(err) })
    } finally {
      setBusyApprovalId(null)
    }
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
      setShowCreateUser(false)

      // Refresh user list
      const controller = new AbortController()
      loadData(controller)
    } catch (err) {
      setCreateFeedback({ type: 'error', message: getErrorMessage(err) })
    } finally {
      setCreating(false)
    }
  }

  // Display copy: upcoming events first (soonest-to-start at the very top),
  // then already-ended (past) events sorted descending so the OLDEST ended
  // event is last. Does NOT mutate `events` — header counts and Pendientes
  // badge read the original.
  const now = new Date().getTime()
  const upcoming = events
    .filter((e) => new Date(e.date).getTime() >= now)
    .sort((a, b) => new Date(a.date) - new Date(b.date))
  const past = events
    .filter((e) => new Date(e.date).getTime() < now)
    .sort((a, b) => new Date(b.date) - new Date(a.date))
  const sortedEvents = [...upcoming, ...past]

  // Users section: filter + paginate the fetched `users` array client-side.
  const filteredUsers = users.filter((u) => {
    const matchesRole = userRole === '' || u.role === userRole
    const q = userSearch.trim().toLowerCase()
    const matchesSearch =
      q === '' ||
      (u.email || '').toLowerCase().includes(q) ||
      (u.name || '').toLowerCase().includes(q)
    return matchesRole && matchesSearch
  })
  const totalUserPages = Math.max(1, Math.ceil(filteredUsers.length / USERS_PER_PAGE))
  const safeUserPage = Math.min(Math.max(1, userPage), totalUserPages)
  const pageUsers = filteredUsers.slice(
    (safeUserPage - 1) * USERS_PER_PAGE,
    safeUserPage * USERS_PER_PAGE
  )

  const handleUserSearchChange = (e) => {
    setUserSearch(e.target.value)
    setUserPage(1)
  }

  const handleUserRoleChange = (e) => {
    setUserRole(e.target.value)
    setUserPage(1)
  }

  const goToUserPage = (page) => {
    setUserPage(Math.min(Math.max(1, page), totalUserPages))
  }

  return (
    <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
      <header className="mb-4">
        <h1 className="text-2xl md:text-3xl font-display font-bold text-text-1 text-center mb-2">
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
        <GlassCard className="py-6">
          <div className="flex flex-col items-center gap-4" role="status" aria-label="Cargando panel de administración…">
            <Skeleton width="240px" height="18px" />
            <Skeleton width="180px" height="18px" />
            <Skeleton width="120px" height="18px" />
          </div>
        </GlassCard>
      ) : error ? (
        <GlassCard className="text-center py-6" role="alert">
          <p className="text-text-1 mb-3">{error}</p>
          <Button variant="secondary" onClick={handleRetry}>
            Reintentar
          </Button>
        </GlassCard>
      ) : (
        <>
          {/* ── Single container — no nested cards: sections render directly over
              the page background, with the tab bar acting as the divider ── */}
          <div>
            {/* Section switcher: always visible, even from the create-user form,
                so the user can jump between Eventos and Usuarios at any time. */}
            <div
              role="tablist"
              aria-label="Secciones del panel de administración"
              className="flex flex-wrap gap-2 mb-4 pb-2 border-b border-border"
            >
              <button
                type="button"
                role="tab"
                id="tab-eventos"
                aria-selected={activeSection === 'eventos'}
                aria-controls="panel-eventos"
                onClick={() => setActiveSection('eventos')}
                className={`px-4 py-2 rounded-full text-sm font-medium transition-colors ${
                  activeSection === 'eventos'
                    ? 'bg-purpura/15 text-purpura-dark border border-purpura/30'
                    : 'text-text-2 hover:bg-gris-oscuro/5 border border-transparent'
                }`}
              >
                Eventos
              </button>
              <button
                type="button"
                role="tab"
                id="tab-usuarios"
                aria-selected={activeSection === 'usuarios'}
                aria-controls="panel-usuarios"
                onClick={() => setActiveSection('usuarios')}
                className={`px-4 py-2 rounded-full text-sm font-medium transition-colors ${
                  activeSection === 'usuarios'
                    ? 'bg-purpura/15 text-purpura-dark border border-purpura/30'
                    : 'text-text-2 hover:bg-gris-oscuro/5 border border-transparent'
                }`}
              >
                Usuarios
              </button>
            </div>

            {activeSection === 'eventos' ? (
              <div role="tabpanel" id="panel-eventos" aria-labelledby="tab-eventos">
                {/* ── Events section ─────────────────────────────── */}
                <div className="flex flex-wrap items-center gap-2 mb-4">
                  <h2 className="font-display text-base font-semibold text-text-1">
                    Eventos ({events.length})
                  </h2>
                  {events.filter((e) => e.status === 'Pending').length > 0 && (
                    <Badge variant="warning">
                      Pendientes: {events.filter((e) => e.status === 'Pending').length}
                    </Badge>
                  )}
                </div>

                {events.length === 0 ? (
                  <p className="text-text-2 text-center py-4">No hay eventos en el sistema.</p>
                ) : (
                  <div className="flex flex-col">
                    {sortedEvents.map((event, index) => {
                      // D-7: past events are immutable (PEM-002) — computed per row
                      // in UTC (event.date is an ISO UTC DateTime; new Date() is
                      // UTC-based). Backend guard is authoritative (EHE-010); this
                      // only disables the mutation affordances (cosmetic defense).
                      const isPast = new Date(event.date) < new Date()
                      const readonlyTitle = 'Evento finalizado — solo lectura'
                      const isLast = index === sortedEvents.length - 1
                      return (
                        <div
                          key={event.id}
                          className={`flex flex-wrap items-center justify-between gap-3 py-2 px-1 hover:bg-surface-elevated transition-colors ${
                            isLast ? '' : 'border-b border-border'
                          }`}
                        >
                          <div className="flex-1 min-w-0">
                            <h3 className="font-display text-base md:text-lg font-semibold text-gris-oscuro leading-tight">
                              {event.name}
                            </h3>
                            <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                              <Badge variant={statusBadgeVariant(event.status)}>
                                {statusLabel(event.status)}
                              </Badge>
                              {isPast && <Badge variant="info">Finalizado</Badge>}
                            </div>
                            <p className="mt-1 text-sm text-text-2">
                              <span aria-hidden="true">📅</span> <span>{formatDate(event.date)}</span>
                              <span aria-hidden="true"> • </span> <span>{event.location || '\u2014'}</span>
                              <span aria-hidden="true"> • </span> <span>{getOrganizerEmail(event.organizerId)}</span>
                            </p>
                          </div>
                          <div className="flex flex-shrink-0 flex-wrap items-center gap-2">
                            {isPast && (
                              <Button
                                variant="glass"
                                size="sm"
                                onClick={() => navigate(`/organizer/events/${event.id}/view`)}
                                aria-label={`Ver ${event.name}`}
                                className={ACTION_HOVER}
                              >
                                Ver
                              </Button>
                            )}
                            {event.status !== 'Approved' && (
                              <span title={isPast ? readonlyTitle : undefined} className="inline-flex">
                                <Button
                                  variant="gradient"
                                  size="sm"
                                  onClick={() => handleApprove(event)}
                                  disabled={isPast || busyApprovalId === event.id}
                                  aria-label={`Aprobar ${event.name}`}
                                  className={ACTION_HOVER}
                                >
                                  Aprobar
                                </Button>
                              </span>
                            )}
                            {event.status === 'Pending' && (
                              <span title={isPast ? readonlyTitle : undefined} className="inline-flex">
                                <Button
                                  variant="secondary"
                                  size="sm"
                                  onClick={() => handleReject(event)}
                                  disabled={isPast || busyApprovalId === event.id}
                                  aria-label={`Rechazar ${event.name}`}
                                  className={`!bg-rose-50/70 !text-rose-700 border border-rose-300/60 !hover:bg-rose-100 ${ACTION_HOVER}`}
                                >
                                  Rechazar
                                </Button>
                              </span>
                            )}
                            <span title={isPast ? readonlyTitle : undefined} className="inline-flex">
                              <Button
                                variant="glass"
                                size="sm"
                                onClick={() => setAddTicketsTarget(event)}
                                disabled={isPast}
                                aria-label={`Agregar entradas a ${event.name}`}
                                className={ACTION_HOVER}
                              >
                                Agregar entradas
                              </Button>
                            </span>
                            <DropdownMenu
                              triggerLabel="Acciones"
                              align="right"
                              items={[
                                {
                                  label: 'Compras',
                                  ariaLabel: `Compras de ${event.name}`,
                                  onClick: () => navigate(`/admin/events/${event.id}/purchases`),
                                  disabled: false,
                                },
                                {
                                  label: 'Editar',
                                  ariaLabel: `Editar ${event.name}`,
                                  onClick: () => navigate(`/organizer/events/${event.id}`),
                                  disabled: isPast,
                                  title: isPast ? readonlyTitle : undefined,
                                },
                                {
                                  label: 'Eliminar',
                                  ariaLabel: `Eliminar ${event.name}`,
                                  onClick: () => handleDeleteClick(event),
                                  disabled: isPast,
                                  variant: 'danger',
                                  title: isPast ? readonlyTitle : undefined,
                                },
                              ]}
                            />
                          </div>
                        </div>
                      )
                    })}
                  </div>
                )}
              </div>
            ) : (
              <div role="tabpanel" id="panel-usuarios" aria-labelledby="tab-usuarios">
                {/* ── Users section (list ⇄ create-user) ─────────── */}
                {showCreateUser ? (
                  <>
                    <div className="mb-4">
                      <Button
                        variant="glass"
                        size="sm"
                        onClick={() => setShowCreateUser(false)}
                        aria-label="Volver a la lista"
                      >
                        ← Volver a la lista
                      </Button>
                    </div>

                    <h3 className="text-base font-display font-semibold text-gris-oscuro mb-4 text-center">
                      Crear usuario
                    </h3>

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

                    <form onSubmit={handleCreateUser} noValidate className="max-w-md mx-auto space-y-3.5">
                      <div>
                        <label
                          htmlFor="new-user-name"
                          className="block text-sm font-medium text-text-2 mb-1"
                        >
                          Nombre
                        </label>
                        <input
                          id="new-user-name"
                          type="text"
                          value={formData.name}
                          onChange={(e) => updateFormField('name', e.target.value)}
                          disabled={creating}
                          autoComplete="name"
                          className="w-full px-3 py-2 bg-surface-elevated border border-gris-oscuro/15 rounded-lg text-sm text-text-1 placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent"
                        />
                        {formErrors.name && (
                          <span className="form-error">{formErrors.name}</span>
                        )}
                      </div>

                      <div>
                        <label
                          htmlFor="new-user-email"
                          className="block text-sm font-medium text-text-2 mb-1"
                        >
                          Email
                        </label>
                        <input
                          id="new-user-email"
                          type="email"
                          value={formData.email}
                          onChange={(e) => updateFormField('email', e.target.value)}
                          disabled={creating}
                          autoComplete="email"
                          className="w-full px-3 py-2 bg-surface-elevated border border-gris-oscuro/15 rounded-lg text-sm text-text-1 placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent"
                        />
                        {formErrors.email && (
                          <span className="form-error">{formErrors.email}</span>
                        )}
                      </div>

                      <div>
                        <label
                          htmlFor="new-user-password"
                          className="block text-sm font-medium text-text-2 mb-1"
                        >
                          Contraseña
                        </label>
                        <PasswordInput
                          id="new-user-password"
                          value={formData.password}
                          onChange={(e) => updateFormField('password', e.target.value)}
                          disabled={creating}
                          autoComplete="new-password"
                          className="w-full px-3 py-2 bg-surface-elevated border border-gris-oscuro/15 rounded-lg text-sm text-text-1 placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent"
                        />
                        {formErrors.password && (
                          <span className="form-error">{formErrors.password}</span>
                        )}
                      </div>

                      <div>
                        <label
                          htmlFor="new-user-role"
                          className="block text-sm font-medium text-text-2 mb-1"
                        >
                          Rol
                        </label>
                        <select
                          id="new-user-role"
                          value={formData.role}
                          onChange={(e) => updateFormField('role', e.target.value)}
                          disabled={creating}
                          className="w-full px-3 py-2 bg-surface-elevated border border-gris-oscuro/15 rounded-lg text-sm text-text-1 focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent"
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
                  </>
                ) : (
                  <>
                    <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
                      <h2 className="font-display text-base font-semibold text-text-1">
                        Usuarios ({filteredUsers.length})
                      </h2>
                      <div className="flex flex-wrap items-center gap-2">
                        <label htmlFor="user-search" className="sr-only">
                          Buscar usuarios
                        </label>
                        <input
                          id="user-search"
                          type="search"
                          value={userSearch}
                          onChange={handleUserSearchChange}
                          placeholder="Buscar…"
                          aria-label="Buscar usuarios"
                          className="w-44 bg-white/60 border border-gris-oscuro/15 rounded-lg px-3 py-2 text-sm text-gris-oscuro placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent"
                        />
                        <label htmlFor="user-role" className="sr-only">
                          Filtrar por rol
                        </label>
                        <select
                          id="user-role"
                          value={userRole}
                          onChange={handleUserRoleChange}
                          aria-label="Filtrar por rol"
                          className="bg-white/60 border border-gris-oscuro/15 rounded-lg px-3 py-2 text-sm text-gris-oscuro focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent"
                        >
                          <option value="">Todos</option>
                          <option value="Admin">Admin</option>
                          <option value="Staff">Staff</option>
                          <option value="Organizador">Organizador</option>
                        </select>
                        <Button
                          variant="primary"
                          onClick={() => setShowCreateUser(true)}
                          aria-label="Crear nuevo usuario"
                        >
                          Crear nuevo usuario
                        </Button>
                      </div>
                    </div>

                    {users.length === 0 ? (
                      <p className="text-text-2 text-center py-4">No hay usuarios registrados.</p>
                    ) : filteredUsers.length === 0 ? (
                      <p className="text-text-2 text-center py-4">
                        No se encontraron usuarios con esos filtros.
                      </p>
                    ) : (
                      <>
                        <div className="overflow-x-auto">
                          <table className="admin-table w-full border-collapse text-left text-sm">
                            <thead>
                              <tr className="border-b-2 border-border">
                                <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap">Email</th>
                                <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap">Rol</th>
                                <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap">Fecha de registro</th>
                              </tr>
                            </thead>
                            <tbody>
                              {pageUsers.map((user) => (
                                <tr key={user.id} className="border-b border-border hover:bg-surface-elevated transition-colors">
                                  <td className="py-2 px-3 text-text-1 align-middle" data-label="Email">{user.email}</td>
                                  <td className="py-2 px-3 align-middle" data-label="Rol">
                                    <Badge variant={roleBadgeVariant(user.role)}>
                                      {roleLabel(user.role)}
                                    </Badge>
                                  </td>
                                  <td className="py-2 px-3 text-text-2 align-middle" data-label="Fecha de registro">
                                    {formatDate(user.createdAt)}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>

                        {totalUserPages > 1 && (
                          <div className="flex items-center justify-between mt-4">
                            <Button
                              variant="glass"
                              size="sm"
                              onClick={() => goToUserPage(safeUserPage - 1)}
                              disabled={safeUserPage <= 1}
                              aria-label="Página anterior"
                            >
                              Anterior
                            </Button>
                            <span className="text-sm text-text-2">
                              Página {safeUserPage} de {totalUserPages}
                            </span>
                            <Button
                              variant="glass"
                              size="sm"
                              onClick={() => goToUserPage(safeUserPage + 1)}
                              disabled={safeUserPage >= totalUserPages}
                              aria-label="Página siguiente"
                            >
                              Siguiente
                            </Button>
                          </div>
                        )}
                      </>
                    )}
                  </>
                )}
              </div>
            )}
          </div>
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
