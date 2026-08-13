import { describe, it, expect, vi, beforeEach, afterEach, beforeAll } from 'vitest'
import { screen, waitFor, act, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import StaffScan from './StaffScan.jsx'
import { renderWithQueryClient } from '../test/queryClientUtils.jsx'

// ---------------------------------------------------------------------------
// Module-level state for html5-qrcode mock
// ---------------------------------------------------------------------------

let capturedSuccessCallback = null
let fakeIsScanning = false
let shouldFailCamera = false

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

const mockPost = vi.fn()
const mockGet = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
}))

vi.mock('../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
    get: (...args) => mockGet(...args),
  },
}))

vi.mock('../context/auth.js', () => ({
  useAuth: () => ({
    user: { id: 'staff-1', email: 'staff@test.com', role: 'Staff' },
    token: 'mock-staff-token',
  }),
}))

// ---------------------------------------------------------------------------
// sessionStorage mock
// ---------------------------------------------------------------------------

const sessionStore = {}

beforeAll(() => {
  vi.stubGlobal('sessionStorage', {
    getItem: vi.fn((key) => sessionStore[key] || null),
    setItem: vi.fn((key, value) => { sessionStore[key] = value }),
  })
})

vi.mock('html5-qrcode', () => ({
  Html5Qrcode: vi.fn().mockImplementation(function (elementId) {
    this._elementId = elementId
    this.start = vi.fn().mockImplementation(
      async (_cameraConfig, _scanConfig, successCallback) => {
        if (shouldFailCamera) {
          throw new Error('Permission denied')
        }
        capturedSuccessCallback = successCallback
        fakeIsScanning = true
        return null
      }
    )
    this.stop = vi.fn().mockImplementation(async () => {
      fakeIsScanning = false
    })
    Object.defineProperty(this, 'isScanning', {
      get: () => fakeIsScanning,
      configurable: true,
    })
  }),
}))

// ---------------------------------------------------------------------------
// Web Audio API mock
// ---------------------------------------------------------------------------

const mockOsc = () => ({
  type: '',
  frequency: { value: 0, setValueAtTime: vi.fn() },
  connect: vi.fn(),
  start: vi.fn(),
  stop: vi.fn(),
})

const mockGainNode = () => ({
  gain: { value: 0, setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() },
  connect: vi.fn(),
})

let audioCtxInstance

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const eventId = 'b3e4f5a1-2222-4d4d-9d9d-111111111111'

const mockEvents = [
  {
    id: 'b3e4f5a1-2222-4d4d-9d9d-111111111111',
    name: 'Rock en el Parque',
    date: '2026-08-15T21:00:00Z',
    location: 'Estadio Monumental',
  },
  {
    id: 'c4d5e6f2-3333-5e5e-0e0e-222222222222',
    name: 'Jazz Night',
    date: '2026-09-20T20:00:00Z',
    location: 'Teatro Colon',
  },
]

const mockTicketDetails = {
  id: 'ticket-001',
  eventName: 'Recital de Rock Nacional',
  ticketTypeName: 'Platea',
  purchaserEmail: 'comprador@test.com',
  isUsed: true,
  usedAt: '2026-08-15T21:30:00Z',
}

const successResponse = {
  data: {
    isValid: true,
    error: null,
    ticket: mockTicketDetails,
  },
}

const alreadyUsedResponse = {
  data: {
    isValid: false,
    error: 'Ticket already used on 2026-08-15 21:30:00 UTC.',
    ticket: { ...mockTicketDetails, isUsed: true },
  },
}

const invalidSignatureResponse = {
  data: {
    isValid: false,
    error: 'Invalid QR code signature. This ticket may be fraudulent.',
    ticket: null,
  },
}

// ---------------------------------------------------------------------------
// Lifecycle
// ---------------------------------------------------------------------------

beforeEach(() => {
  vi.clearAllMocks()
  mockPost.mockReset()
  mockGet.mockReset()
  capturedSuccessCallback = null
  fakeIsScanning = false
  shouldFailCamera = false
  // Clear sessionStorage mock between tests
  Object.keys(sessionStore).forEach((k) => delete sessionStore[k])

  // Default: events fetch succeeds
  mockGet.mockResolvedValue({ data: mockEvents })

  audioCtxInstance = {
    currentTime: 123,
    createOscillator: vi.fn(() => mockOsc()),
    createGain: vi.fn(() => mockGainNode()),
  }

  if (!window.AudioContext) {
    Object.defineProperty(window, 'AudioContext', {
      writable: true,
      value: vi.fn(() => audioCtxInstance),
    })
  } else {
    vi.spyOn(window, 'AudioContext').mockImplementation(() => audioCtxInstance)
  }
})

