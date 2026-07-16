import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import EmptyState from '../EmptyState.jsx'

describe('EmptyState', () => {
  it('renders title and description', () => {
    render(<EmptyState title="No results" description="Try a different search." />)
    expect(screen.getByText('No results')).toBeInTheDocument()
    expect(screen.getByText('Try a different search.')).toBeInTheDocument()
  })

  it('renders an icon when provided', () => {
    render(<EmptyState icon="🔍" title="Empty" />)
    expect(screen.getByText('🔍')).toBeInTheDocument()
  })

  it('renders an action element when provided', () => {
    render(
      <EmptyState
        title="Nothing here"
        action={<button>Create one</button>}
      />
    )
    expect(screen.getByRole('button', { name: 'Create one' })).toBeInTheDocument()
  })

  it('does not render icon wrapper when icon is not provided', () => {
    render(<EmptyState title="Just title" />)
    // The icon wrapper div should not exist; only title and description elements
    expect(screen.queryByText('🔍')).toBeNull()
    expect(screen.getByText('Just title')).toBeInTheDocument()
  })

  it('does not render action wrapper when action is not provided', () => {
    render(<EmptyState title="No action" description="Desc" />)
    expect(screen.getByText('No action')).toBeInTheDocument()
    expect(screen.getByText('Desc')).toBeInTheDocument()
    // No button should be present
    expect(screen.queryByRole('button')).toBeNull()
  })

  it('renders all slots together', () => {
    render(
      <EmptyState
        icon="📭"
        title="Inbox zero"
        description="You have no messages."
        action={<a href="/compose">Compose</a>}
      />
    )
    expect(screen.getByText('📭')).toBeInTheDocument()
    expect(screen.getByText('Inbox zero')).toBeInTheDocument()
    expect(screen.getByText('You have no messages.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Compose' })).toBeInTheDocument()
  })

  it('renders without any props gracefully', () => {
    const { container } = render(<EmptyState />)
    // Should render an empty div with the base classes
    const div = container.firstChild
    expect(div).toBeTruthy()
    expect(div.className).toContain('text-center')
  })
})
