import { useState, useEffect, useCallback } from 'react'
import { AuthContext } from './auth.js'
import apiClient from '../api/client.js'

export default function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false

    apiClient
      .get('/auth/me')
      .then((response) => {
        if (!cancelled) {
          const { id, email, name, role } = response.data
          setUser({ id, email, name, role })
        }
      })
      .catch(() => {
        // 401 or network error — user stays null (not authenticated)
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  const login = useCallback(async (email, password) => {
    const response = await apiClient.post('/auth/login', {
      email,
      password,
    })
    const { userId, role, name } = response.data
    const userData = { id: userId, email, role, name }
    setUser(userData)
    return userData
  }, [])

  const logout = useCallback(async () => {
    try {
      await apiClient.post('/auth/logout')
    } catch {
      // Proceed even if the server call fails
    }
    setUser(null)
    window.location.href = '/login'
  }, [])

  const value = {
    user,
    loading,
    isAuthenticated: !!user,
    login,
    logout,
  }

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  )
}
