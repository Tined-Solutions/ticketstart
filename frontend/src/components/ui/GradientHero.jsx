import { motion } from 'framer-motion'
import { Music, Drama, Laugh, PartyPopper } from 'lucide-react'
import { fadeInUp, heroTransition, useReducedMotion } from '../../lib/motion.js'

// Representative icon per category, rendered as decorative hero chips.
const categoryIcons = {
  musica: Music,
  teatro: Drama,
  standup: Laugh,
  festivales: PartyPopper,
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
                  className="group relative flex h-11 w-11 items-center justify-center rounded-full border border-gris-oscuro/10 bg-white/60 text-gris-oscuro/70 shadow-sm backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:bg-white/90 hover:text-gris-oscuro motion-reduce:transition-none"
                  role="img"
                  aria-label={chip.label}
                >
                  {Icon && (
                    <Icon
                      strokeWidth={2}
                      className="relative h-5 w-5 transition-transform duration-300 group-hover:scale-110 motion-reduce:transition-none"
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
