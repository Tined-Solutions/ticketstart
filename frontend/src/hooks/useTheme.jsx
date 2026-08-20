/* eslint-disable react-refresh/only-export-components --
   This is a context module: the useTheme hook and ThemeProvider are inherently
   coupled (they share ThemeContext). Fast Refresh is a DX optimization, not a
   runtime concern, so the coupled exports are acceptable here. */
import { createContext, useContext, useEffect, useCallback } from 'react'

const ThemeContext = createContext(null)

export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) {
    throw new Error('useTheme must be used within a ThemeProvider')
  }
  return context
}

// Light-only MVP (brand 2.5). The theme is pinned to 'light'; the
// toggle/setTheme are no-ops so consumers keep working but the app stays
// light. data-theme="light" is applied to <html> on mount to preserve the
// mechanism for future dark mode.
const LIGHT = 'light'

function applyThemeToDOM(theme) {
  document.documentElement.setAttribute('data-theme', theme)
}

export default function ThemeProvider({ children }) {
  // Sync data-theme attribute to <html> — always light.
  useEffect(() => {
    applyThemeToDOM(LIGHT)
  }, [])

  const setTheme = useCallback(() => {}, [])

  const toggle = useCallback(() => {}, [])

  return (
    <ThemeContext.Provider value={{ theme: LIGHT, setTheme, toggle }}>
      {children}
    </ThemeContext.Provider>
  )
}
