import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import apiClient from '../api/client.js'
import { formatCurrency, formatEventDate } from '../lib/format.js'
import { getErrorMessage } from '../lib/apiError.js'
import { statusBadgeVariant, statusLabel } from '../lib/eventStatus.js'
import { useAuth } from '../context/auth.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Badge from '../components/ui/Badge.jsx'
import DropdownMenu from '../components/ui/DropdownMenu.jsx'
import EmptyState from '../components/ui/EmptyState.jsx'
import Button from '../components/Button.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import { fadeIn } from '../lib/motion.js'

// Shared hover treatment for the row action buttons (same classes as
// AdminPanel): grow a soft shadow on hover and honor prefers-reduced-motion
// by disabling the movement/shadow transition. Tailwind emits motion-reduce
// in a later @media block, so it reliably overrides the base hover transforms.
const ACTION_HOVER =
  'hover:shadow-[0_8px_20px_rgba(74,74,74,0.18)] motion-reduce:transition-none motion-reduce:hover:translate-y-0 motion-reduce:hover:shadow-none'

export default function OrganizerDashboard() {
  const navigate = useNavigate()
  const { user } = useAuth()
  // EA-009: backend EventOwnership is unchanged; Edit is hidden for organizers
  // (UI-only) and kept for admins (D-8).
  const canEdit = user?.role === 'Admin'

  const [metrics, setMetrics] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const loadMetrics = useCallback((controller) => {
    apiClient
      .get('/metrics/organizer', { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) return
        // Accept both the paginated { items: [...] } envelope and a flat array.
        setMetrics(response.data?.items || response.data || [])
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

  // Display copy: upcoming events first (soonest-to-start at the very top),
  // then already-ended (past) events sorted descending so the OLDEST ended
  // event is last. Does NOT mutate `metrics` — the header count reads the
  // original (same strategy as AdminPanel).
  const now = new Date().getTime()
  const upcoming = metrics
    .filter((m) => new Date(m.eventDate).getTime() >= now)
    .sort((a, b) => new Date(a.eventDate) - new Date(b.eventDate))
  const past = metrics
    .filter((m) => new Date(m.eventDate).getTime() < now)
    .sort((a, b) => new Date(b.eventDate) - new Date(a.eventDate))
  const sortedMetrics = [...upcoming, ...past]

  return (
    <motion.div
      variants={fadeIn}
      initial="initial"
      animate="animate"
      className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4"
    >
      <header className="mb-4">
        <h1 className="text-2xl md:text-3xl font-display font-bold text-text-1 text-center mb-2">
          Dashboard
        </h1>
        <p className="text-text-2 text-center">Gestiona tus eventos y consulta las metricas</p>
      </header>

      {loading ? (
        <GlassCard className="py-6">
          <div className="flex flex-col items-center gap-4" role="status" aria-label="Cargando tus eventos…">
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
        <GlassCard className="p-4">
          <div className="flex flex-wrap items-center justify-between gap-3 mb-4 pb-2 border-b border-border">
            <h2 className="text-lg font-display font-semibold text-text-1 text-left">
              Eventos ({metrics.length})
            </h2>
            <Button
              variant="primary"
              size="sm"
              onClick={() => navigate('/organizer/events/new')}
            >
              + Crear evento
            </Button>
          </div>

          {metrics.length === 0 ? (
            <EmptyState
              icon="🎟️"
              title="No tenes eventos creados todavia"
              description="Crea tu primer evento para empezar a vender entradas y ver sus metricas."
              action={
                <Button variant="gradient" onClick={() => navigate('/organizer/events/new')}>
                  Crear evento
                </Button>
              }
            />
          ) : (
            <div className="flex flex-col">
              {sortedMetrics.map((m, index) => {
                // D-7: past events are immutable (PEM-002) — computed per row
                // in UTC (m.eventDate is an ISO UTC DateTime). Backend guard is
                // authoritative; this disables mutation affordances cosmetically.
                const isPast = new Date(m.eventDate) < new Date()
                const readonlyTitle = 'Evento finalizado — solo lectura'
                const isLast = index === sortedMetrics.length - 1
                return (
                  <div
                    key={m.eventId}
                    className={`flex flex-wrap items-center justify-between gap-3 py-2 px-1 hover:bg-surface-elevated transition-colors ${
                      isLast ? '' : 'border-b border-border'
                    }`}
                  >
                    <div className="flex-1 min-w-0">
                      <h3 className="font-display text-sm md:text-base font-semibold text-gris-oscuro leading-tight break-words">
                        {m.eventName}
                      </h3>
                      <div className="mt-1 flex flex-wrap items-center gap-1.5">
                        <Badge variant={statusBadgeVariant(m.status)}>
                          {statusLabel(m.status)}
                        </Badge>
                        {isPast && <Badge variant="info">Finalizado</Badge>}
                      </div>
                      <p className="mt-0.5 text-sm text-text-2">
                        <span aria-hidden="true">📅</span>{' '}
                        <span>{formatEventDate(m.eventDate)}</span>
                        <span aria-hidden="true"> • </span>{' '}
                        <span>{m.location || '\u2014'}</span>
                      </p>
                      <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-text-2">
                        <span>
                          Entradas vendidas:{' '}
                          <span className="font-semibold text-gris-oscuro">{m.ticketsSold}</span>
                        </span>
                        <span>
                          Ingresos:{' '}
                          <span className="font-semibold text-gris-oscuro">
                            {formatCurrency(m.totalRevenue)}
                          </span>
                        </span>
                        <span>
                          Inventario:{' '}
                          <span className="font-semibold text-gris-oscuro">{m.remainingInventory}</span>
                        </span>
                        <span>
                          Escaneados:{' '}
                          <span className="font-semibold text-gris-oscuro">{m.ticketsScanned}</span>
                        </span>
                      </div>
                    </div>
                    <div className="flex flex-shrink-0 items-center gap-2">
                      <Button
                        variant="glass"
                        size="sm"
                        onClick={() => navigate(`/organizer/events/${m.eventId}/view`)}
                        aria-label={`Ver ${m.eventName}`}
                        className={ACTION_HOVER}
                      >
                        Ver
                      </Button>
                      {/* ED-001/EHE-006 (D-4): the kebab only exists for admins —
                          organizers would otherwise get a dead trigger opening an
                          empty panel. It narrows to Editar: Metricas (page removed)
                          and Eliminar (Admin-only via the backend service guard)
                          are gone for every row regardless of status. */}
                      {canEdit && (
                        <DropdownMenu
                          triggerLabel="Acciones"
                          align="right"
                          items={[
                            {
                              label: 'Editar',
                              ariaLabel: `Editar ${m.eventName}`,
                              onClick: () => navigate(`/organizer/events/${m.eventId}`),
                              disabled: isPast,
                              title: isPast ? readonlyTitle : undefined,
                            },
                          ]}
                        />
                      )}
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </GlassCard>
      )}
    </motion.div>
  )
}
