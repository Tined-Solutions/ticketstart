import { motion } from 'framer-motion'
import { formatCurrency } from '../../lib/format.js'
import { useReducedMotion } from '../../lib/motion.js'
import Badge from '../ui/Badge.jsx'

/**
 * Selectable ticket type rendered as a slim vertical ticket (perforation,
 * brand edge, tinted background) so each purchasable entry looks like the
 * real physical ticket. Keeps the `ticket-type-row` class for test
 * compatibility.
 */
export default function TicketTypeTicket({ ticketType, isSelected, quantity, onSelect, onChange }) {
  const reduced = useReducedMotion()
  const available = ticketType.available ?? ticketType.quantity ?? 0
  const total = ticketType.quantity || available
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
      {/* The ticket itself: one card with a brand-tinted gradient background */}
      <div
        className={`ticket-type-row peer-focus-visible:ring-2 peer-focus-visible:ring-brand-1 relative mx-auto h-full w-full max-w-[170px] cursor-pointer overflow-hidden rounded-none border-2 bg-gradient-to-b from-naranja/15 via-[#f7f0fa] to-purpura/15 p-0 transition-all duration-300 ${
          isSelected
            ? 'border-brand-1 shadow-[0_10px_24px_rgba(182,93,194,0.3)]'
            : 'border-transparent shadow-[0_6px_16px_rgba(74,74,74,0.12)] hover:border-gris-oscuro/20 hover:shadow-[0_10px_22px_rgba(74,74,74,0.18)]'
        } ${isSoldOut ? 'opacity-60' : ''}`}
        onClick={() => !isSoldOut && onSelect(ticketType.id)}
      >
        <div className="flex h-full w-full flex-col items-center justify-center">
          {/* Ticket head: name + price (the ENTRADA band sits above, full width) */}
          <div className="w-full px-2 pb-2 pt-9 text-center">
            <h3 className="line-clamp-2 font-display text-base font-semibold leading-snug text-gris-oscuro">
              {ticketType.name}
            </h3>
            <p className="mt-1 font-display text-2xl font-bold leading-tight text-purpura-dark">
              {formatCurrency(ticketType.price)}
              <span className="block text-[10px] font-medium leading-none text-text-2">
                por persona
              </span>
            </p>
          </div>

          {/* Horizontal perforation — centered on the card axis */}
          <div
            aria-hidden="true"
            className="mx-auto my-1 h-0.5 w-[calc(100%-24px)] rounded-full bg-[repeating-linear-gradient(90deg,rgba(74,74,74,0.28)_0_8px,transparent_8px_16px)]"
          />

          {/* Availability text */}
          <p className="w-full px-2 pb-1.5 pt-1 text-center text-sm text-text-2">
            {isSoldOut
              ? 'Sin stock'
              : available === total
                ? `${available} disponibles`
                : `${available} disponibles de ${total}`}
          </p>

          {/* Ticket foot: quantity controls / choose CTA / sold out.
              Fixed h-14 + w-[120px] slots so switching between the CTA and the
              quantity controls never shifts the card content. */}
          <div className="flex h-14 w-full flex-col items-center justify-center">
            {isSoldOut ? (
              <Badge variant="error">Agotado</Badge>
            ) : isSelected ? (
              <div
                className="mx-auto flex h-9 w-[120px] items-center justify-center gap-1.5"
                onClick={(e) => e.stopPropagation()}
              >
                {/* Quantity controls: only the selected ticket type can hold a
                    quantity. Decrementing to 0 deselects it (single type per
                    purchase). */}
                <motion.button
                  type="button"
                  aria-label={`Disminuir cantidad de ${ticketType.name}`}
                  whileTap={reduced ? undefined : { scale: 0.85 }}
                  onClick={() => onChange(Math.max(0, quantity - 1))}
                  disabled={quantity <= 0}
                  className="flex h-9 w-9 items-center justify-center rounded-full border border-gris-oscuro/15 bg-white/70 text-base text-gris-oscuro transition-colors hover:bg-gris-oscuro/10 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  −
                </motion.button>
                <span
                  aria-live="polite"
                  className="w-7 text-center text-base font-semibold tabular-nums text-text-1"
                >
                  {quantity}
                </span>
                <motion.button
                  type="button"
                  aria-label={`Aumentar cantidad de ${ticketType.name}`}
                  whileTap={reduced ? undefined : { scale: 0.85 }}
                  onClick={() => onChange(Math.min(available, quantity + 1))}
                  disabled={quantity >= available}
                  className="flex h-9 w-9 items-center justify-center rounded-full border border-gris-oscuro/15 bg-white/70 text-base text-gris-oscuro transition-colors hover:bg-gris-oscuro/10 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  +
                </motion.button>
              </div>
            ) : (
              <span className="mx-auto inline-flex h-9 w-[120px] items-center justify-center rounded-full border border-purpura-dark/30 text-xs font-semibold text-purpura-dark transition-colors hover:bg-purpura-dark/10">
                Elegir entrada
              </span>
            )}
          </div>
        </div>

        {/* ENTRADA band — full-width header that divides the ticket content */}
        <div className="absolute inset-x-0 top-0 flex h-7 items-center justify-center bg-gris-oscuro/10">
          <span className="text-[9px] font-semibold uppercase tracking-[0.16em] text-text-2">
            Entrada
          </span>
        </div>

        {/* Printed brand edge — always spans the full card width */}
        <div aria-hidden="true" className="absolute inset-x-0 bottom-0 flex h-1.5">
          <span className="flex-1 bg-naranja" />
          <span className="flex-1 bg-amarillo" />
          <span className="flex-1 bg-verde" />
          <span className="flex-1 bg-cian" />
          <span className="flex-1 bg-purpura" />
        </div>
      </div>
    </>
  )
}