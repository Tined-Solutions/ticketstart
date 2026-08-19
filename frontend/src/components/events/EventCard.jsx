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
      >
        <GlassCard className="overflow-hidden p-0 h-full flex flex-col">
          {/* Event image */}
          <div className="relative aspect-[16/10] overflow-hidden">
            {event.imageUrl ? (
              <img
                src={event.imageUrl}
                alt={event.name}
                loading="lazy"
                width="640"
                height="400"
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
            <h2 className="font-display font-semibold text-lg text-gris-oscuro leading-snug line-clamp-2">
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
      </motion.div>
    </Link>
  )
}