afterEach(() => {
  vi.restoreAllMocks()
})

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function simulateQrScan(qrData = 'ticket-001:1750000000:abc123sig') {
  if (capturedSuccessCallback) {
    capturedSuccessCallback(qrData)
  }
}

async function startScanning(user) {
  const select = await screen.findByLabelText(/^evento$/i)
  await user.selectOptions(select, eventId)
  await user.click(screen.getByRole('button', { name: /iniciar escaneo/i }))
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('StaffScan', () => {
  // -- Event selector & data fetching ------------------------------------

  it('renders the event selector with fetched events', async () => {
    renderWithQueryClient(<StaffScan />)
    const select = await screen.findByLabelText(/^evento$/i)
    expect(select).toBeInTheDocument()

    const options = Array.from(select.options)
    expect(options).toHaveLength(3) // placeholder + 2 events
    expect(options[0].textContent).toBe('Seleccionar evento...')
    expect(options[1].textContent).toMatch(/Rock en el Parque/)
    expect(options[1].textContent).toMatch(/Estadio Monumental/)
    expect(options[2].textContent).toMatch(/Jazz Night/)
    expect(options[2].textContent).toMatch(/Teatro Colon/)

    // The option must include the event time in 24h local format so staff can
    // tell apart same-day events. Computed dynamically so the assertion is
    // timezone-robust (the API returns UTC and the browser renders local time).
    const expectedTime1 = new Date('2026-08-15T21:00:00Z').toLocaleTimeString('es-AR', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    })
    const expectedTime2 = new Date('2026-09-20T20:00:00Z').toLocaleTimeString('es-AR', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    })
    expect(options[1].textContent).toContain(expectedTime1)
    expect(options[2].textContent).toContain(expectedTime2)
  })

  it('the UUID is never displayed to the user', async () => {
    renderWithQueryClient(<StaffScan />)
    const select = await screen.findByLabelText(/^evento$/i)
    const options = Array.from(select.options)
    for (const opt of options) {
      if (opt.value) {
        expect(opt.textContent).not.toMatch(
          /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i
        )
      }
    }
  })

  it('shows loading state initially', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))
    renderWithQueryClient(<StaffScan />)
    // Spinner renders a screen-reader-only span plus the visible text
    expect(screen.getAllByText(/cargando eventos/i).length).toBeGreaterThan(0)
    expect(screen.queryByLabelText(/^evento$/i)).not.toBeInTheDocument()
  })

  it('shows error when fetch fails', async () => {
    mockGet.mockRejectedValueOnce(new Error('Network error'))
    renderWithQueryClient(<StaffScan />)
    await waitFor(() => {
      expect(screen.getByText(/no se pudieron cargar los eventos/i)).toBeInTheDocument()
    })
  })

  // -- Rendering ----------------------------------------------------------

  it('renders the page with heading and event selector', async () => {
    renderWithQueryClient(<StaffScan />)
    expect(screen.getByRole('heading', { name: /escanear qr/i })).toBeInTheDocument()
    expect(await screen.findByLabelText(/^evento$/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /iniciar escaneo/i })).toBeInTheDocument()
  })

  it('does not show history when there are no scans', async () => {
    renderWithQueryClient(<StaffScan />)
    await screen.findByLabelText(/^evento$/i)
    expect(screen.queryByRole('heading', { name: /historial/i })).not.toBeInTheDocument()
  })

  // -- Validation ---------------------------------------------------------

  it('shows an error when trying to scan without selecting an event', async () => {
    renderWithQueryClient(<StaffScan />)
    await screen.findByLabelText(/^evento$/i)
    await userEvent.click(screen.getByRole('button', { name: /iniciar escaneo/i }))
    expect(screen.getByText(/debe seleccionar un evento/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('clears the validation error when the user selects an event', async () => {
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await screen.findByLabelText(/^evento$/i)

    await userEvent.click(screen.getByRole('button', { name: /iniciar escaneo/i }))
    expect(screen.getByText(/debe seleccionar un evento/i)).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText(/^evento$/i), eventId)
    expect(screen.queryByText(/debe seleccionar un evento/i)).not.toBeInTheDocument()
  })

  // -- Selecting an event enables the scan button -------------------------

  it('selecting an event enables the scan button', async () => {
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    const select = await screen.findByLabelText(/^evento$/i)
    await user.selectOptions(select, eventId)
    await user.click(screen.getByRole('button', { name: /iniciar escaneo/i }))
    expect(screen.getByRole('button', { name: /detener escaneo/i })).toBeInTheDocument()
  })

  // -- Scanning lifecycle -------------------------------------------------

  it('starts the camera and shows the stop button when event is selected', async () => {
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)
    expect(screen.getByRole('button', { name: /detener escaneo/i })).toBeInTheDocument()
  })

  it('shows a camera error message when camera access is denied', async () => {
    shouldFailCamera = true
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)
    await waitFor(() => {
      expect(screen.getByText(/no se pudo acceder a la cámara/i)).toBeInTheDocument()
    })
  })

  it('stops the scanner when the stop button is clicked', async () => {
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)
    expect(screen.getByRole('button', { name: /detener escaneo/i })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /detener escaneo/i }))
    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /detener escaneo/i })).not.toBeInTheDocument()
    })
  })

  // -- Successful scan ----------------------------------------------------

  it('calls the validation API and shows a success result with audio beep', async () => {
    mockPost.mockResolvedValueOnce(successResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /detener escaneo/i })).not.toBeInTheDocument()
    })

    expect(mockPost).toHaveBeenCalledWith('/tickets/validate', {
      qrCodeData: 'ticket-001:1750000000:abc123sig',
      eventId,
    })

    const resultAlert = screen.getByRole('alert')
    expect(resultAlert).toBeInTheDocument()
    expect(within(resultAlert).getByText(/ticket válido/i)).toBeInTheDocument()
    expect(within(resultAlert).getByText(/recital de rock nacional/i)).toBeInTheDocument()
    expect(within(resultAlert).getByText(/platea/i)).toBeInTheDocument()
    expect(within(resultAlert).getByText(/comprador@test.com/i)).toBeInTheDocument()

    expect(window.AudioContext).toHaveBeenCalled()
  })

  it('shows the "Escanear Otro" button after a scan result', async () => {
    mockPost.mockResolvedValueOnce(successResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    expect(screen.getByRole('button', { name: /escanear otro/i })).toBeInTheDocument()
  })

  // -- Failed scan --------------------------------------------------------

  it('shows an error result when the ticket is already used', async () => {
    mockPost.mockResolvedValueOnce(alreadyUsedResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    const resultAlert = screen.getByRole('alert')
    expect(resultAlert).toBeInTheDocument()
    expect(
      within(resultAlert).getByText(/ticket already used on 2026-08-15 21:30:00 UTC/i)
    ).toBeInTheDocument()
    expect(window.AudioContext).toHaveBeenCalled()
  })

  it('shows an error result when the QR signature is invalid', async () => {
    mockPost.mockResolvedValueOnce(invalidSignatureResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    const resultAlert = screen.getByRole('alert')
    expect(resultAlert).toBeInTheDocument()
    expect(
      within(resultAlert).getByText(/invalid qr code signature/i)
    ).toBeInTheDocument()
  })

  it('shows a connection error when the API call fails', async () => {
    mockPost.mockRejectedValueOnce(new Error('Network error'))
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    const resultAlert = screen.getByRole('alert')
    expect(resultAlert).toBeInTheDocument()
    expect(
      within(resultAlert).getByText(/error de conexión al validar el ticket/i)
    ).toBeInTheDocument()
  })

  it('shows a backend error message when the API returns a structured error', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: { message: 'No hay entradas para este evento' } } },
    })
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    const resultAlert = screen.getByRole('alert')
    expect(resultAlert).toBeInTheDocument()
    expect(
      within(resultAlert).getByText(/no hay entradas para este evento/i)
    ).toBeInTheDocument()
  })

  it('handles a plain-string error from the backend', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: 'QRCodeData is required' } },
    })
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    const resultAlert = screen.getByRole('alert')
    expect(resultAlert).toBeInTheDocument()
    expect(
      within(resultAlert).getByText(/qrcodedata is required/i)
    ).toBeInTheDocument()
  })

  // -- Scan history -------------------------------------------------------

  it('accumulates scan entries in the history list', async () => {
    mockPost
      .mockResolvedValueOnce(successResponse)
      .mockResolvedValueOnce(alreadyUsedResponse)
      .mockResolvedValueOnce(invalidSignatureResponse)

    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    // First scan — success
    await act(async () => {
      simulateQrScan('qr-success-data')
    })
    await waitFor(() => {
      expect(screen.getByText(/ticket válido/i)).toBeInTheDocument()
    })

    // Click "Escanear Otro" and re-start scanning
    await user.click(screen.getByRole('button', { name: /escanear otro/i }))
    await user.selectOptions(screen.getByLabelText(/^evento$/i), eventId)
    await user.click(screen.getByRole('button', { name: /iniciar escaneo/i }))

    // Second scan — already used
    await act(async () => {
      simulateQrScan('qr-already-used')
    })
    await waitFor(() => {
      expect(screen.getByText(/ticket already used/i)).toBeInTheDocument()
    })

    // Click "Escanear Otro" again
    await user.click(screen.getByRole('button', { name: /escanear otro/i }))
    await user.selectOptions(screen.getByLabelText(/^evento$/i), eventId)
    await user.click(screen.getByRole('button', { name: /iniciar escaneo/i }))

    // Third scan — invalid signature
    await act(async () => {
      simulateQrScan('qr-invalid-sig')
    })
    await waitFor(() => {
      const resultAlert = screen.getByRole('alert')
      expect(within(resultAlert).getByText(/invalid qr code signature/i)).toBeInTheDocument()
    })

    // Verify history
    expect(
      screen.getByRole('heading', { name: /historial de escaneos \(3\)/i })
    ).toBeInTheDocument()

    const historyItems = screen.getAllByRole('listitem')
    expect(historyItems).toHaveLength(3)

    // Most recent is first
    expect(historyItems[0].textContent).toMatch(/invalido/i)
    expect(historyItems[1].textContent).toMatch(/invalido/i)
    expect(historyItems[2].textContent).toMatch(/valido/i)
  })

  // -- Resetting after scan -----------------------------------------------

  it('resets the result display when a different event is selected after a scan', async () => {
    mockPost.mockResolvedValueOnce(successResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    expect(screen.getByText(/ticket válido/i)).toBeInTheDocument()

    // Select a different event — result should clear
    await user.selectOptions(screen.getByLabelText(/^evento$/i), mockEvents[1].id)
    // The overlay exits via AnimatePresence — wait for the exit animation to finish
    await waitFor(() => {
      expect(screen.queryByText(/ticket válido/i)).not.toBeInTheDocument()
    })
    expect(screen.getByRole('button', { name: /iniciar escaneo/i })).toBeInTheDocument()
  })

  // -- Event selector disabled during scanning ----------------------------

  it('disables the event selector while scanning is active', async () => {
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)
    expect(screen.getByLabelText(/^evento$/i)).toBeDisabled()
  })

  // -- sessionStorage scan history ----------------------------------------
  it('persists scan history to sessionStorage', async () => {
    mockPost.mockResolvedValueOnce(successResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    await waitFor(() => {
      expect(screen.getByText(/ticket válido/i)).toBeInTheDocument()
    })

    const stored = sessionStorage.getItem('staff_scan_history')
    expect(stored).toBeTruthy()
    const parsed = JSON.parse(stored)
    expect(Array.isArray(parsed)).toBe(true)
    expect(parsed.length).toBeGreaterThan(0)
    expect(parsed[0].eventId).toBe(eventId)
    expect(parsed[0].isValid).toBe(true)
  })
})

