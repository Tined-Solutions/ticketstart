import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { useAuth } from '../context/auth.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import Badge from '../components/ui/Badge.jsx'

function getRedirectPath(role) {
  if (role === 'Organizador') return '/organizer/dashboard'
  if (role === 'Staff') return '/staff/scan'
  if (role === 'Admin') return '/admin'
  return '/'
}

function isValidEmail(email) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
}

const shakeAnim = {
  x: [0, -6, 6, -6, 6, 0],
  transition: { duration: 0.35 },
}

export default function Login() {
  const navigate = useNavigate()
  const { login } = useAuth()

  const [formData, setFormData] = useState({
    email: '',
    password: '',
  })
  const [errors, setErrors] = useState({})
  const [apiError, setApiError] = useState('')
  const [loading, setLoading] = useState(false)
  const [shakeCard, setShakeCard] = useState(false)

  const updateField = (field) => (event) => {
    setFormData((prev) => ({ ...prev, [field]: event.target.value }))
    setErrors((prev) => ({ ...prev, [field]: '' }))
    setApiError('')
  }

  const validate = () => {
    const nextErrors = {}

    if (!formData.email.trim()) {
      nextErrors.email = 'El email es obligatorio'
    } else if (!isValidEmail(formData.email.trim())) {
      nextErrors.email = 'El formato del email no es valido'
    }

    if (!formData.password) {
      nextErrors.password = 'La contrasena es obligatoria'
    }

    setErrors(nextErrors)
    return Object.keys(nextErrors).length === 0
  }

  const handleSubmit = async (event) => {
    event.preventDefault()
    setApiError('')
    setShakeCard(false)

    if (!validate()) {
      return
    }

    setLoading(true)
    try {
      const user = await login(formData.email.trim(), formData.password)
      navigate(getRedirectPath(user.role), { replace: true })
    } catch (error) {
      const message =
        error.response?.data?.error?.message || error.response?.data?.message
      setApiError(message || 'Credenciales invalidas')
      setShakeCard(true)
    } finally {
      setLoading(false)
    }
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
      className="min-h-[80vh] flex items-center justify-center px-4 py-12"
    >
      <motion.div
        animate={shakeCard ? shakeAnim : {}}
        onAnimationComplete={() => setShakeCard(false)}
        className="w-full max-w-md"
      >
        <GlassCard className="p-8">
          <div className="text-center mb-8">
            <h1 className="text-3xl font-display font-bold bg-gradient-to-r from-brand-1 to-brand-2 bg-clip-text text-transparent mb-2">
              Iniciar sesion
            </h1>
            <p className="text-text-2 text-sm">TicketStar</p>
          </div>

          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            <div>
              <label
                htmlFor="email"
                className="block text-sm font-medium text-text-2 mb-1"
              >
                Email
              </label>
              <input
                id="email"
                type="email"
                value={formData.email}
                onChange={updateField('email')}
                disabled={loading}
                autoComplete="email"
                aria-invalid={errors.email ? 'true' : undefined}
                className={`w-full px-4 py-2.5 bg-surface-elevated border rounded-lg
                  text-text-1 placeholder:text-text-muted
                  focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                  transition-all duration-200
                  disabled:opacity-60 disabled:cursor-not-allowed
                  ${errors.email ? 'border-rose-400' : 'border-white/10'}`}
              />
              <AnimatePresence>
                {errors.email && (
                  <motion.p
                    initial={{ opacity: 0, y: -4 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -4 }}
                    className="text-rose-400 text-xs mt-1"
                    role="alert"
                  >
                    {errors.email}
                  </motion.p>
                )}
              </AnimatePresence>
            </div>

            <div>
              <label
                htmlFor="password"
                className="block text-sm font-medium text-text-2 mb-1"
              >
                Contrasena
              </label>
              <input
                id="password"
                type="password"
                value={formData.password}
                onChange={updateField('password')}
                disabled={loading}
                autoComplete="current-password"
                aria-invalid={errors.password ? 'true' : undefined}
                className={`w-full px-4 py-2.5 bg-surface-elevated border rounded-lg
                  text-text-1 placeholder:text-text-muted
                  focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
                  transition-all duration-200
                  disabled:opacity-60 disabled:cursor-not-allowed
                  ${errors.password ? 'border-rose-400' : 'border-white/10'}`}
              />
              <AnimatePresence>
                {errors.password && (
                  <motion.p
                    initial={{ opacity: 0, y: -4 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -4 }}
                    className="text-rose-400 text-xs mt-1"
                    role="alert"
                  >
                    {errors.password}
                  </motion.p>
                )}
              </AnimatePresence>
            </div>

            <AnimatePresence>
              {apiError && (
                <motion.div
                  initial={{ opacity: 0, scale: 0.95 }}
                  animate={{ opacity: 1, scale: 1 }}
                  exit={{ opacity: 0, scale: 0.95 }}
                >
                  <Badge variant="error" className="w-full justify-center px-4 py-2">
                    {apiError}
                  </Badge>
                </motion.div>
              )}
            </AnimatePresence>

            <Button
              type="submit"
              variant="gradient"
              size="lg"
              loading={loading}
              className="w-full"
            >
              {loading ? 'Ingresando...' : 'Ingresar'}
            </Button>
          </form>

          <p className="text-center text-text-2 text-sm mt-6">
            No tenes cuenta?{' '}
            <Link
              to="/register"
              className="text-brand-1 hover:text-brand-2 underline underline-offset-2 transition-colors"
            >
              Crear cuenta
            </Link>
          </p>
        </GlassCard>
      </motion.div>
    </motion.div>
  )
}
