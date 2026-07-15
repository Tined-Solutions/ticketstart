import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/auth.js'

export default function RoleGuard({ allowedRoles, children, fallback = '/' }) {
  const { user, isAuthenticated } = useAuth()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (!allowedRoles.includes(user?.role)) {
    return (
      <div className="error-page">
        <h1>403 — Acceso denegado</h1>
        <p>No tenes permisos para acceder a esta pagina.</p>
        <a href={fallback}>Volver al inicio</a>
      </div>
    )
  }

  return children
}