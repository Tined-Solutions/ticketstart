import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from './App.jsx'

describe('App routing', () => {
  it('shows 404 page when navigating to /register', () => {
    render(
      <MemoryRouter initialEntries={['/register']}>
        <App />
      </MemoryRouter>
    )

    expect(screen.getByText(/404/i)).toBeInTheDocument()
    expect(screen.getByText(/pagina no encontrada/i)).toBeInTheDocument()
  })
})
