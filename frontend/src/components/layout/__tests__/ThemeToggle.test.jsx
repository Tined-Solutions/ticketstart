import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ThemeProvider from '../../../hooks/useTheme.jsx'
import ThemeToggle from '../ThemeToggle.jsx'

const storage = {}
const mockLocalStorage = {
  getItem: vi.fn((key) => storage[key] ?? null),
  setItem: vi.fn((key, value) => {
    storage[key] = String(value)
  }),
}

function renderToggle() {
  return render(
    <ThemeProvider>
      <ThemeToggle />
    </ThemeProvider>
  )
}

describe('ThemeToggle', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', mockLocalStorage)
    Object.keys(storage).forEach((k) => delete storage[k])
    mockLocalStorage.getItem.mockClear()
    mockLocalStorage.setItem.mockClear()
    document.documentElement.removeAttribute('data-theme')
  })

  it('renders a toggle button with an accessible label', () => {
    mockLocalStorage.getItem.mockReturnValue('dark')
    renderToggle()
    const btn = screen.getByRole('button', { name: /cambiar a modo claro/i })
    expect(btn).toBeInTheDocument()
  })

  it('toggles theme from dark to light on click', async () => {
    mockLocalStorage.getItem.mockReturnValue('dark')
    renderToggle()
    const btn = screen.getByRole('button')

    expect(document.documentElement.getAttribute('data-theme')).toBe('dark')
    await userEvent.click(btn)
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
    expect(mockLocalStorage.setItem).toHaveBeenCalledWith('ticketera-theme', 'light')
    expect(btn).toHaveAttribute('aria-label', 'Cambiar a modo oscuro')
  })

  it('toggles back to dark on second click', async () => {
    mockLocalStorage.getItem.mockReturnValue('dark')
    renderToggle()
    const btn = screen.getByRole('button')

    await userEvent.click(btn) // dark → light
    await userEvent.click(btn) // light → dark

    expect(document.documentElement.getAttribute('data-theme')).toBe('dark')
    expect(mockLocalStorage.setItem).toHaveBeenCalledWith('ticketera-theme', 'dark')
  })

  it('renders sun icon when in dark mode', () => {
    mockLocalStorage.getItem.mockReturnValue('dark')
    renderToggle()
    const svg = document.querySelector('svg')
    expect(svg).toBeTruthy()
    expect(svg.querySelector('circle')).toBeTruthy()
  })

  it('renders moon icon when in light mode', () => {
    mockLocalStorage.getItem.mockReturnValue('light')
    renderToggle()
    const svg = document.querySelector('svg')
    expect(svg).toBeTruthy()
    expect(svg.querySelector('circle')).toBeFalsy()
  })
})
