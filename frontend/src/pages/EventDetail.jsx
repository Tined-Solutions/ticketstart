import { useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useEvent } from '../hooks/useEvent.js'
import { formatEventDate, formatCurrency } from '../lib/format.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import Badge from '../components/ui/Badge.jsx'
import Button from '../components/Button.jsx'

function TicketTypeRow({ ticketType, isSelected, quantity, onSelect, onChange }) {
  const available = ticketType.available ?? ticketType.quantity ?? 0
  const isSoldOut = available <= 0

  return (
    <>
      {/* Hidden radio input for accessibility + test compatibility. Kept as a
          preceding sibling of the card so `peer-focus-visible` on the card
          shows a visible focus ring when the radio is focused via keyboard. */}
      <input
        type="radio"
        name="ticket-type"
        id={`ticket-type-${ticketType.id}`}
        value={ticketType.id}
        checked={isSelected}
        onChange={() => onSelect(ticketType.id)}
        disabled={isSoldOut}
        aria-label={`Seleccionar ${ticketType.name}`}
        className="sr-only peer"
      />
      <GlassCard
        className={`ticket-type-row peer-focus-visible:ring-2 peer-focus-visible:ring-brand-1 cursor-pointer transition-all border-2 ${
          isSelected
            ? 'border-brand-1 bg-brand-1/5 ticket-type-row-selected'
            : 'border-transparent hover:border-white/10'
        }`}
        onClick={() => !isSoldOut && onSelect(ticketType.id)}
      >
        <div className="flex items-center gap-4">
          {/* Visual radio indicator */}
        <label
          htmlFor={`ticket-type-${ticketType.id}`}
          className="sr-only"
        >
          {ticketType.name}
        </label>
        <div
          className={`w-5 h-5 rounded-full border-2 flex-shrink-0 flex items-center justify-center transition-colors ${
            isSelected
              ? 'border-brand-1 bg-brand-1'
              : 'border-white/30'
          }`}
        >
          {isSelected && <div className="w-2 h-2 rounded-full bg-white" />}
        </div>

        {/* Ticket info */}
        <div className="flex-1 min-w-0">
          <h3 className="font-heading font-semibold text-text-1">
            {ticketType.name}
          </h3>
          <p className="text-brand-1 font-bold text-lg">
            {formatCurrency(ticketType.price)}
          </p>
          <p className="text-text-2 text-sm">
            {isSoldOut
              ? 'Agotado'
              : `${available} disponibles de ${ticketType.quantity}`}
          </p>
        </div>

        {/* Quantity controls */}
        {isSelected && !isSoldOut && (
          <div
            className="flex items-center gap-2"
            onClick={(e) => e.stopPropagation()}
          >
            <button
              type="button"
              aria-label={`Disminuir cantidad de ${ticketType.name}`}
              onClick={() => onChange(Math.max(1, quantity - 1))}
              disabled={quantity <= 1}
              className="w-11 h-11 rounded-full glass-surface flex items-center justify-center text-text-1 hover:bg-white/15 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              −
            </button>
            <span
              aria-live="polite"
              className="w-8 text-center font-semibold text-text-1 tabular-nums"
            >
              {quantity}
            </span>
            <button
              type="button"
              aria-label={`Aumentar cantidad de ${ticketType.name}`}
              onClick={() => onChange(Math.min(available, quantity + 1))}
              disabled={quantity >= available}
              className="w-11 h-11 rounded-full glass-surface flex items-center justify-center text-text-1 hover:bg-white/15 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              +
            </button>
          </div>
        )}

        {isSoldOut && (
          <Badge variant="error">Agotado</Badge>
        )}
        </div>
      </GlassCard>
    </>
  )
}

// ─── Loading skeleton ────────────────────────────────────────────────────

function DetailSkeleton() {
  return (
    <div className="max-w-5xl mx-auto px-4 py-8 space-y-8">
      {/* Hero skeleton */}
      <div className="relative w-full h-72 md:h-96 overflow-hidden rounded-xl">
        <Skeleton width="100%" height="100%" variant="rectangular" />
      </div>

      {/* Ticket type skeletons */}
      <div className="space-y-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <GlassCard key={i} className="p-4">
            <div className="flex items-center gap-4">
              <Skeleton width="20px" height="20px" variant="circular" />
              <div className="flex-1 space-y-2">
                <Skeleton width="40%" height="20px" variant="text" />
                <Skeleton width="30%" height="16px" variant="text" />
              </div>
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
  const [quantity, setQuantity] = useState(0)

  const errorMessage = isError
    ? error?.response?.status === 404
      ? 'El evento no existe o no está disponible'
      : error?.response?.data?.error?.message ||
        error?.response?.data?.message ||
        'Ocurrió un error al cargar el evento'
    : ''

  const handleSelectTicketType = (ticketTypeId) => {
    setSelectedTicketTypeId(ticketTypeId)
    setQuantity((prev) => (prev > 0 ? prev : 1))
  }

  const updateQuantity = (nextQuantity) => {
    setQuantity(nextQuantity)
  }

  const selectedTicketType = event?.ticketTypes?.find(
    (ticketType) => ticketType.id === selectedTicketTypeId
  )

  const totalTickets = selectedTicketTypeId ? quantity : 0
  const totalPrice = selectedTicketType
    ? quantity * (selectedTicketType.price || 0)
    : 0

  const handleReserve = () => {
    if (!selectedTicketType || quantity === 0) return

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
          quantity,
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
        transition={{ duration: 0.6, ease: [0.4, 0, 0.2, 1] }}
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
        <h2 className="text-xl font-heading font-semibold text-text-1 mb-3">
          Descripción
        </h2>
        <p className="text-text-2 leading-relaxed">
          {event.description || 'Sin descripción'}
        </p>
      </section>

      {/* Tickets section */}
      <section className="px-4 pb-12">
        <h2 className="text-xl font-heading font-semibold text-text-1 mb-4">
          Entradas
        </h2>

        {!event.ticketTypes || event.ticketTypes.length === 0 ? (
          <GlassCard className="text-center py-8">
            <p className="text-text-2">No hay entradas disponibles para este evento.</p>
          </GlassCard>
        ) : (
          <>
            <div className="space-y-3">
              {event.ticketTypes.map((ticketType) => (
                <TicketTypeRow
                  key={ticketType.id}
                  ticketType={ticketType}
                  isSelected={selectedTicketTypeId === ticketType.id}
                  quantity={quantity}
                  onSelect={handleSelectTicketType}
                  onChange={updateQuantity}
                />
              ))}
            </div>

            {/* Reservation summary — always visible, button disabled when no selection */}
            <div className="mt-6">
              <GlassCard className="p-4 md:p-6">
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                  <div>
                    <p className="text-text-2 text-sm">Entradas seleccionadas</p>
                    <p className="text-text-1 font-semibold">
                      {selectedTicketTypeId && quantity > 0
                        ? `${totalTickets} × ${selectedTicketType?.name}`
                        : 'Ninguna'}
                    </p>
                  </div>
                  <div className="text-right">
                    <p className="text-text-2 text-sm">Total</p>
                    <p className="text-2xl font-display font-bold text-brand-1">
                      {formatCurrency(totalPrice)}
                    </p>
                  </div>
                </div>
                <Button
                  variant="gradient"
                  size="lg"
                  className="w-full mt-4 sm:mt-0 sm:w-auto sm:self-end"
                  onClick={handleReserve}
                  disabled={!selectedTicketType || quantity === 0}
                >
                  Reservar entradas
                </Button>
              </GlassCard>
            </div>
          </>
        )}
      </section>
    </div>
  )
}
