import { useRef } from 'react'
import { motion } from 'framer-motion'
import { fadeInUp, heroTransition, useReducedMotion } from '../../lib/motion.js'
import { Disc3Icon } from '../icons/disc-3.jsx'
import { ClapIcon } from '../icons/clap.jsx'
import { LaughIcon } from '../icons/laugh.jsx'
import { PartyPopperIcon } from '../icons/party-popper.jsx'
import { PaletteIcon } from '../icons/palette.jsx'

// Representative icon per category, rendered as decorative hero chips.
const categoryIcons = {
  musica: Disc3Icon,
  teatro: ClapIcon,
  standup: LaughIcon,
  festivales: PartyPopperIcon,
  arte: PaletteIcon,
}

// Fill color per category, using the ORIGINAL logo colors (base tones from
// tokens.css, not the dark variants — the chip fill is decorative, so the
// AA text tones don't apply). Classes must stay full literals so the
// Tailwind v4 scanner picks them up; never interpolate the color name.
const chipFillColor = {
  naranja: 'bg-naranja',
  purpura: 'bg-purpura',
  amarillo: 'bg-amarillo',
  verde: 'bg-verde',
  cian: 'bg-cian',
}


/**
 * Light Confetti hero (brand 2.5 / 9).
 *
 * The hero always fills the full viewport (`min-h-svh`): the home navbar's
 * scroll-linked reveal is measured against this element's height (#home-hero),
 * so any smaller height desynchronizes the navbar's deployment. svh (not vh)
 * keeps the background unclipped on mobile browsers with dynamic UI.
 *
 * The Confetti language fills large surfaces with brand color, so the hero
 * uses a layered brand-tinted background (decorative, non-text) while the
 * title and subtitle stay in Gris Oscuro / text-2 to pass WCAG AA. When a
 * logo is provided it becomes the hero title, using a crop wrapper to remove
 * transparent padding from the source asset.
 */
