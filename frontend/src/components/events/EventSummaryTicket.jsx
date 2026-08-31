import { Calendar, MapPin } from 'lucide-react'
import { formatEventDate, formatCurrency } from '../../lib/format.js'

/**
 * Event summary rendered as a real ticket — header band with the event name,
 * image + structured date/location, perforation, purchase detail and the
 * 5-color brand edge. Shared by checkout step 1 (summary) and step 2
 * (confirmation) so both screens show the exact same card. Ticket styling
 * only applies to event-related info; other cards stay plain glass.
 */
export default function EventSummaryTicket({
  event,
  selectionName,
  quantity,
  totalPrice,
  className = '',
}) {
  return (
    <div
      className={`overflow-hidden rounded-[1.25rem] border border-gris-oscuro/15 bg-white/80 shadow-[0_12px_32px_rgba(74,74,74,0.16)] ${className}`}
    >
      {/* Header band — event name */}
      <div className="flex h-8 items-center justify-center bg-gris-oscuro/10 px-3">
        <span className="truncate font-display text-sm font-semibold text-text-1">
          {event.name}
        </span>
      </div>

      {/* Ticket face: image + structured event info */}
      <div className="flex gap-3 px-4 pb-3 pt-3">
        {event.imageUrl ? (
          <img
            src={event.imageUrl}
            alt={event.name}
            className="w-20 h-20 rounded-lg object-cover flex-shrink-0"
          />
        ) : (
          <div className="w-20 h-20 rounded-lg bg-surface-elevated flex items-center justify-center flex-shrink-0">
            <span className="text-text-muted text-xs">Sin imagen</span>
          </div>
        )}
        <div className="min-w-0">
          <p className="flex items-center gap-1.5 text-sm text-text-2">
            <Calendar className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
            <span>{formatEventDate(event.date)}</span>
          </p>
          <p className="mt-1.5 flex items-center gap-1.5 text-sm text-text-2">
            <MapPin className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
            <span className="truncate">{event.location}</span>
          </p>
        </div>
      </div>

      {/* Horizontal perforation */}
      <div
        aria-hidden="true"
        className="mx-auto my-1 h-0.5 w-[calc(100%-32px)] rounded-full bg-[repeating-linear-gradient(90deg,rgba(74,74,74,0.28)_0_8px,transparent_8px_16px)]"
      />

      {/* Purchase detail — structured labels */}
      <div className="grid grid-cols-3 gap-2 px-4 pb-3 pt-1">
        <div className="min-w-0">
          <p className="text-[10px] font-medium uppercase tracking-wider text-text-muted">
            Entrada
          </p>
          <p className="truncate font-semibold text-text-1">{selectionName}</p>
        </div>
        <div>
          <p className="text-[10px] font-medium uppercase tracking-wider text-text-muted">
            Cantidad
          </p>
          <p className="font-semibold text-text-1">{quantity}</p>
        </div>
        <div className="text-right">
          <p className="text-[10px] font-medium uppercase tracking-wider text-text-muted">
            Total
          </p>
          <p className="font-display font-bold text-brand-1">
            {formatCurrency(totalPrice)}
          </p>
        </div>
      </div>

      {/* Brand edge — always spans the full card width */}
      <div aria-hidden="true" className="flex h-1.5">
        <span className="flex-1 bg-naranja" />
        <span className="flex-1 bg-amarillo" />
        <span className="flex-1 bg-verde" />
        <span className="flex-1 bg-cian" />
        <span className="flex-1 bg-purpura" />
      </div>
    </div>
  )
}