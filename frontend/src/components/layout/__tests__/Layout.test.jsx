import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Layout from '../Layout.jsx'

vi.mock('../../../context/auth.js', () => ({
  useAuth: vi.fn(),
}))

vi.mock('../../../hooks/useTheme.jsx', async (importOriginal) => {
  const original = await importOriginal()
  return {
    ...original,
    useTheme: vi.fn(),
  }
})

import { useAuth } from '../../../context/auth.js'
import { useTheme } from '../../../hooks/useTheme.jsx'

function renderLayout(children, initialEntries = ['/']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <Layout>{children}</Layout>
    </MemoryRouter>
  )
}

describe('Layout', () => {
  beforeEach(() => {
    useAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      logout: vi.fn(),
    })
    useTheme.mockReturnValue({
      theme: 'dark',
      setTheme: vi.fn(),
      toggle: vi.fn(),
    })
  })

  it('renders Navbar, main, and Footer', () => {
    renderLayout(<div>content</div>)

    expect(screen.getByText('TicketStart')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /powered by TicketStart/i })).toBeInTheDocument()
    expect(document.querySelector('main')).toBeTruthy()
  })

  it('renders children inside main', () => {
    renderLayout(<div data-testid="child">hello</div>)

    const main = document.querySelector('main')
    const child = screen.getByTestId('child')
    expect(child).toBeInTheDocument()
    expect(child.closest('main')).toBe(main)
  })

  it('re-keys children on location.pathname change', async () => {
    const { rerender } = renderLayout(<div>page-a</div>, ['/page-a'])
    expect(screen.getByText('page-a')).toBeInTheDocument()

    rerender(
      <MemoryRouter initialEntries={['/page-b']}>
        <Layout>
          <div>page-b</div>
        </Layout>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByText('page-b')).toBeInTheDocument()
      expect(screen.queryByText('page-a')).not.toBeInTheDocument()
    })
  })
})
