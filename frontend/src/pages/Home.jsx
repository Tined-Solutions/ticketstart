import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import GradientHero from '../components/ui/GradientHero.jsx'
import Button from '../components/Button.jsx'
import EventCard from '../components/events/EventCard.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import { useEvents } from '../hooks/useEvents.js'
import { categoriesWithChipClass } from '../data/categories.js'
import { staggerContainer, staggerItem } from '../lib/motion.js'

export default function Home() {
  const { data: events = [], isLoading } = useEvents()
  const featured = events.slice(0, 6)

  return (
    <div className="home-page">
      {/* Full-viewport GradientHero with logo + category chips */}
      <GradientHero
        imageUrl={null}
        logo="/ticketera-logo.webp"
        chips={categoriesWithChipClass}
        title="TicketStart"
        subtitle="La plataforma mas simple para descubrir eventos, reservar entradas y gestionar tus propios shows."
        cta={
          <Link to="/events">
            <Button variant="gradient" size="lg">
              Ver catalogo de eventos
            </Button>
          </Link>
        }
      />

      {/* Featured events grid */}
      <motion.section
        variants={staggerContainer}
        initial="initial"
        whileInView="animate"
        viewport={{ once: true, margin: '-100px' }}
        className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16"
      >
        <motion.h2
          variants={staggerItem}
          className="text-3xl md:text-4xl font-display font-bold text-gris-oscuro text-center mb-12"
        >
          Eventos destacados
        </motion.h2>

        {isLoading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="glass-surface p-0 overflow-hidden rounded-[var(--radius-card)]">
                <Skeleton width="100%" height="200px" variant="rectangular" />
                <div className="p-4 space-y-3">
                  <Skeleton width="75%" height="20px" variant="text" />
                  <Skeleton width="50%" height="14px" variant="text" />
                </div>
              </div>
            ))}
          </div>
        ) : (
          <motion.div
            variants={staggerContainer}
            initial="initial"
            animate="animate"
            className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6"
          >
            {featured.map((event) => (
              <EventCard key={event.id} event={event} />
            ))}
          </motion.div>
        )}

        <motion.div variants={staggerItem} className="text-center mt-12">
          <Link to="/events">
            <Button variant="secondary" size="lg">
              Ver todos
            </Button>
          </Link>
        </motion.div>
      </motion.section>
    </div>
  )
}
