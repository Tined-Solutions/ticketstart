import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useEvents } from '../hooks/useEvents.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import EmptyState from '../components/ui/EmptyState.jsx'
import Button from '../components/Button.jsx'
import EventCard from '../components/events/EventCard.jsx'
import { staggerContainer } from '../lib/motion.js'

export default function EventList() {
  const { data: events = [], isLoading, isError, error, refetch } = useEvents()

  const errorMessage = isError
    ? error?.response?.data?.error?.message ||
      error?.response?.data?.message ||
      'Ocurrió un error al cargar los eventos'
    : ''

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 md:py-12">
      {/* Page header */}
      <motion.header
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
        className="text-center mb-10"
      >
        <h1 className="text-4xl md:text-5xl font-display font-bold text-text-1 mb-3">
          Eventos
        </h1>
        <p className="text-text-2 text-lg max-w-xl mx-auto">
          Descubrí los mejores eventos y compra tus entradas
        </p>
      </motion.header>

      {/* Loading state: Skeleton grid */}
      {isLoading && (
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
      {!isLoading && isError && (
        <GlassCard className="text-center py-12 max-w-lg mx-auto">
          <p className="text-text-1 mb-4">{errorMessage}</p>
          <Button variant="gradient" onClick={() => refetch()}>
            Reintentar
          </Button>
        </GlassCard>
      )}

      {/* Empty state */}
      {!isLoading && !isError && events.length === 0 && (
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
      {!isLoading && !isError && events.length > 0 && (
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
            />
          ))}
        </motion.div>
      )}
    </div>
  )
}
