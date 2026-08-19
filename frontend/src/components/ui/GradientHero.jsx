import { motion } from 'framer-motion'
import { fadeInUp, heroTransition } from '../../lib/motion.js'

/**
 * Light Confetti hero (brand 2.5 / 9).
 *
 * The Confetti language fills large surfaces with brand color, so the hero
 * uses a soft brand-tinted background (decorative, non-text) while the title
 * and subtitle stay in Gris Oscuro / text-2 to pass WCAG AA. Optional `logo`
 * and `chips` props render the brand logo (h-12) and the category chips.
 */
export default function GradientHero({
  imageUrl,
  title,
  subtitle,
  cta,
  logo = null,
  chips = [],
}) {
  return (
    <div className="relative w-full min-h-[60vh] flex items-center justify-center overflow-hidden">
      {/* Confetti surface — soft brand tint backdrop (decorative) */}
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-br from-naranja/15 via-purpura/10 to-cian/15 z-[1]"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-t from-canvas to-transparent z-[2]"
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
        transition={heroTransition}
        className="relative z-10 text-center px-4 max-w-3xl py-12"
      >
        {logo && (
          <div className="flex justify-center mb-6">
            <img
              src={logo}
              alt="TicketStart"
              width="48"
              height="48"
              className="h-12 w-auto"
            />
          </div>
        )}

        <h1 className="text-4xl md:text-6xl font-display font-bold text-gris-oscuro mb-4">
          {title}
        </h1>
        {subtitle && (
          <p className="text-lg md:text-xl text-text-2 mb-8">{subtitle}</p>
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
