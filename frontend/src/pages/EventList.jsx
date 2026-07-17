import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import apiClient from '../api/client.js'
import { formatEventDate } from '../lib/format.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import EmptyState from '../components/ui/EmptyState.jsx'
import Badge from '../components/ui/Badge.jsx'
import Button from '../components/Button.jsx'
import { staggerContainer, staggerItem } from '../lib/motion.js'

function EventCard({ event, onClick }) {
  const ticketRange = event.ticketTypes?.length
    ? (() => {
        const prices = event.ticketTypes.map((t) => t.price).filter(Boolean)
        if (prices.length === 0) return null
        const min = Math.min(...prices)
        const max = Math.max(...prices)
        return min === max ? `$${min}` : `$${min} — $${max}`
      })()
    : null

  return (
    <motion.button
      type="button"
      variants={staggerItem}
      whileHover={{ y: -6, scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
      transition={{ duration: 0.2, ease: [0.2, 0.6, 0.2, 1] }}
      onClick={onClick}
          aria-label={`Ver detalle de ${event.name}`}
      className="text-left w-full cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 rounded-[--radius-glass]"
    >
      <GlassCard className="overflow-hidden p-0 h-full flex flex-col">
        {/* Event image */}
        <div className="relative aspect-[16/10] overflow-hidden">
          {event.imageUrl ? (
            <img
              src={event.imageUrl}
              alt={event.name}
              loading="lazy"
              className="w-full h-full object-cover"
            />
          ) : (
            <div className="w-full h-full bg-surface-elevated flex items-center justify-center">
              <span className="text-text-muted text-sm">Sin imagen</span>
            </div>
          )}
          {/* Date overlay badge */}
          <div className="absolute top-3 right-3">
            <Badge variant="info">{formatEventDate(event.date)}</Badge>
          </div>
        </div>

        {/* Card body */}
        <div className="p-4 flex flex-col flex-1">
          <h2 className="font-display font-semibold text-lg text-text-1 leading-snug line-clamp-2">
            {event.name}
          </h2>
          <p className="text-text-2 text-sm mt-1">{event.location}</p>
          {ticketRange && (
            <p className="text-brand-1 font-semibold text-sm mt-auto pt-3">
              {ticketRange}
            </p>
          )}
        </div>
      </GlassCard>
    </motion.button>
  )
}

export default function EventList() {
  const navigate = useNavigate()
  const [events, setEvents] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const loadEvents = useCallback((controller) => {
    apiClient
      .get('/events', { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) return
        setEvents(response.data || [])
        setError('')
        setLoading(false)
      })
      .catch((error) => {
        if (controller.signal.aborted) return
          const message =
            error.response?.data?.error?.message ||
            error.response?.data?.message ||
            'Ocurrio un error al cargar los eventos'
        setError(message)
        setLoading(false)
      })
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    loadEvents(controller)
    return () => controller.abort()
  }, [loadEvents])

  const handleRetry = () => {
    setLoading(true)
    setError('')
    const controller = new AbortController()
    loadEvents(controller)
  }

  const handleEventClick = (eventId) => () => {
    navigate(`/events/${eventId}`)
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 md:py-12">
      {/* Page header */}
      <motion.header
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
        className="text-center mb-10"
      >
        <h1 className="text-4xl md:text-5xl font-display font-bold text-text-1 mb-3">
          Eventos
        </h1>
        <p className="text-text-2 text-lg max-w-xl mx-auto">
          Descubri los mejores eventos y compra tus entradas
        </p>
      </motion.header>

      {/* Loading state: Skeleton grid */}
      {loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <GlassCard key={i} className="event-card-skeleton p-0 overflow-hidden">
              <Skeleton width="100%" height="200px" variant="rectangular" />
              <div className="p-4 space-y-3">
                <Skeleton width="75%" height="20px" variant="text" />
                <Skeleton width="50%" height="14px" variant="text" />
                <Skeleton width="40%" height="14px" variant="text" />
              </div>
            </GlassCard>
          ))}
        </div>
      )}

      {/* Error state */}
      {!loading && error && (
        <GlassCard className="text-center py-12 max-w-lg mx-auto">
          <p className="text-text-1 mb-4">{error}</p>
          <Button variant="gradient" onClick={handleRetry}>
            Reintentar
          </Button>
        </GlassCard>
      )}

      {/* Empty state */}
      {!loading && !error && events.length === 0 && (
        <EmptyState
          icon="📅"
          title="Sin eventos"
          description="No hay eventos disponibles por el momento."
          action={
            <Link to="/">
              <Button variant="secondary">Volver al inicio</Button>
            </Link>
          }
        />
      )}

      {/* Event grid */}
      {!loading && !error && events.length > 0 && (
        <motion.div
          variants={staggerContainer}
          initial="initial"
          animate="animate"
          className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6"
        >
          {events.map((event) => (
            <EventCard
              key={event.id}
              event={event}
              onClick={handleEventClick(event.id)}
            />
          ))}
        </motion.div>
      )}
    </div>
  )
}