// ── Visual Regression: Glass & Theme ──────────────────────────────────

describe('StaffScan — Visual Regression', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockGet.mockReset()
    capturedSuccessCallback = null
    fakeIsScanning = false
    shouldFailCamera = false
    Object.keys(sessionStore).forEach((k) => delete sessionStore[k])
    mockGet.mockResolvedValue({ data: mockEvents })
  })

  it('renders glass-surface on the controls panel', async () => {
    renderWithQueryClient(<StaffScan />)
    await screen.findByLabelText(/^evento$/i)
    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
  })

  it('renders Badge components in scan history after a scan', async () => {
    mockPost.mockResolvedValueOnce(successResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    await waitFor(() => {
      expect(screen.getByText(/ticket válido/i)).toBeInTheDocument()
    })

    expect(screen.getByText(/historial de escaneos/i)).toBeInTheDocument()
    expect(screen.getByText('Valido')).toBeInTheDocument()
  })

  it('result overlay appears after scan with animated entry', async () => {
    mockPost.mockResolvedValueOnce(successResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    const resultAlert = await screen.findByRole('alert')
    expect(resultAlert).toBeInTheDocument()
    expect(resultAlert.textContent).toMatch(/ticket válido/i)
  })

  it('result overlay clears when rescanning', async () => {
    mockPost.mockResolvedValueOnce(successResponse)
    renderWithQueryClient(<StaffScan />)
    const user = userEvent.setup()
    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    await screen.findByText(/ticket válido/i)

    await user.click(screen.getByRole('button', { name: /escanear otro/i }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /iniciar escaneo/i })).toBeInTheDocument()
    })
  })
})
