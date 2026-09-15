import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useEvents } from '../hooks/useEvents.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import EmptyState from '../components/ui/EmptyState.jsx'
import { CalendarDaysIcon } from '../components/icons/calendar-days.jsx'
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
    <div className="relative -mt-16 overflow-x-hidden bg-gradient-to-b from-cian/10 via-canvas to-amarillo/10">
      {/* Gradient background identical to the Mis Entradas page: it starts at
          the very top, behind the fixed translucent navbar, so there is no
          white gap between the navbar and the page background. */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-28 pb-12">
        {/* Page header — same scale as the Mis Entradas section header */}
        <motion.header
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
          className="text-center mb-6"
        >
          <h1 className="text-3xl font-display font-bold text-gris-oscuro mb-2">
            Eventos
          </h1>
          <p className="text-text-2">
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
            icon={<CalendarDaysIcon size={48} className="inline-block" />}
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
    </div>
  )
}
