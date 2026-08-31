import { useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Ticket } from 'lucide-react'
import { useEvent } from '../hooks/useEvent.js'
import { formatEventDate, formatCurrency } from '../lib/format.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import Button from '../components/Button.jsx'
import TicketTypeTicket from '../components/events/TicketTypeTicket.jsx'

// ─── Loading skeleton ────────────────────────────────────────────────────

function DetailSkeleton() {
  return (
    <div className="max-w-5xl mx-auto px-4 py-8 space-y-8">
      {/* Hero skeleton */}
      <div className="relative w-full h-72 md:h-96 overflow-hidden rounded-xl">
        <Skeleton width="100%" height="100%" variant="rectangular" />
      </div>

      {/* Ticket type skeletons */}
      <div className="grid grid-cols-[repeat(auto-fit,10.625rem)] justify-start gap-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <GlassCard key={i} className="rounded-none border-gris-oscuro/25 bg-[#f7f0fa] p-0 mx-auto w-full max-w-[170px]">
            <div className="space-y-3 p-4 text-center">
              <Skeleton width="50%" height="12px" variant="text" />
              <Skeleton width="70%" height="20px" variant="text" />
              <Skeleton width="40%" height="28px" variant="text" />
            </div>
            <div className="space-y-2 px-4 pb-4 text-center">
              <Skeleton width="60%" height="14px" variant="text" />
            </div>
          </GlassCard>
        ))}
      </div>
    </div>
  )
}

// ─── Page ─────────────────────────────────────────────────────────────────

