import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { formatEventDate, formatCurrency } from '../../lib/format.js'
import GlassCard from '../ui/GlassCard.jsx'
import Badge from '../ui/Badge.jsx'
import { staggerItem } from '../../lib/motion.js'

export default function EventCard({ event }) {
  const ticketRange = event.ticketTypes?.length
    ? (() => {
        const prices = event.ticketTypes.map((t) => t.price).filter(Boolean)
        if (prices.length === 0) return null
        const min = Math.min(...prices)
        const max = Math.max(...prices)
        return min === max
          ? formatCurrency(min)
          : `${formatCurrency(min)} — ${formatCurrency(max)}`
      })()
    : null

  return (
    <Link
      to={`/events/${event.id}`}
      aria-label={`Ver detalle de ${event.name}`}
      className="block w-full text-left cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 rounded-[var(--radius-card)]"
    >
      <motion.div
        variants={staggerItem}
        whileHover={{ y: -6, scale: 1.02 }}
        whileTap={{ scale: 0.98 }}
        transition={{ duration: 0.2, ease: [0.2, 0.6, 0.2, 1] }}
        className="group relative"
      >
        <GlassCard className="h-full overflow-hidden border-white/75 bg-white/80 p-0 shadow-[0_14px_34px_rgba(74,74,74,0.12)] ring-1 ring-white/50 transition-[border-color,box-shadow] duration-300 group-hover:border-purpura/40 group-hover:shadow-[0_22px_48px_rgba(74,74,74,0.2)]">
          {/* Event image */}
          <div className="relative aspect-[16/10] overflow-hidden bg-gradient-to-br from-naranja/20 via-cian/10 to-purpura/20">
            {event.imageUrl ? (
              <img
                src={event.imageUrl}
                alt={event.name}
                loading="lazy"
                width="640"
                height="400"
                className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-[1.04]"
              />
            ) : (
              <div className="flex h-full w-full items-center justify-center bg-surface-elevated">
                <span className="text-text-muted text-sm">Sin imagen</span>
              </div>
            )}
            <div
              aria-hidden="true"
              className="pointer-events-none absolute inset-0 bg-gradient-to-t from-gris-oscuro/25 via-transparent to-transparent opacity-0 transition-opacity duration-300 group-hover:opacity-100"
            />
            {/* Date overlay badge */}
            <div className="absolute top-3 right-3">
              <Badge variant="info">{formatEventDate(event.date)}</Badge>
            </div>
            {/* Five-color ticket signature */}
            <div aria-hidden="true" className="absolute inset-x-0 bottom-0 flex h-1.5">
              <span className="flex-1 bg-naranja" />
              <span className="flex-1 bg-amarillo" />
              <span className="flex-1 bg-verde" />
              <span className="flex-1 bg-cian" />
              <span className="flex-1 bg-purpura" />
            </div>
          </div>

          {/* Card body */}
          <div className="flex flex-1 flex-col bg-white/45 p-5">
            <h2 className="line-clamp-2 font-display text-lg font-semibold leading-snug text-gris-oscuro">
              {event.name}
            </h2>
            <p className="mt-1 text-sm text-text-2">{event.location}</p>
            {ticketRange && (
              <div className="mt-auto pt-4">
                <p className="inline-flex rounded-full border border-purpura/20 bg-purpura/10 px-3 py-1 text-sm font-semibold text-purpura-dark">
                  {ticketRange}
                </p>
              </div>
            )}
          </div>
        </GlassCard>
      </motion.div>
    </Link>
  )
}
