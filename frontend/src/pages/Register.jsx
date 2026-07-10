import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/auth.js'

const ROLES = [
  { value: '', label: 'Seleccionar rol' },
  { value: 'Organizador', label: 'Organizador' },
  { value: 'Staff', label: 'Staff' },
]

function getRedirectPath(role) {
  if (role === 'Organizador') return '/organizer/dashboard'
  if (role === 'Staff') return '/staff/scan'
  return '/'
}

function isValidEmail(email) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
}

export default function Register() {
  const navigate = useNavigate()
  const { register } = useAuth()

  const [formData, setFormData] = useState({
    name: '',
    email: '',
    password: '',
    confirmPassword: '',
    role: '',
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

    if (!formData.name.trim()) {
      nextErrors.name = 'El nombre es obligatorio'
    } else if (formData.name.trim().length < 2) {
      nextErrors.name = 'El nombre debe tener al menos 2 caracteres'
    }

    if (!formData.email.trim()) {
      nextErrors.email = 'El email es obligatorio'
    } else if (!isValidEmail(formData.email.trim())) {
      nextErrors.email = 'El formato del email no es valido'
    }

    if (!formData.password) {
      nextErrors.password = 'La contrasena es obligatoria'
    } else if (formData.password.length < 8) {
      nextErrors.password = 'La contrasena debe tener al menos 8 caracteres'
    }

    if (!formData.confirmPassword) {
      nextErrors.confirmPassword = 'Debes confirmar la contrasena'
    } else if (formData.password !== formData.confirmPassword) {
      nextErrors.confirmPassword = 'Las contrasenas no coinciden'
    }

    if (!formData.role) {
      nextErrors.role = 'Debes seleccionar un rol'
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
      const user = await register(
        formData.name.trim(),
        formData.email.trim(),
        formData.password,
        formData.role
      )
      navigate(getRedirectPath(user.role), { replace: true })
    } catch (error) {
      const message =
        error.response?.data?.error?.message || error.response?.data?.message
      setApiError(message || 'Ocurrio un error al crear la cuenta')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-page">
      <h1>Crear cuenta</h1>
      <form onSubmit={handleSubmit} noValidate>
        <div className="form-group">
          <label htmlFor="name">Nombre</label>
          <input
            id="name"
            type="text"
            value={formData.name}
            onChange={updateField('name')}
            disabled={loading}
            autoComplete="name"
          />
          {errors.name && <span className="error">{errors.name}</span>}
        </div>

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
            autoComplete="new-password"
          />
          {errors.password && <span className="error">{errors.password}</span>}
        </div>

        <div className="form-group">
          <label htmlFor="confirmPassword">Confirmar contrasena</label>
          <input
            id="confirmPassword"
            type="password"
            value={formData.confirmPassword}
            onChange={updateField('confirmPassword')}
            disabled={loading}
            autoComplete="new-password"
          />
          {errors.confirmPassword && (
            <span className="error">{errors.confirmPassword}</span>
          )}
        </div>

        <div className="form-group">
          <label htmlFor="role">Rol</label>
          <select
            id="role"
            value={formData.role}
            onChange={updateField('role')}
            disabled={loading}
          >
            {ROLES.map((role) => (
              <option key={role.value} value={role.value}>
                {role.label}
              </option>
            ))}
          </select>
          {errors.role && <span className="error">{errors.role}</span>}
        </div>

        {apiError && <div className="error">{apiError}</div>}

        <button type="submit" disabled={loading}>
          {loading ? 'Creando cuenta...' : 'Registrarse'}
        </button>
      </form>

      <p>
        Ya tenes cuenta? <Link to="/login">Iniciar sesion</Link>
      </p>
    </div>
  )
}
