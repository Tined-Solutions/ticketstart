import { describe, it, expect, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EventForm from '../EventForm.jsx'

vi.mock('../../api/client.js', () => ({
  default: {
    post: vi.fn(),
    put: vi.fn(),
  },
}))

function renderCreateForm(initialData) {
  return render(
    <EventForm mode="create" initialData={initialData} onSuccess={vi.fn()} />
  )
}

async function addTicketTypeRow(user) {
  await user.click(
    screen.getByRole('button', { name: /agregar tipo de entrada/i })
  )
}

async function fillTicketTypeRow(user, index, { name, price, quantity = '10' }) {
  const fieldset = screen.getByRole('group', { name: /tipos de entrada/i })
  const names = within(fieldset).getAllByLabelText('Nombre')
  const prices = within(fieldset).getAllByLabelText('Precio ($)')
  const quantities = within(fieldset).getAllByLabelText('Cantidad')

  if (name !== undefined) {
    await user.clear(names[index])
    if (name) await user.type(names[index], name)
  }
  if (price !== undefined) {
    await user.clear(prices[index])
    if (price) await user.type(prices[index], price)
  }
  if (quantity !== undefined) {
    await user.clear(quantities[index])
    if (quantity) await user.type(quantities[index], quantity)
  }
}

function previewItems() {
  const list = screen.queryByRole('list')
  return list
    ? within(list).getAllByRole('listitem').map((item) => item.textContent)
    : []
}

describe('EventForm — ticket-type ordering preview (create mode)', () => {
  it('shows the ordering hint in create mode', () => {
    renderCreateForm()

    expect(
      screen.getByText(
        'En la página del evento se muestran de la más cara a la más barata.'
      )
    ).toBeInTheDocument()
  })

  it('previews valid ticket types by price descending, ties by name ascending', async () => {
    const user = userEvent.setup()
    renderCreateForm()

    // Editable rows stay in insertion order; only the preview is ordered.
    await fillTicketTypeRow(user, 0, { name: 'VIP', price: '300' })
    await addTicketTypeRow(user)
    await fillTicketTypeRow(user, 1, { name: 'Zeta', price: '100' })
    await addTicketTypeRow(user)
    await fillTicketTypeRow(user, 2, { name: 'Alfa', price: '100' })

    expect(
      screen.getByText('Así se van a mostrar en la página del evento:')
    ).toBeInTheDocument()
    expect(previewItems()).toEqual(['VIP — $ 300', 'Alfa — $ 100', 'Zeta — $ 100'])
  })

  it('excludes rows without a name or without a valid price', async () => {
    const user = userEvent.setup()
    renderCreateForm()

    await fillTicketTypeRow(user, 0, { name: 'Completa', price: '500' })
    await addTicketTypeRow(user)
    await fillTicketTypeRow(user, 1, { name: '   ', price: '200' })
    await addTicketTypeRow(user)
    await fillTicketTypeRow(user, 2, { name: 'Sin precio', price: '' })

    expect(previewItems()).toEqual(['Completa — $ 500'])
  })

  it('does not render the ordering preview in readOnly mode', () => {
    // EventReadOnlyView renders <EventForm mode="edit" readOnly /> — the
    // create-mode preview and its hint must not leak into that branch.
    render(
      <EventForm
        mode="edit"
        readOnly
        initialData={{
          id: 'ev-1',
          ticketTypes: [
            { id: 'tt-1', name: 'General', price: 5000, quantity: 100 },
          ],
        }}
      />
    )

    expect(
      screen.queryByText(/así se van a mostrar en la página del evento/i)
    ).not.toBeInTheDocument()
    expect(
      screen.queryByText(/de la más cara a la más barata/i)
    ).not.toBeInTheDocument()
  })
})