export default function GradientHero({
  imageUrl,
  title,
  subtitle,
  cta,
  logo = null,
  chips = [],
}) {
  const shouldReduceMotion = useReducedMotion()

  // Refs to drive each chip icon imperatively: hovering anywhere on the chip
  // (not just the 20px icon) starts/stops its animation through the handle
  // every icon exposes. Passing a ref also switches the icon to controlled
  // mode, so its own hover no longer double-triggers.
  const iconRefs = useRef({})
  const startChipIcon = (id) => iconRefs.current[id]?.startAnimation?.()
  const stopChipIcon = (id) => iconRefs.current[id]?.stopAnimation?.()

  return (
    // #home-hero is the anchor the Navbar scroll-linked reveal measures
    // against; keep the id in sync with Navbar.jsx.
    <div
      id="home-hero"
      className="relative w-full min-h-svh flex items-center justify-center overflow-hidden bg-canvas"
    >
      {/* Confetti surface — saturated brand layers remain decorative */}
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-br from-naranja/35 via-amarillo/20 to-cian/35 z-0"
      />
      <div
        aria-hidden="true"
        className="absolute -left-24 top-8 h-72 w-72 rounded-full bg-purpura/30 blur-3xl z-[1]"
      />
      <div
        aria-hidden="true"
        className="absolute -right-20 bottom-0 h-80 w-80 rounded-full bg-verde/30 blur-3xl z-[1]"
      />
      <div
        aria-hidden="true"
        className="absolute inset-x-0 bottom-0 h-40 bg-gradient-to-t from-cian/20 via-canvas/25 to-transparent z-[2]"
      />

      {/* Background image (optional, below content) */}
      {imageUrl && (
        <img
          src={imageUrl}
          className="absolute inset-0 w-full h-full object-cover"
          alt=""
        />
      )}

      {/* Content */}
      <motion.div
        variants={fadeInUp}
        initial="initial"
        animate="animate"
        transition={shouldReduceMotion ? { duration: 0 } : heroTransition}
        className="relative z-10 text-center px-4 max-w-3xl py-12"
      >
        {logo ? (
          <motion.div
            initial={shouldReduceMotion ? false : { opacity: 0, scale: 0.8, rotate: -6, y: 14 }}
            animate={{ opacity: 1, scale: 1, rotate: 0, y: 0 }}
            transition={
              shouldReduceMotion
                ? { duration: 0 }
                : { duration: 0.3, ease: [0.16, 1, 0.3, 1], delay: 0.08 }
            }
            className="relative mx-auto mb-7 w-[min(88vw,32rem)] aspect-[2.6]"
          >
            <motion.span
              aria-hidden="true"
              initial={shouldReduceMotion ? false : { opacity: 0, scale: 0.65 }}
              animate={{ opacity: 0.5, scale: 1.15 }}
              transition={
                shouldReduceMotion
                  ? { duration: 0 }
                  : { duration: 0.3, ease: 'easeOut', delay: 0.14 }
              }
              className="absolute left-[18%] top-[28%] h-24 w-44 rounded-full bg-naranja/45 blur-3xl"
            />
            <motion.span
              aria-hidden="true"
              initial={shouldReduceMotion ? false : { opacity: 0, scale: 0.6 }}
              animate={{ opacity: 0.42, scale: 1.1 }}
              transition={
                shouldReduceMotion
                  ? { duration: 0 }
                  : { duration: 0.3, ease: 'easeOut', delay: 0.2 }
              }
              className="absolute right-[16%] top-[24%] h-28 w-40 rounded-full bg-purpura/40 blur-3xl"
            />
            <img
              src={logo}
              alt="TicketStart"
              width="1594"
              height="1063"
              className="relative h-full w-full object-cover object-center drop-shadow-[0_16px_18px_rgba(74,74,74,0.22)]"
            />
          </motion.div>
        ) : title ? (
          <h1 className="text-4xl md:text-6xl font-display font-bold text-gris-oscuro mb-4">
            {title}
          </h1>
        ) : null}

        {subtitle && (
          <p className="mx-auto max-w-2xl text-lg md:text-xl font-medium text-gris-oscuro mb-8">
            {subtitle}
          </p>
        )}

        {chips.length > 0 && (
          <motion.div
            initial={shouldReduceMotion ? false : 'hidden'}
            animate={shouldReduceMotion ? undefined : 'show'}
            className="flex flex-wrap items-center justify-center gap-3 mb-8"
          >
            {chips.map((chip, index) => {
              const Icon = categoryIcons[chip.id]
              return (
                <motion.span
                  key={chip.id}
                  onMouseEnter={() => startChipIcon(chip.id)}
                  onMouseLeave={() => stopChipIcon(chip.id)}
                  variants={{
                    hidden: { opacity: 0, y: -22 },
                    show: {
                      opacity: 1,
                      y: 0,
                      transition: {
                        duration: 0.5,
                        ease: [0.16, 1, 0.3, 1],
                        // Wait until the title/logo has been read, then fall in one by one.
                        delay: 0.7 + index * 0.08,
                      },
                    },
                  }}
                  className="group relative flex h-11 w-11 items-center justify-center rounded-full border border-gris-oscuro/10 bg-white/60 text-gris-oscuro/70 shadow-sm backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:border-transparent motion-reduce:transition-none"
                  role="img"
                  aria-label={chip.label}
                >
                  {/* Fill layer: rises from the bottom on hover, tinted with
                      the category's original logo color. The chip keeps
                      overflow visible for the tooltip; the layer itself is
                      rounded-full so it never spills outside the circle. */}
                  <span
                    aria-hidden="true"
                    className={`absolute inset-0 origin-bottom scale-y-0 rounded-full transition-transform duration-300 group-hover:scale-y-100 motion-reduce:transition-none ${chipFillColor[chip.colorKey] ?? ''}`}
                  />
                  {Icon && (
                    <Icon
                      ref={(node) => {
                        if (node) iconRefs.current[chip.id] = node
                        else delete iconRefs.current[chip.id]
                      }}
                      size={20}
                      className="relative"
                    />
                  )}

                  {/* Tooltip with the category name on hover */}
                  <span
                    role="tooltip"
                    className="pointer-events-none absolute -top-9 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-md bg-gris-oscuro px-2 py-1 text-xs font-medium text-white opacity-0 shadow-md transition-all duration-200 group-hover:-translate-y-0.5 group-hover:opacity-100 motion-reduce:transition-none"
                  >
                    {chip.label}
                    <span
                      aria-hidden="true"
                      className="absolute left-1/2 top-full -translate-x-1/2 border-4 border-transparent border-t-gris-oscuro"
                    />
                  </span>
                </motion.span>
              )
            })}
          </motion.div>
        )}

        {cta && <div>{cta}</div>}
      </motion.div>
    </div>
  )
}
