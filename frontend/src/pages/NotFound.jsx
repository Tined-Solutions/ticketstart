import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import Button from '../components/Button.jsx'

export default function NotFound() {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
      className="flex flex-col items-center justify-center min-h-[60vh] text-center px-4"
    >
      <motion.h1
        initial={{ scale: 0.8, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ delay: 0.1, duration: 0.6, ease: [0.4, 0, 0.2, 1] }}
        className="text-8xl md:text-9xl font-display font-bold text-brand-1 mb-4"
        style={{ fontFamily: 'var(--font-display)' }}
      >
        404
      </motion.h1>

      <p className="text-xl text-text-2 mb-8 max-w-md">
        The page you're looking for doesn't exist or has been moved.
      </p>

      <Link to="/">
        <Button variant="gradient" size="lg">
          Go Home
        </Button>
      </Link>
    </motion.div>
  )
}
