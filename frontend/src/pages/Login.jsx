import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/auth.js'

function getRedirectPath(role) {
  if (role === 'Organizador') return '/organizer/dashboard'
  if (role === 'Staff') return '/staff/scan'
  if (role === 'Admin') return '/admin'
  return '/'
}

function isValidEmail(email) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
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
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-page">
      <h1>Iniciar sesion</h1>
      <form onSubmit={handleSubmit} noValidate>
        <div className="form-group">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            type="email"
            value={formData.email}
            onChange={updateField('email')}
            disabled={loading}
            autoComplete="email"
          />
          {errors.email && <span className="error">{errors.email}</span>}
        </div>

        <div className="form-group">
          <label htmlFor="password">Contrasena</label>
          <input
            id="password"
            type="password"
            value={formData.password}
            onChange={updateField('password')}
            disabled={loading}
            autoComplete="current-password"
          />
          {errors.password && <span className="error">{errors.password}</span>}
        </div>

        {apiError && <div className="error">{apiError}</div>}

        <button type="submit" disabled={loading}>
          {loading ? 'Ingresando...' : 'Ingresar'}
        </button>
      </form>

      <p>
        No tenes cuenta? <Link to="/register">Crear cuenta</Link>
      </p>
    </div>
  )
}
