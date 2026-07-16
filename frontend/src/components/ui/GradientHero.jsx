import { motion } from 'framer-motion'
import { fadeInUp, heroTransition } from '../../lib/motion.js'

export default function GradientHero({ imageUrl, title, subtitle, cta }) {
  return (
    <div className="relative w-full min-h-[60vh] flex items-center justify-center overflow-hidden">
      {/* Dark gradient overlay */}
      <div className="absolute inset-0 bg-gradient-to-b from-black/60 via-black/40 to-canvas z-[1]" />

      {/* Background image */}
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
        className="relative z-10 text-center px-4 max-w-3xl"
      >
        <h1 className="text-4xl md:text-6xl font-display font-bold text-white mb-4">
          {title}
        </h1>
        <p className="text-lg md:text-xl text-gray-300 mb-8">{subtitle}</p>
        {cta && <div>{cta}</div>}
      </motion.div>
    </div>
  )
}
