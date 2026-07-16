import { createContext, useContext, useState, useEffect, useCallback } from 'react'

const STORAGE_KEY = 'ticketera-theme'

const ThemeContext = createContext(null)

export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) {
    throw new Error('useTheme must be used within a ThemeProvider')
  }
  return context
}

function readStoredTheme() {
  try {
    return localStorage.getItem(STORAGE_KEY) || 'dark'
  } catch {
    return 'dark'
  }
}

function applyThemeToDOM(theme) {
  document.documentElement.setAttribute('data-theme', theme)
}

function persistTheme(theme) {
  try {
    localStorage.setItem(STORAGE_KEY, theme)
  } catch {
    // Storage unavailable — theme still applies for the session
  }
}

export default function ThemeProvider({ children }) {
  const [theme, setThemeState] = useState(readStoredTheme)

  // Sync data-theme attribute to <html> whenever theme state changes
  useEffect(() => {
    applyThemeToDOM(theme)
  }, [theme])

  const setTheme = useCallback((next) => {
    setThemeState(next)
    persistTheme(next)
  }, [])

  const toggle = useCallback(() => {
    setThemeState((prev) => {
      const next = prev === 'dark' ? 'light' : 'dark'
      persistTheme(next)
      return next
    })
  }, [])

  return (
    <ThemeContext.Provider value={{ theme, setTheme, toggle }}>
      {children}
    </ThemeContext.Provider>
  )
}
