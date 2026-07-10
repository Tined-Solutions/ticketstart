import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/auth.js'

export default function RoleGuard({ allowedRoles, children, fallback = '/' }) {
  const { user, isAuthenticated } = useAuth()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (!allowedRoles.includes(user?.role)) {
    return <Navigate to={fallback} replace />
  }

  return children
}
