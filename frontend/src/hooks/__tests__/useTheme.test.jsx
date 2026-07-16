import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ThemeProvider, { useTheme } from '../useTheme.jsx'

const STORAGE_KEY = 'ticketera-theme'

// Mock localStorage — jsdom in vitest may not expose the full Storage API
const storage = {}
const mockLocalStorage = {
  getItem: vi.fn((key) => storage[key] ?? null),
  setItem: vi.fn((key, value) => {
    storage[key] = String(value)
  }),
  removeItem: vi.fn((key) => {
    delete storage[key]
  }),
}

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

describe('useTheme / ThemeProvider', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', mockLocalStorage)
    Object.keys(storage).forEach((k) => delete storage[k])
    mockLocalStorage.getItem.mockClear()
    mockLocalStorage.setItem.mockClear()
    mockLocalStorage.removeItem.mockClear()
    document.documentElement.removeAttribute('data-theme')
  })

  it('defaults to "dark" when no localStorage value exists', () => {
    mockLocalStorage.getItem.mockReturnValue(null)
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    expect(screen.getByTestId('theme-value')).toHaveTextContent('dark')
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark')
  })

  it('reads the stored theme from localStorage', () => {
    mockLocalStorage.getItem.mockReturnValue('light')
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    expect(screen.getByTestId('theme-value')).toHaveTextContent('light')
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })

  it('setTheme changes the theme and persists to localStorage', async () => {
    mockLocalStorage.getItem.mockReturnValue('dark')
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    await userEvent.click(screen.getByTestId('set-light'))
    expect(screen.getByTestId('theme-value')).toHaveTextContent('light')
    expect(mockLocalStorage.setItem).toHaveBeenCalledWith(STORAGE_KEY, 'light')
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })

  it('toggle flips between dark and light', async () => {
    mockLocalStorage.getItem.mockReturnValue('dark')
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    expect(screen.getByTestId('theme-value')).toHaveTextContent('dark')

    await userEvent.click(screen.getByTestId('toggle-theme'))
    expect(screen.getByTestId('theme-value')).toHaveTextContent('light')
    expect(mockLocalStorage.setItem).toHaveBeenCalledWith(STORAGE_KEY, 'light')

    await userEvent.click(screen.getByTestId('toggle-theme'))
    expect(screen.getByTestId('theme-value')).toHaveTextContent('dark')
    expect(mockLocalStorage.setItem).toHaveBeenCalledWith(STORAGE_KEY, 'dark')
  })

  it('syncs data-theme attribute on <html> on every change', async () => {
    mockLocalStorage.getItem.mockReturnValue('dark')
    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark')

    await userEvent.click(screen.getByTestId('toggle-theme'))
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })

  it('throws when useTheme is used outside ThemeProvider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => render(<ThemeConsumer />)).toThrow(
      'useTheme must be used within a ThemeProvider'
    )
    spy.mockRestore()
  })

  it('survives localStorage being unavailable', () => {
    // Simulate broken localStorage
    const brokenStorage = {
      getItem: vi.fn(() => {
        throw new Error('Quota exceeded')
      }),
      setItem: vi.fn(() => {
        throw new Error('Quota exceeded')
      }),
    }
    vi.stubGlobal('localStorage', brokenStorage)

    render(
      <ThemeProvider>
        <ThemeConsumer />
      </ThemeProvider>
    )

    // Should still render with default dark theme, no crash
    expect(screen.getByTestId('theme-value')).toHaveTextContent('dark')
  })
})
