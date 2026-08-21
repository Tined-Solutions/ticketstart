import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ThemeProvider, { useTheme } from '../useTheme.jsx'

// Helper component to inspect context values in tests
function ThemeConsumer() {
  const { theme, setTheme, toggle } = useTheme()
  return (
    <div>
      <span data-testid="theme-value">{theme}</span>
      <button data-testid="set-dark" onClick={() => setTheme('dark')}>
        Set Dark
      </button>
      <button data-testid="set-light" onClick={() => setTheme('light')}>
        Set Light
      </button>
      <button data-testid="toggle-theme" onClick={toggle}>
        Toggle
      </button>
    </div>
  )
}

describe('useTheme / ThemeProvider — light-only (brand 2.5)', () => {
  beforeEach(() => {
    document.documentElement.removeAttribute('data-theme')
  })

  it('always defaults to "light" regardless of localStorage', () => {
    // Even with a stored "dark" preference, the app stays pinned to light.
    vi.stubGlobal(
      'localStorage',
      { getItem: () => 'dark' },
    )
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    expect(screen.getByTestId('theme-value')).toHaveTextContent('light')
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
    vi.unstubAllGlobals()
  })

  it('applies data-theme="light" to <html> on mount', () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })

  it('setTheme is a no-op — theme stays light', async () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    await userEvent.click(screen.getByTestId('set-dark'))
    expect(screen.getByTestId('theme-value')).toHaveTextContent('light')
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })

  it('toggle is a no-op — theme stays light', async () => {
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    await userEvent.click(screen.getByTestId('toggle-theme'))
    expect(screen.getByTestId('theme-value')).toHaveTextContent('light')
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })

  it('throws when useTheme is used outside ThemeProvider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<ThemeConsumer />)).toThrow(
      'useTheme must be used within a ThemeProvider'
    )
    spy.mockRestore()
  })
})
