import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import Faq from './Faq.jsx'

const questions = [
  '¿Cómo compro entradas?',
  '¿Necesito una cuenta para comprar?',
  '¿Cómo veo mis entradas?',
  '¿Qué pasa si mi reserva expira antes de pagar?',
  '¿Qué medios de pago aceptan?',
  '¿Por qué me redirigen a Mercado Pago para pagar?',
  '¿Puedo pedir una devolución?',
  '¿Cómo sé que un evento es legítimo?',
  '¿Quién organiza los eventos?',
]

const answers = {
  '¿Cómo compro entradas?':
    'Elegí un evento del catálogo, seleccioná el tipo de entrada y la cantidad, y seguí el paso a paso para reservar y pagar. Al confirmar el pago, te enviamos las entradas con sus códigos QR al email del comprador.',
  '¿Necesito una cuenta para comprar?':
    'No. Podés comprar como invitado: solo necesitás un email válido y tu DNI para completar la reserva.',
  '¿Cómo veo mis entradas?':
    'Entrá a «Mis Entradas» desde el menú y buscá con el email y el DNI que usaste al comprar para consultar tus entradas. Las entradas se envían por email a la casilla del comprador: si no las recibiste, desde esa misma sección podés solicitar que te las reenvíen.',
  '¿Qué pasa si mi reserva expira antes de pagar?':
    'La reserva tiene un tiempo límite para completar el pago. Si se vence, las entradas se liberan y vuelven a estar disponibles; solo tenés que volver a intentar la compra.',
  '¿Qué medios de pago aceptan?':
    'Los pagos se procesan con Mercado Pago. Podés pagar con las tarjetas y los medios que Mercado Pago soporte.',
  '¿Por qué me redirigen a Mercado Pago para pagar?':
    'El pago se procesa en la plataforma de Mercado Pago para que los datos de tu tarjeta nunca pasen por nuestro sistema.',
  '¿Puedo pedir una devolución?':
    'Cada caso se evalúa de forma manual por el equipo de TicketStart. Si necesitás una devolución, escribinos y lo revisamos.',
  '¿Cómo sé que un evento es legítimo?':
    'Todos los eventos pasan por un proceso de aprobación del equipo antes de publicarse en el catálogo.',
  '¿Quién organiza los eventos?':
    'Los eventos son publicados y gestionados por organizadores independientes dentro de TicketStart.',
}

function renderFaq() {
  return render(
    <MemoryRouter>
      <Faq />
    </MemoryRouter>
  )
}

describe('Faq', () => {
  it('renders the page title and every group heading', () => {
    renderFaq()

    expect(
      screen.getByRole('heading', { name: 'Preguntas frecuentes' })
    ).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Compras y entradas' })
    ).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Pagos y devoluciones' })
    ).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Eventos' })
    ).toBeInTheDocument()
  })

  it('renders every question as a collapsed disclosure button', () => {
    renderFaq()

    questions.forEach((question) => {
      const button = screen.getByRole('button', { name: question })
      expect(button).toHaveAttribute('aria-expanded', 'false')
      expect(button).toHaveAttribute('aria-controls')
    })

    Object.values(answers).forEach((answer) => {
      expect(screen.queryByText(answer)).not.toBeInTheDocument()
    })
  })

  it('expands and collapses the answer when the question is clicked', async () => {
    const user = userEvent.setup()
    renderFaq()

    const button = screen.getByRole('button', { name: questions[0] })
    expect(button).toHaveAttribute('aria-expanded', 'false')

    await user.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText(answers[questions[0]])).toBeInTheDocument()

    await user.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'false')
    await waitFor(() => {
      expect(screen.queryByText(answers[questions[0]])).not.toBeInTheDocument()
    })
  })

  it('opens only the clicked question while others stay collapsed', async () => {
    const user = userEvent.setup()
    renderFaq()

    const clickedIndex = 4
    await user.click(
      screen.getByRole('button', { name: questions[clickedIndex] })
    )

    questions.forEach((question, index) => {
      const button = screen.getByRole('button', { name: question })
      expect(button).toHaveAttribute(
        'aria-expanded',
        index === clickedIndex ? 'true' : 'false'
      )
    })
  })

  it('toggles with the keyboard (Enter)', async () => {
    const user = userEvent.setup()
    renderFaq()

    const firstButton = screen.getByRole('button', { name: questions[0] })
    firstButton.focus()
    expect(firstButton).toHaveFocus()

    await user.keyboard('{Enter}')
    expect(firstButton).toHaveAttribute('aria-expanded', 'true')
  })
})
