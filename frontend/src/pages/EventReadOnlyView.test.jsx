import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EventReadOnlyView from './EventReadOnlyView.jsx'

const mockNavigate = vi.fn()
const mockUseManagementEvent = vi.fn()

vi.mock('react-router-dom', () => ({
  useParams: () => ({ id: 'event-1' }),
  useNavigate: () => mockNavigate,
}))

vi.mock('../hooks/useManagementEvent.js', () => ({
  useManagementEvent: (...args) => mockUseManagementEvent(...args),
}))

// EventForm renders inside the view; in readOnly mode it never calls the API,
// so a stub keeps the module free of real network wiring.
vi.mock('../api/client.js', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}))

const eventData = {
  id: 'event-1',
  name: 'Recital de Rock Nacional',
  date: '2026-12-25T20:00:00Z',
  location: 'Estadio Luna Park, Buenos Aires',
  description: 'Un gran recital',
  imageUrl: 'https://example.com/rock.jpg',
  ticketTypes: [],
}

describe('EventReadOnlyView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockNavigate.mockReset()
    mockUseManagementEvent.mockReset()
  })

  it('shows the loading state while the event is being fetched', () => {
    mockUseManagementEvent.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    })

    render(<EventReadOnlyView />)

    expect(screen.getByText(/cargando evento/i)).toBeInTheDocument()
  })

  it('shows the error message and a Volver button when the fetch fails', async () => {
    mockUseManagementEvent.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { response: { data: { error: { message: 'Error de conexion' } } } },
    })

    render(<EventReadOnlyView />)

    expect(screen.getByText(/error de conexion/i)).toBeInTheDocument()

    const backBtn = screen.getByRole('button', { name: /volver/i })
    await userEvent.click(backBtn)
    expect(mockNavigate).toHaveBeenCalledWith(-1)
  })

  it('renders the "Ver evento" heading and a readOnly EventForm on success', () => {
    mockUseManagementEvent.mockReturnValue({
      data: eventData,
      isLoading: false,
      isError: false,
      error: null,
    })

    render(<EventReadOnlyView />)

    expect(
      screen.getByRole('heading', { name: /ver evento/i })
    ).toBeInTheDocument()

    // readOnly is forwarded to EventForm: inputs are disabled, the submit button
    // and the image upload control are hidden, and the event data is pre-filled.
    expect(screen.getByLabelText(/nombre del evento/i)).toBeDisabled()
    expect(screen.getByLabelText(/fecha y hora/i)).toBeDisabled()
    expect(screen.getByLabelText(/^ubicacion/i)).toBeDisabled()
    expect(screen.getByLabelText(/descripcion/i)).toBeDisabled()
    expect(
      screen.queryByRole('button', { name: /guardar cambios/i })
    ).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/imagen del evento/i)).not.toBeInTheDocument()
  })
})