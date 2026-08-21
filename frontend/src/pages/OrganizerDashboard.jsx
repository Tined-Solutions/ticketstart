import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import apiClient from '../api/client.js'
import { formatCurrency } from '../lib/format.js'
import { getErrorMessage } from '../lib/apiError.js'
import { statusBadgeVariant, statusLabel } from '../lib/eventStatus.js'
import { useAuth } from '../context/auth.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Badge from '../components/ui/Badge.jsx'
import Button from '../components/Button.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
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

function DeleteConfirmationDialog({ eventName, onConfirm, onCancel, deleting }) {
  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-5"
      role="dialog"
      aria-modal="true"
      aria-labelledby="delete-dialog-title"
    >
      <div className="glass-surface p-8 max-w-md w-full shadow-xl text-left rounded-[--radius-glass]">
        <h2 id="delete-dialog-title" className="text-xl font-display font-semibold text-text-1 mb-3">
          Confirmar eliminacion
        </h2>
        <p className="text-text-2 mb-6 leading-relaxed">
          Estas seguro que deseas eliminar el evento <strong>{eventName}</strong>?
          Esta accion no se puede deshacer.
        </p>
        <div className="flex gap-3 justify-end">
          <Button variant="secondary" onClick={onCancel} disabled={deleting}>
            Cancelar
          </Button>
          <Button variant="danger" onClick={onConfirm} disabled={deleting}>
            {deleting ? 'Eliminando...' : 'Eliminar'}
          </Button>
        </div>
      </div>
    </div>
  )
}

export default function OrganizerDashboard() {
  const navigate = useNavigate()
  const { user } = useAuth()
  // EA-009: backend EventOwnership is unchanged; Edit is hidden for organizers
  // (UI-only) and kept for admins (D-8).
  const canEdit = user?.role === 'Admin'

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
    <motion.div
      variants={fadeIn}
      initial="initial"
      animate="animate"
      className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10"
    >
      <header className="mb-8">
        <h1 className="text-4xl md:text-5xl font-display font-bold text-text-1 text-center mb-2">
          Dashboard
        </h1>
        <p className="text-text-2 text-center">Gestiona tus eventos y consulta las metricas</p>
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

      <div className="flex justify-end mb-6">
        <Button variant="gradient" onClick={() => navigate('/organizer/events/new')}>
          + Crear evento
        </Button>
      </div>

      {loading ? (
        <GlassCard className="p-6 space-y-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="flex gap-4 items-center">
              <Skeleton width="30%" height="18px" variant="text" />
              <Skeleton width="20%" height="18px" variant="text" />
              <Skeleton width="15%" height="18px" variant="text" />
              <Skeleton width="15%" height="18px" variant="text" />
              <div className="flex gap-2 ml-auto">
                <Skeleton width="64px" height="32px" variant="rectangular" />
                <Skeleton width="64px" height="32px" variant="rectangular" />
              </div>
            </div>
          ))}
        </GlassCard>
      ) : error ? (
        <GlassCard className="text-center py-12" role="alert">
          <p className="text-text-1 mb-3">{error}</p>
          <Button variant="secondary" onClick={handleRetry}>
            Reintentar
          </Button>
        </GlassCard>
      ) : metrics.length === 0 ? (
        <GlassCard className="text-center py-12">
          <p className="text-text-2 mb-4">No tenes eventos creados todavia.</p>
          <Button variant="gradient" onClick={() => navigate('/organizer/events/new')}>
            Crear tu primer evento
          </Button>
        </GlassCard>
      ) : (
        <GlassCard className="p-0 sm:p-0 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="admin-table w-full border-collapse text-left text-sm">
              <thead>
                <tr className="border-b-2 border-border">
                  <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Evento</th>
                  <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Fecha</th>
                  <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Estado</th>
                  <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Entradas vendidas</th>
                  <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Ingresos</th>
                  <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Inventario</th>
                  <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Escaneados</th>
                  <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {metrics.map((m) => {
                      // D-7: past events are immutable (PEM-002) — computed per row
                      // in UTC (m.eventDate is an ISO UTC DateTime). Backend guard is
                      // authoritative; this disables mutation affordances cosmetically.
                      const isPast = new Date(m.eventDate) < new Date()
                      const readonlyTitle = 'Evento finalizado — solo lectura'
                      return (
                      <tr
                    key={m.eventId}
                    className="border-b border-border hover:bg-surface-elevated transition-colors"
                  >
                    <td className="py-3.5 px-4 text-text-1 align-middle" data-label="Evento">{m.eventName}</td>
                    <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Fecha">{formatDate(m.eventDate)}</td>
                    <td className="py-3.5 px-4 align-middle" data-label="Estado">
                      <Badge variant={statusBadgeVariant(m.status)}>
                        {statusLabel(m.status)}
                      </Badge>
                      {isPast && (
                        <Badge variant="info" className="ml-1.5">
                          Finalizado
                        </Badge>
                      )}
                    </td>
                    <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Entradas vendidas">{m.ticketsSold}</td>
                    <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Ingresos">{formatCurrency(m.totalRevenue)}</td>
                    <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Inventario">{m.remainingInventory}</td>
                    <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Escaneados">{m.ticketsScanned}</td>
                    <td className="py-3.5 px-4 align-middle" data-label="Acciones">
                      <div className="flex gap-2 flex-nowrap">
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => navigate(`/organizer/events/${m.eventId}/view`)}
                          aria-label={`Ver ${m.eventName}`}
                          className="min-h-[44px]"
                        >
                          Ver
                        </Button>
                        {canEdit && (
                          <span title={isPast ? readonlyTitle : undefined} className="inline-flex">
                            <Button
                              variant="secondary"
                              size="sm"
                              onClick={() => navigate(`/organizer/events/${m.eventId}`)}
                              disabled={isPast}
                              aria-label={`Editar ${m.eventName}`}
                              className="min-h-[44px]"
                            >
                              Editar
                            </Button>
                          </span>
                        )}
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => navigate(`/organizer/events/${m.eventId}/metrics`)}
                          aria-label={`Ver metricas de ${m.eventName}`}
                          className="min-h-[44px]"
                        >
                          Metricas
                        </Button>
                        <span title={isPast ? readonlyTitle : undefined} className="inline-flex">
                          <Button
                            variant="danger"
                            size="sm"
                            onClick={() => handleDeleteClick(m)}
                            disabled={isPast}
                            aria-label={`Eliminar ${m.eventName}`}
                            className="min-h-[44px]"
                          >
                            Eliminar
                          </Button>
                        </span>
                      </div>
                    </td>
                  </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </GlassCard>
      )}

      {deleteTarget && (
        <DeleteConfirmationDialog
          eventName={deleteTarget.eventName}
          onConfirm={handleDeleteConfirm}
          onCancel={handleDeleteCancel}
          deleting={deleting}
        />
      )}
    </motion.div>
  )
}
