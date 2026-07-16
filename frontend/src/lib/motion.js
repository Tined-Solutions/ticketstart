import { useReducedMotion } from 'framer-motion'

// ═══ CSS Token Mirror ═══
// These constants mirror the --dur-* and --ease-* custom properties
// defined in index.css @theme inline block. Durations are in seconds
// for framer-motion compatibility; easings are cubic-bezier arrays.

const DUR = {
  micro: 0.2,
  normal: 0.4,
  slow: 0.6,
}

const EASE = {
  micro: [0.2, 0.6, 0.2, 1],
  smooth: [0.4, 0, 0.2, 1],
}

// ═══ Reduced Motion ═══

/**
 * SSR-safe static check. Prefer useReducedMotion() hook inside components
 * so the result stays reactive to OS-level changes during a session.
 */
export function prefersReducedMotion() {
  if (typeof window === 'undefined') return false
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

export { useReducedMotion }

// ═══ Reusable Animation Presets (variants) ═══

export const fadeIn = {
  initial: { opacity: 0 },
  animate: { opacity: 1 },
  exit: { opacity: 0 },
}

export const fadeInUp = {
  initial: { opacity: 0, y: 24 },
  animate: { opacity: 1, y: 0 },
  exit: { opacity: 0, y: -12 },
}

export const fadeInScale = {
  initial: { opacity: 0, scale: 0.95 },
  animate: { opacity: 1, scale: 1 },
  exit: { opacity: 0, scale: 0.95 },
}

export const staggerContainer = {
  animate: {
    transition: {
      staggerChildren: 0.08,
      delayChildren: 0.1,
    },
  },
}

export const staggerItem = {
  initial: { opacity: 0, y: 16 },
  animate: {
    opacity: 1,
    y: 0,
    transition: { duration: DUR.normal, ease: EASE.smooth },
  },
}

// ═══ Transition Configs (for motion.div transition prop) ═══

export const pageTransition = {
  duration: DUR.normal,
  ease: EASE.smooth,
}

export const heroTransition = {
  duration: DUR.slow,
  ease: EASE.smooth,
}

export const microTransition = {
  duration: DUR.micro,
  ease: EASE.micro,
}
