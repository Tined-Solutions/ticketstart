import { useCallback, useEffect, useMemo, useState } from 'react'
import apiClient from '../../api/client.js'
import { formatCurrency, formatEventDate } from '../../lib/format.js'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import Button from '../Button.jsx'
import Badge from '../ui/Badge.jsx'
import GlassCard from '../ui/GlassCard.jsx'
import Skeleton from '../ui/Skeleton.jsx'
import DateTimePicker from '../ui/DateTimePicker.jsx'

const EVENTS_PER_PAGE = 10

const INPUT_CLASS =
  'bg-white/60 border border-gris-oscuro/15 rounded-lg px-3 py-2 text-sm text-gris-oscuro focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent'

/** Date → 'YYYY-MM-DD' in LOCAL time (what an <input type="date"> expects). */
function toLocalDateInput(date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(
    date.getDate()
  ).padStart(2, '0')}`
}

/** Default range: the full current month — first day to last day of the current year/month. */
function defaultRange() {
  const now = new Date()
  return {
    from: toLocalDateInput(new Date(now.getFullYear(), now.getMonth(), 1)),
    // Day 0 of the next month is the last day of the current one (month lengths
    // and leap years handled by Date itself).
    to: toLocalDateInput(new Date(now.getFullYear(), now.getMonth() + 1, 0)),
  }
}

/** Local 'YYYY-MM-DD' → ISO instant for the API (start / end of the local day). */
function startOfDayIso(localDate) {
  return localDate ? new Date(`${localDate}T00:00:00`).toISOString() : undefined
}

function endOfDayIso(localDate) {
  return localDate ? new Date(`${localDate}T23:59:59.999`).toISOString() : undefined
}

export default function AdminMetricsTab() {
  const defaults = useMemo(defaultRange, [])
  const [fromDate, setFromDate] = useState(defaults.from)
  const [toDate, setToDate] = useState(defaults.to)
  const [eventState, setEventState] = useState('')
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)

  const load = useCallback(
    async (signal) => {
      setLoading(true)
      setError('')
      try {
        const params = {
          from: startOfDayIso(fromDate),
          to: endOfDayIso(toDate),
        }
        if (eventState) params.eventState = eventState

        const response = await apiClient.get('/metrics/admin', { params, signal })
        setData(response.data)
        setPage(1)
      } catch (err) {
        // Aborted requests (filter change / unmount) are not errors.
        if (err?.code === 'ERR_CANCELED') return
        setError('No se pudieron cargar las métricas. Reintentá en unos segundos.')
      } finally {
        // A superseded request must not clear the loading state of its successor.
        if (!signal?.aborted) setLoading(false)
      }
    },
    [fromDate, toDate, eventState]
  )

  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal)
    return () => controller.abort()
  }, [load])

  const handleClear = () => {
    const fresh = defaultRange()
    setFromDate(fresh.from)
    setToDate(fresh.to)
    setEventState('')
  }

  if (loading) {
    return (
      <GlassCard className="py-6" role="status" aria-label="Cargando métricas…">
        <div className="flex flex-col items-center gap-4">
          <Skeleton width="240px" height="18px" />
          <Skeleton width="180px" height="18px" />
          <Skeleton width="120px" height="18px" />
        </div>
      </GlassCard>
    )
  }

  if (error) {
    return (
      <GlassCard className="text-center py-6" role="alert">
        <p className="text-text-1 mb-3">{error}</p>
        <Button variant="secondary" onClick={() => load()}>
          Reintentar
        </Button>
      </GlassCard>
    )
  }

  const totals = data ?? {
    charged: 0,
    refunded: 0,
    net: 0,
    ticketsSold: 0,
    refundOperations: 0,
    events: [],
  }
  const events = totals.events ?? []

  const totalPages = Math.max(1, Math.ceil(events.length / EVENTS_PER_PAGE))
  const safePage = Math.min(Math.max(1, page), totalPages)
  const pageEvents = events.slice((safePage - 1) * EVENTS_PER_PAGE, safePage * EVENTS_PER_PAGE)

  return (
    <div>
      {/* ── Filters ─────────────────────────────────────────────────── */}
      {/* relative + z-20 lifts the whole card (and the picker popover it
          contains) above the KPI GlassCards below: backdrop-blur traps the
          popover's own z-50 inside this card's stacking context. */}
      <GlassCard className="relative z-20 p-4 mb-4">
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-full sm:w-80">
            <label
              htmlFor="metrics-from"
              className="block text-xs font-semibold text-text-muted uppercase tracking-wider mb-1"
            >
              Desde
            </label>
            <DateTimePicker
              id="metrics-from"
              mode="date"
              allowPast
              value={fromDate}
              onChange={setFromDate}
            />
          </div>
          <div className="w-full sm:w-80">
            <label
              htmlFor="metrics-to"
              className="block text-xs font-semibold text-text-muted uppercase tracking-wider mb-1"
            >
              Hasta
            </label>
            <DateTimePicker
              id="metrics-to"
              mode="date"
              allowPast
              value={toDate}
              onChange={setToDate}
            />
          </div>
          <div>
            <label
              htmlFor="metrics-state"
              className="block text-xs font-semibold text-text-muted uppercase tracking-wider mb-1"
            >
              Estado del evento
            </label>
            <select
              id="metrics-state"
              value={eventState}
              onChange={(e) => setEventState(e.target.value)}
              className={INPUT_CLASS}
            >
              <option value="">Todos</option>
              <option value="upcoming">Activos</option>
              <option value="past">Finalizados</option>
            </select>
          </div>
          <Button variant="ghost" size="sm" onClick={handleClear}>
            Limpiar
          </Button>
        </div>
      </GlassCard>

      {/* ── KPIs (money-only) ───────────────────────────────────────── */}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-6">
        <GlassCard className="p-5 ring-1 ring-purpura/30">
          <p className="text-xs font-semibold text-text-muted uppercase tracking-wider">
            Neto percibido
          </p>
          <p className="mt-1 font-display text-2xl sm:text-3xl font-extrabold tracking-tight text-purpura-dark">
            {formatCurrency(totals.net)}
          </p>
          <p className="mt-1 text-xs text-text-muted">En el período</p>
        </GlassCard>

        <GlassCard className="p-5">
          <p className="text-xs font-semibold text-text-muted uppercase tracking-wider">Cobrado</p>
          <p className="mt-1 font-display text-2xl sm:text-3xl font-bold tracking-tight text-text-1">
            {formatCurrency(totals.charged)}
          </p>
          <p className="mt-1 text-xs text-text-muted">Total bruto cobrado</p>
        </GlassCard>

        <GlassCard className="p-5">
          <p className="text-xs font-semibold text-text-muted uppercase tracking-wider">
            Reembolsado
          </p>
          <p className="mt-1 font-display text-2xl sm:text-3xl font-bold tracking-tight text-danger">
            {formatCurrency(totals.refunded)}
          </p>
          <p className="mt-1">
            {totals.refundOperations > 0 ? (
              <Badge variant="error">
                {totals.refundOperations === 1
                  ? '1 devolución'
                  : `${totals.refundOperations} devoluciones`}
              </Badge>
            ) : (
              <span className="text-xs text-text-muted">Sin devoluciones</span>
            )}
          </p>
        </GlassCard>

        <GlassCard className="p-5">
          <p className="text-xs font-semibold text-text-muted uppercase tracking-wider">
            Entradas vendidas
          </p>
          <p className="mt-1 font-display text-2xl sm:text-3xl font-bold tracking-tight text-text-1">
            {totals.ticketsSold.toLocaleString('es-AR')}
          </p>
          <p className="mt-1 text-xs text-text-muted">En el período</p>
        </GlassCard>
      </div>

      {/* ── Per-event net table ─────────────────────────────────────── */}
      <div className="rounded-xl border border-glass-border bg-surface shadow-sm overflow-hidden">
        <div className="px-4 sm:px-5 py-3 border-b border-border">
          <h3 className="font-display text-sm font-semibold text-text-1">Detalle por evento</h3>
        </div>

        {events.length === 0 ? (
          <p className="text-text-2 text-center py-6 text-sm">
            No hay eventos para los filtros seleccionados.
          </p>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="admin-table w-full border-collapse text-left text-sm">
                <thead>
                  <tr className="border-b-2 border-border">
                    <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap">Evento</th>
                    <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap">Fecha</th>
                    <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap">Estado</th>
                    <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap text-right">
                      Entradas
                    </th>
                    <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap text-right">
                      Cobrado
                    </th>
                    <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap text-right">
                      Reembolsado
                    </th>
                    <th className="py-2 px-3 text-text-1 font-semibold whitespace-nowrap text-right">
                      Neto
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {pageEvents.map((row) => (
                    <tr
                      key={row.eventId}
                      className="border-b border-border hover:bg-surface-elevated transition-colors"
                    >
                      <td
                        className="max-w-[20rem] truncate py-2 px-3 text-text-1 align-middle"
                        data-label="Evento"
                        title={row.eventName}
                      >
                        {row.eventName}
                      </td>
                      <td className="py-2 px-3 text-text-2 align-middle" data-label="Fecha">
                        {formatEventDate(row.eventDate)}
                      </td>
                      <td className="py-2 px-3 align-middle" data-label="Estado">
                        {row.isPast ? (
                          <Badge variant="info">Finalizado</Badge>
                        ) : (
                          <Badge variant="success">Activo</Badge>
                        )}
                      </td>
                      <td
                        className="py-2 px-3 text-text-2 align-middle text-right"
                        data-label="Entradas"
                      >
                        {row.ticketsSold.toLocaleString('es-AR')}
                      </td>
                      <td
                        className="py-2 px-3 text-text-2 align-middle text-right"
                        data-label="Cobrado"
                      >
                        {formatCurrency(row.charged)}
                      </td>
                      <td
                        className="py-2 px-3 text-danger align-middle text-right"
                        data-label="Reembolsado"
                      >
                        {formatCurrency(row.refunded)}
                      </td>
                      <td
                        className="py-2 px-3 text-text-1 font-semibold align-middle text-right"
                        data-label="Neto"
                      >
                        {formatCurrency(row.net)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-between gap-2 px-4 py-3 border-t border-border">
                <Button
                  variant="glass"
                  size="sm"
                  onClick={() => setPage(safePage - 1)}
                  disabled={safePage <= 1}
                  aria-label="Página anterior"
                >
                  <ChevronLeft className="h-4 w-4 sm:hidden" aria-hidden="true" />
                  <span className="hidden sm:inline">Anterior</span>
                </Button>
                <span className="text-sm text-text-2 whitespace-nowrap">
                  Página {safePage} de {totalPages}
                </span>
                <Button
                  variant="glass"
                  size="sm"
                  onClick={() => setPage(safePage + 1)}
                  disabled={safePage >= totalPages}
                  aria-label="Página siguiente"
                >
                  <ChevronRight className="h-4 w-4 sm:hidden" aria-hidden="true" />
                  <span className="hidden sm:inline">Siguiente</span>
                </Button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
