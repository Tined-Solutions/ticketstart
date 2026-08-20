import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import GradientHero from '../components/ui/GradientHero.jsx'
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
        subtitle="La plataforma mas simple para descubrir eventos, reservar entradas y gestionar tus propios shows."
        cta={
          <Link
            to="/events"
            className="group relative inline-flex items-center rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 transition-transform duration-300 hover:-translate-y-0.5 hover:scale-[1.03] active:translate-y-0 active:scale-100 motion-reduce:transition-none"
          >
            {/* Soft gradient halo behind the button, glows in on hover */}
            <span
              aria-hidden="true"
              className="pointer-events-none absolute -inset-1 rounded-full bg-[linear-gradient(90deg,#F78B2D,#F5C01F,#67CF65,#18C8DB,#B65DC2)] opacity-0 blur-md transition-opacity duration-300 group-hover:opacity-40 motion-reduce:transition-none"
            />
            <span className="relative rounded-full bg-[linear-gradient(90deg,#F78B2D,#F5C01F,#67CF65,#18C8DB,#B65DC2)] p-[1.5px] shadow-[0_8px_22px_rgba(74,74,74,0.22)] transition-shadow duration-300 group-hover:shadow-[0_12px_30px_rgba(74,74,74,0.32)] motion-reduce:transition-none">
              <span className="relative inline-flex items-center gap-2 rounded-full bg-white px-5 py-2.5 font-display text-sm font-semibold text-gris-oscuro transition-colors duration-300 group-hover:bg-[#fffdf8] md:px-6 md:text-base motion-reduce:transition-none">
                Ver catálogo de eventos
                <span
                  aria-hidden="true"
                  className="text-purpura-dark transition-transform duration-300 group-hover:translate-x-1 motion-reduce:transition-none"
                >
                  →
                </span>
              </span>
            </span>
          </Link>
        }
      />

      {/* Featured events grid */}
      <motion.section
        variants={staggerContainer}
        initial="initial"
        whileInView="animate"
        viewport={{ once: true, margin: '-100px' }}
        className="relative overflow-hidden bg-gradient-to-b from-cian/10 via-canvas to-amarillo/10 px-4 py-16 sm:px-6 lg:px-8"
      >
        <div className="relative z-10 max-w-7xl mx-auto">
          <motion.div
            variants={staggerItem}
            className="mb-12 flex items-center justify-center gap-3"
          >
            <div aria-hidden="true" className="hidden items-center gap-1.5 sm:flex">
              <span className="h-8 w-1.5 -rotate-12 rounded-full bg-naranja" />
              <span className="h-5 w-1.5 -rotate-12 rounded-full bg-amarillo" />
              <span className="h-3 w-1.5 -rotate-12 rounded-full bg-cian" />
            </div>
            <motion.h2
              variants={staggerItem}
              className="text-center text-3xl font-display font-bold text-gris-oscuro md:text-4xl"
            >
              Eventos destacados
            </motion.h2>
            <div aria-hidden="true" className="hidden items-center gap-1.5 sm:flex">
              <span className="h-3 w-1.5 rotate-12 rounded-full bg-verde" />
              <span className="h-5 w-1.5 rotate-12 rounded-full bg-purpura" />
              <span className="h-8 w-1.5 rotate-12 rounded-full bg-naranja" />
            </div>
          </motion.div>

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
            <Link
              to="/events"
              className="group relative inline-flex items-center rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 transition-transform duration-300 hover:-translate-y-0.5 active:translate-y-0 motion-reduce:transition-none"
            >
              <span className="relative inline-flex items-center gap-2 rounded-full border border-gris-oscuro/15 bg-white/60 px-6 py-2.5 font-display text-sm font-semibold text-purpura-dark backdrop-blur-sm transition-all duration-300 group-hover:border-purpura/40 group-hover:bg-white/80 group-hover:shadow-[0_10px_24px_rgba(74,74,74,0.16)] motion-reduce:transition-none">
                Ver todos
                <span
                  aria-hidden="true"
                  className="text-purpura-dark transition-transform duration-300 group-hover:translate-x-1 motion-reduce:transition-none"
                >
                  →
                </span>
              </span>
            </Link>
          </motion.div>
        </div>
      </motion.section>
    </div>
  )
}
