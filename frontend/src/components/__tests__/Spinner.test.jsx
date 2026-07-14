import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import Spinner from '../Spinner.jsx'

describe('Spinner', () => {
  it('renders with default label', () => {
    render(<Spinner />)
    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.getByLabelText('Cargando...')).toBeInTheDocument()
  })

  it('renders custom label', () => {
    render(<Spinner label="Buscando entradas..." />)
    expect(screen.getByLabelText('Buscando entradas...')).toBeInTheDocument()
  })

  it('renders with small size', () => {
    render(<Spinner size="sm" />)
    const spinner = screen.getByRole('status').querySelector('div[aria-hidden]')
    expect(spinner.className).toContain('h-4')
    expect(spinner.className).toContain('w-4')
  })

  it('renders with medium size by default', () => {
    render(<Spinner />)
    const spinner = screen.getByRole('status').querySelector('div[aria-hidden]')
    expect(spinner.className).toContain('h-8')
    expect(spinner.className).toContain('w-8')
  })

  it('renders with large size', () => {
    render(<Spinner size="lg" />)
    const spinner = screen.getByRole('status').querySelector('div[aria-hidden]')
    expect(spinner.className).toContain('h-12')
    expect(spinner.className).toContain('w-12')
  })

  it('the animated circle is hidden from screen readers', () => {
    render(<Spinner />)
    const status = screen.getByRole('status')
    const animated = status.querySelector('[aria-hidden="true"]')
    expect(animated).toBeInTheDocument()
  })

  it('has screen-reader-only text', () => {
    render(<Spinner label="Loading data" />)
    const srText = screen.getByText('Loading data')
    expect(srText.className).toContain('sr-only')
  })
})