export default function EventDetail() {
  const { id } = useParams()
  const navigate = useNavigate()

  const { data: event, isLoading, isError, error, refetch } = useEvent(id)

  const [selectedTicketTypeId, setSelectedTicketTypeId] = useState(null)
  const [quantities, setQuantities] = useState({})

  const errorMessage = isError
    ? error?.response?.status === 404
      ? 'El evento no existe o no está disponible'
      : error?.response?.data?.error?.message ||
        error?.response?.data?.message ||
        'Ocurrió un error al cargar el evento'
    : ''

  const handleSelectTicketType = (ticketTypeId) => {
    // Single ticket type per purchase: choosing a different type replaces the
    // previous one entirely (its quantity resets to 1). Clicking the already
    // chosen ticket keeps its quantity — cancelling is only possible via the
    // decrement button down to 0.
    if (selectedTicketTypeId === ticketTypeId) return
    setSelectedTicketTypeId(ticketTypeId)
    setQuantities({ [ticketTypeId]: 1 })
  }

  const updateQuantity = (ticketTypeId, nextQuantity) => {
    if (nextQuantity <= 0) {
      // Dropping to 0 cancels that ticket's selection.
      setQuantities((prev) => ({ ...prev, [ticketTypeId]: 0 }))
      if (selectedTicketTypeId === ticketTypeId) {
        setSelectedTicketTypeId(null)
      }
      return
    }
    setQuantities((prev) => ({ ...prev, [ticketTypeId]: nextQuantity }))
  }

  const selectedTicketType = event?.ticketTypes?.find(
    (ticketType) => ticketType.id === selectedTicketTypeId
  )
  const selectedQuantity = selectedTicketTypeId ? quantities[selectedTicketTypeId] || 0 : 0

  const totalTickets = selectedQuantity
  const totalPrice = selectedTicketType ? selectedQuantity * (selectedTicketType.price || 0) : 0

  const handleReserve = () => {
    if (!selectedTicketType || selectedQuantity === 0) return

    navigate('/checkout', {
      state: {
        eventId: event.id,
        eventName: event.name,
        eventDate: event.date,
        eventLocation: event.location,
        eventImageUrl: event.imageUrl,
        selection: {
          ticketTypeId: selectedTicketType.id,
          name: selectedTicketType.name,
          price: selectedTicketType.price,
          quantity: selectedQuantity,
        },
        totalTickets,
        totalPrice,
      },
    })
  }

  // ─── Loading state ──────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="max-w-5xl mx-auto px-4 sm:px-6">
        <p className="px-4 py-8 text-text-2">Cargando evento…</p>
        <DetailSkeleton />
      </div>
    )
  }

  // ─── Error state ────────────────────────────────────────────────────────

  if (isError) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-16 text-center">
        <Link to="/events" className="inline-block text-text-2 hover:text-text-1 mb-6 transition-colors">
          ← Volver al catálogo
        </Link>
        <GlassCard className="py-12">
          <p className="text-text-1 mb-4">{errorMessage}</p>
          <Button variant="gradient" onClick={() => refetch()}>
            Reintentar
          </Button>
        </GlassCard>
      </div>
    )
  }

  // ─── Event not found ────────────────────────────────────────────────────

  if (!event) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-16 text-center">
        <Link to="/events" className="inline-block text-text-2 hover:text-text-1 mb-6 transition-colors">
          ← Volver al catálogo
        </Link>
        <p className="text-text-1 text-lg">El evento no esta disponible.</p>
      </div>
    )
  }

  // ─── Event loaded ───────────────────────────────────────────────────────

  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6">
      {/* Back link */}
      <Link
        to="/events"
        className="inline-flex items-center gap-1 text-text-2 hover:text-text-1 px-4 pt-6 transition-colors"
      >
        ← Volver al catálogo
      </Link>

      {/* Hero section */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
        className="relative w-full mt-4 mb-10 overflow-hidden rounded-xl"
      >
        {/* Background image */}
        {event.imageUrl ? (
          <img
            src={event.imageUrl}
            alt={event.name}
            width="1280"
            height="384"
            className="w-full h-72 md:h-96 object-cover"
          />
        ) : (
          <div className="w-full h-72 md:h-96 bg-surface-elevated flex items-center justify-center">
            <span className="text-text-muted">Sin imagen</span>
          </div>
        )}

        {/* Dark gradient overlay */}
        <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/30 to-transparent" />

        {/* Hero text */}
        <div className="absolute bottom-0 left-0 right-0 p-6 md:p-8">
          <h1 className="text-3xl md:text-4xl font-display font-bold text-white mb-2">
            {event.name}
          </h1>
          <div className="flex flex-wrap gap-4 text-white/80 text-sm md:text-base">
            <span>{formatEventDate(event.date)}</span>
            <span className="hidden sm:inline">·</span>
            <span>{event.location}</span>
          </div>
        </div>
      </motion.div>

      {/* Decorative-only banner (EHE-010): the backend is the enforcement
          authority — this simply informs the visitor that the event has
          started/ended. No effect on reservation flow. */}
      {new Date(event.date) < new Date() && (
        <div
          role="status"
          className="mx-4 mb-6 px-4 py-3 rounded-lg border border-amber-500/40 bg-amber-500/10 text-text-2 text-sm"
        >
          Este evento ya finalizó y sus entradas ya no están a la venta.
        </div>
      )}

      {/* Description */}
      <section className="px-4 mb-10">
        <h2 className="text-xl font-display font-semibold text-text-1 mb-3">
          Descripción
        </h2>
        <p className="text-text-2 leading-relaxed">
          {event.description || 'Sin descripción'}
        </p>
      </section>

      {/* Tickets section */}
      <section className="px-4 pb-12">
        <h2 className="text-xl font-display font-semibold text-text-1 mb-4">
          Entradas
        </h2>

        {!event.ticketTypes || event.ticketTypes.length === 0 ? (
          <GlassCard className="text-center py-8">
            <p className="text-text-2">No hay entradas disponibles para este evento.</p>
          </GlassCard>
        ) : (
          <>
            <div className="grid grid-cols-[repeat(auto-fit,10.625rem)] justify-start gap-3">
              {event.ticketTypes.map((ticketType) => (
                <TicketTypeTicket
                  key={ticketType.id}
                  ticketType={ticketType}
                  isSelected={selectedTicketTypeId === ticketType.id}
                  quantity={quantities[ticketType.id] || 0}
                  onSelect={handleSelectTicketType}
                  onChange={(nextQuantity) => updateQuantity(ticketType.id, nextQuantity)}
                />
              ))}
            </div>

            {/* Reservation summary — always visible, button disabled when no selection.
            Single ticket type per purchase: the summary reflects exactly the one
            active selection (backend reservation is single-type). No card: a
            horizontal divider separates the ticket grid from the detail below.
            The CTA is a local button (not the shared Button) so its hover stays
            subtle — no lift/translate — and its size stays contained. */}
            <div className="mt-10">
              <div aria-hidden="true" className="h-px w-full bg-gris-oscuro/15" />
              <div className="flex flex-col gap-4 pt-4 sm:flex-row sm:items-baseline sm:justify-between">
                <div>
                  <p className="text-sm text-text-2">Entradas seleccionadas</p>
                  <p className="mt-1 font-display text-lg font-semibold text-text-1">
                    {selectedTicketTypeId && selectedQuantity > 0
                      ? `${totalTickets} × ${selectedTicketType?.name}`
                      : 'Ninguna'}
                  </p>
                </div>
                <div className="sm:text-right">
                  <p className="text-sm text-text-2">Total</p>
                  <p className="mt-1 font-display text-xl font-bold text-brand-1">
                    {formatCurrency(totalPrice)}
                  </p>
                </div>
              </div>
              <div className="mt-4 flex sm:justify-end">
                <button
                  type="button"
                  onClick={handleReserve}
                  disabled={!selectedTicketType || selectedQuantity === 0}
                  className="inline-flex w-full items-center justify-center gap-2 rounded-full bg-gradient-to-r from-brand-1 to-brand-2 px-5 py-2 text-sm font-semibold text-white transition-colors duration-200 hover:brightness-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                >
                  <Ticket className="h-4 w-4" aria-hidden="true" />
                  Reservar entradas
                </button>
              </div>
            </div>
          </>
        )}
      </section>
    </div>
  )
}
