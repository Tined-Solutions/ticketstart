import { useState, useCallback } from 'react'
import { AuthContext } from './auth.js'
import apiClient from '../api/client.js'

function readStoredUser() {
  const token = localStorage.getItem('token')
  const storedUser = localStorage.getItem('user')
  if (token && storedUser) {
    try {
      return JSON.parse(storedUser)
    } catch {
      localStorage.removeItem('token')
      localStorage.removeItem('user')
    }
  }
  return null
}

export default function AuthProvider({ children }) {
  const [user, setUser] = useState(readStoredUser)

  const persistSession = useCallback((token, userData) => {
    localStorage.setItem('token', token)
    localStorage.setItem('user', JSON.stringify(userData))
    setUser(userData)
  }, [])

  const login = useCallback(
    async (email, password) => {
      const response = await apiClient.post('/auth/login', {
        email,
        password,
      })
      const { token, userId, role, name } = response.data
      const userData = { id: userId, email, role, name }
      persistSession(token, userData)
      return userData
    },
    [persistSession]
  )

  const register = useCallback(
    async (name, email, password, role) => {
      const response = await apiClient.post('/auth/register', {
        name,
        email,
        password,
        role,
      })
      const { token, userId, role: returnedRole } = response.data
      const userData = { id: userId, name, email, role: returnedRole }
      persistSession(token, userData)
      return userData
    },
    [persistSession]
  )

  const logout = useCallback(() => {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    setUser(null)
    window.location.href = '/login'
  }, [])

  const value = {
    user,
    loading: false,
    isAuthenticated: !!user,
    login,
    register,
    logout,
  }

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  )
}
