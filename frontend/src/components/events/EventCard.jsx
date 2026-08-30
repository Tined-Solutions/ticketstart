import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { formatCurrency } from '../../lib/format.js'
import GlassCard from '../ui/GlassCard.jsx'
import { staggerItem, useReducedMotion } from '../../lib/motion.js'

function capitalize(value) {
  return value ? value.charAt(0).toUpperCase() + value.slice(1) : ''
}

function getCardDateParts(dateString) {
  if (!dateString) return null

  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return null

  return {
    weekday: capitalize(date.toLocaleDateString('es-AR', { weekday: 'short' }).replace('.', '')),
    day: date.toLocaleDateString('es-AR', { day: '2-digit' }),
    month: capitalize(date.toLocaleDateString('es-AR', { month: 'short' }).replace('.', '')),
    year: date.toLocaleDateString('es-AR', { year: 'numeric' }),
  }
}

export default function EventCard({ event }) {
  const prefersReducedMotion = useReducedMotion()
  const [isFlipped, setIsFlipped] = useState(false)
  const [isHovered, setIsHovered] = useState(false)
  const frontRef = useRef(null)
  const backLinkRef = useRef(null)
  const focusBackOnFlip = useRef(false)
  const focusFrontOnReturn = useRef(false)

  const startingPrice = event.ticketTypes?.length
    ? (() => {
        const prices = event.ticketTypes
          .map((ticketType) => ticketType.price)
          .filter(
            (price) =>
              price !== null &&
              price !== undefined &&
              Number.isFinite(Number(price))
          )
        if (prices.length === 0) return null
        return formatCurrency(Math.min(...prices.map(Number)))
      })()
    : null
  const revealBack = isFlipped || isHovered
  const dateParts = getCardDateParts(event.date)

  useEffect(() => {
    if (isFlipped && focusBackOnFlip.current) {
      backLinkRef.current?.focus()
      focusBackOnFlip.current = false
    }

    if (!isFlipped && focusFrontOnReturn.current) {
      frontRef.current?.focus()
      focusFrontOnReturn.current = false
    }
  }, [isFlipped])

  const handleFrontClick = () => {
    focusBackOnFlip.current = true
    setIsFlipped((current) => !current)
  }

  const handleReturnClick = () => {
    focusFrontOnReturn.current = true
    setIsFlipped(false)
  }

  const handlePointerEnter = (pointerEvent) => {
    if (pointerEvent.pointerType !== 'touch') setIsHovered(true)
  }

  const handlePointerLeave = (pointerEvent) => {
    if (pointerEvent.pointerType !== 'touch') setIsHovered(false)
  }

  return (
    <motion.div
      variants={staggerItem}
      whileTap={prefersReducedMotion ? undefined : { scale: 0.995 }}
      transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] }}
      className="group relative h-full [perspective:1600px]"
      onPointerEnter={handlePointerEnter}
      onPointerLeave={handlePointerLeave}
    >
      {/* Rotating 3D wrapper — NO overflow/background here, so preserve-3d is not
          flattened and each face can flip in real 3D. The whole ticket turns. */}
      <div
        data-testid="event-card-ticket"
        className="relative aspect-[16/10] [transform-style:preserve-3d] will-change-transform transition-transform duration-600 ease-[cubic-bezier(0.4,0,0.2,1)] motion-reduce:transition-none"
        style={{ transform: revealBack ? 'rotateY(180deg)' : 'rotateY(0deg)' }}
      >
        {/* Front face: the whole ticket with image-led view */}
        <GlassCard
          inert={revealBack}
          aria-hidden={revealBack}
          className="absolute inset-0 overflow-hidden rounded-none border-gris-oscuro/35 bg-[#fffdf8] p-0 shadow-[0_14px_34px_rgba(74,74,74,0.22)] [backface-visibility:hidden] transition-shadow duration-300 group-hover:shadow-[0_22px_48px_rgba(74,74,74,0.3)] motion-reduce:transition-none"
        >
          <button
            ref={frontRef}
            type="button"
            aria-controls={`event-card-back-${event.id}`}
            aria-expanded={revealBack}
            aria-label={`Ver precio y opciones de ${event.name}`}
            onClick={handleFrontClick}
            className="relative block h-full w-full text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-inset"
          >
            {event.imageUrl ? (
              <img
                src={event.imageUrl}
                alt={event.name}
                loading="lazy"
                width="640"
                height="400"
                className="absolute inset-0 h-full w-full object-cover"
              />
            ) : (
              <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-naranja/20 via-cian/10 to-purpura/20">
                <span className="text-text-muted text-sm">Sin imagen</span>
              </div>
            )}
          </button>
        </GlassCard>

        {/* Back face: the whole ticket with compact purchase view */}
        <GlassCard
          inert={!revealBack}
          aria-hidden={!revealBack}
          className="absolute inset-0 overflow-hidden rounded-none border-gris-oscuro/35 p-0 shadow-[0_14px_34px_rgba(74,74,74,0.22)] [backface-visibility:hidden] [transform:rotateY(180deg)] transition-shadow duration-300 group-hover:shadow-[0_22px_48px_rgba(74,74,74,0.3)] motion-reduce:transition-none"
        >
          <div className="relative flex h-full flex-col bg-gradient-to-br from-[#fffdf8] via-[#fdf2ea] to-purpura/20">
            <button
              type="button"
              aria-label={`Volver a la información de ${event.name}`}
              onClick={handleReturnClick}
              className="absolute right-3 top-3 z-10 inline-flex min-h-9 min-w-9 items-center justify-center rounded-full bg-white/70 text-gris-oscuro shadow-sm transition-colors hover:bg-gris-oscuro/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 motion-reduce:transition-none pointer-fine:opacity-0 pointer-fine:pointer-events-none pointer-fine:focus-visible:opacity-100 pointer-fine:focus-visible:pointer-events-auto"
            >
              <span aria-hidden="true">←</span>
            </button>

            <div className="flex min-h-0 flex-1">
              {/* Ticket stub column: the date as the detachable tail */}
              <div className="relative flex w-20 shrink-0 flex-col items-center justify-center bg-[#f3ede2] px-1 text-center">
                <span className="font-display text-lg font-semibold leading-none text-gris-oscuro">
                  {dateParts?.weekday}
                </span>
                <span className="mt-1 font-display text-2xl font-bold leading-none text-purpura-dark">
                  {dateParts?.day}
                </span>
                <span className="mt-1 text-sm font-semibold uppercase tracking-[0.12em] text-text-2">
                  {dateParts?.month}
                </span>
              </div>

              {/* Vertical perforation */}
              <div aria-hidden="true" className="relative w-0 shrink-0">
                <div className="absolute inset-y-0 left-1/2 w-0.5 -translate-x-1/2 rounded-full bg-[repeating-linear-gradient(180deg,rgba(74,74,74,0.28)_0_8px,transparent_8px_16px)]" />
                <span className="absolute left-1/2 top-3 size-4 -translate-x-1/2 rounded-full border border-[#e8e2d8] bg-[#f3ede2]" />
                <span className="absolute bottom-3 left-1/2 size-4 -translate-x-1/2 rounded-full border border-[#e8e2d8] bg-[#f3ede2]" />
              </div>

              {/* Main content */}
              <div className="relative flex min-w-0 flex-1 flex-col items-center justify-center px-4 py-3 text-center">
                <h2 className="line-clamp-2 font-display text-base font-semibold leading-snug text-gris-oscuro">
                  {event.name}
                </h2>
                <p className="mt-0.5 line-clamp-1 text-sm text-text-2">{event.location}</p>
                <p className="mt-1 font-display text-2xl font-bold text-purpura-dark">
                  {startingPrice ? `Desde ${startingPrice}` : 'Precio por confirmar'}
                </p>
                <Link
                  ref={backLinkRef}
                  to={`/events/${event.id}`}
                  tabIndex={revealBack ? 0 : -1}
                  onClick={(clickEvent) => clickEvent.stopPropagation()}
                  className="mt-2 inline-flex min-h-11 items-center rounded-full bg-purpura-dark px-5 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-[#5a1b64] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 motion-reduce:transition-none"
                >
                  Comprar entradas <span aria-hidden="true" className="ml-2">→</span>
                </Link>
              </div>
            </div>

            {/* Printed brand edge */}
            <div aria-hidden="true" className="flex h-1.5 shrink-0">
              <span className="flex-1 bg-naranja" />
              <span className="flex-1 bg-amarillo" />
              <span className="flex-1 bg-verde" />
              <span className="flex-1 bg-cian" />
              <span className="flex-1 bg-purpura" />
            </div>
          </div>
        </GlassCard>
      </div>
    </motion.div>
  )
}
