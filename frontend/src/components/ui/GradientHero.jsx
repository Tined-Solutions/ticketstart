import { motion } from 'framer-motion'
import { fadeInUp, heroTransition, useReducedMotion } from '../../lib/motion.js'

/**
 * Light Confetti hero (brand 2.5 / 9).
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
    <div className="relative w-full min-h-[68vh] flex items-center justify-center overflow-hidden bg-canvas">
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
          <div className="flex flex-wrap justify-center gap-2 mb-8">
            {chips.map((chip) => (
              <span
                key={chip.id}
                className={`${chip.chipClass} rounded-full px-4 py-1.5 font-medium text-sm`}
              >
                {chip.label}
              </span>
            ))}
          </div>
        )}

        {cta && <div>{cta}</div>}
      </motion.div>
    </div>
  )
}
