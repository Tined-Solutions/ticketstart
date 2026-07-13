import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor, act, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import StaffScan from './StaffScan.jsx'

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

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
}))

vi.mock('../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
  },
}))

vi.mock('../context/auth.js', () => ({
  useAuth: () => ({
    user: { id: 'staff-1', email: 'staff@test.com', role: 'Staff' },
    token: 'mock-staff-token',
  }),
}))

vi.mock('html5-qrcode', () => ({
  Html5Qrcode: vi.fn().mockImplementation(function (elementId) {
    this._elementId = elementId
    this.start = vi.fn().mockImplementation(
      async (_cameraConfig, _scanConfig, successCallback, _errorCallback) => {
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
  capturedSuccessCallback = null
  fakeIsScanning = false
  shouldFailCamera = false

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
  await user.clear(screen.getByLabelText(/id del evento/i))
  await user.type(screen.getByLabelText(/id del evento/i), eventId)
  await user.click(screen.getByRole('button', { name: /iniciar escaneo/i }))
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('StaffScan', () => {
  // -- Rendering ----------------------------------------------------------

  it('renders the page with heading and event ID input', () => {
    render(<StaffScan />)

    expect(
      screen.getByRole('heading', { name: /escanear qr/i })
    ).toBeInTheDocument()
    expect(screen.getByLabelText(/id del evento/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /iniciar escaneo/i })
    ).toBeInTheDocument()
  })

  it('does not show history when there are no scans', () => {
    render(<StaffScan />)

    expect(
      screen.queryByRole('heading', { name: /historial/i })
    ).not.toBeInTheDocument()
  })

  // -- Validation ---------------------------------------------------------

  it('shows an error when trying to scan without an event ID', async () => {
    render(<StaffScan />)

    await userEvent.click(
      screen.getByRole('button', { name: /iniciar escaneo/i })
    )

    expect(
      screen.getByText(/debe ingresar el id del evento/i)
    ).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('clears the validation error when the user starts typing in the event ID field', async () => {
    render(<StaffScan />)

    await userEvent.click(
      screen.getByRole('button', { name: /iniciar escaneo/i })
    )
    expect(screen.getByText(/debe ingresar el id del evento/i)).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText(/id del evento/i), 'a')
    expect(
      screen.queryByText(/debe ingresar el id del evento/i)
    ).not.toBeInTheDocument()
  })

  // -- Scanning lifecycle -------------------------------------------------

  it('starts the camera and shows the stop button when event ID is provided', async () => {
    render(<StaffScan />)
    const user = userEvent.setup()

    await startScanning(user)

    expect(
      screen.getByRole('button', { name: /detener escaneo/i })
    ).toBeInTheDocument()
  })

  it('shows a camera error message when camera access is denied', async () => {
    shouldFailCamera = true

    render(<StaffScan />)
    const user = userEvent.setup()

    await startScanning(user)

    await waitFor(() => {
      expect(
        screen.getByText(/no se pudo acceder a la cámara/i)
      ).toBeInTheDocument()
    })
  })

  it('stops the scanner when the stop button is clicked', async () => {
    render(<StaffScan />)
    const user = userEvent.setup()

    await startScanning(user)

    expect(
      screen.getByRole('button', { name: /detener escaneo/i })
    ).toBeInTheDocument()

    await user.click(
      screen.getByRole('button', { name: /detener escaneo/i })
    )

    await waitFor(() => {
      expect(
        screen.queryByRole('button', { name: /detener escaneo/i })
      ).not.toBeInTheDocument()
    })
  })

  // -- Successful scan ----------------------------------------------------

  it('calls the validation API and shows a success result with audio beep', async () => {
    mockPost.mockResolvedValueOnce(successResponse)

    render(<StaffScan />)
    const user = userEvent.setup()

    await startScanning(user)

    // Simulate QR code detection
    await act(async () => {
      simulateQrScan()
    })

    // Scanner should stop after detection
    await waitFor(() => {
      expect(
        screen.queryByRole('button', { name: /detener escaneo/i })
      ).not.toBeInTheDocument()
    })

    expect(mockPost).toHaveBeenCalledWith('/tickets/validate', {
      qrCodeData: 'ticket-001:1750000000:abc123sig',
      eventId,
    })

    // Success result
    const resultAlert = screen.getByRole('alert')
    expect(resultAlert).toBeInTheDocument()
    expect(within(resultAlert).getByText(/ticket válido/i)).toBeInTheDocument()
    expect(within(resultAlert).getByText(/recital de rock nacional/i)).toBeInTheDocument()
    expect(within(resultAlert).getByText(/platea/i)).toBeInTheDocument()
    expect(within(resultAlert).getByText(/comprador@test.com/i)).toBeInTheDocument()

    // Audio feedback
    expect(window.AudioContext).toHaveBeenCalled()
  })

  it('shows the "Escanear Otro" button after a scan result', async () => {
    mockPost.mockResolvedValueOnce(successResponse)

    render(<StaffScan />)
    const user = userEvent.setup()

    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    expect(
      screen.getByRole('button', { name: /escanear otro/i })
    ).toBeInTheDocument()
  })

  // -- Failed scan --------------------------------------------------------

  it('shows an error result when the ticket is already used', async () => {
    mockPost.mockResolvedValueOnce(alreadyUsedResponse)

    render(<StaffScan />)
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
    // Error beep should have been played
    expect(window.AudioContext).toHaveBeenCalled()
  })

  it('shows an error result when the QR signature is invalid', async () => {
    mockPost.mockResolvedValueOnce(invalidSignatureResponse)

    render(<StaffScan />)
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

    render(<StaffScan />)
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

    render(<StaffScan />)
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

    render(<StaffScan />)
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

    render(<StaffScan />)
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
    await user.clear(screen.getByLabelText(/id del evento/i))
    await user.type(screen.getByLabelText(/id del evento/i), eventId)
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
    await user.clear(screen.getByLabelText(/id del evento/i))
    await user.type(screen.getByLabelText(/id del evento/i), eventId)
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
    expect(historyItems[0].textContent).toMatch(/inválido/i)
    expect(historyItems[1].textContent).toMatch(/inválido/i)
    expect(historyItems[2].textContent).toMatch(/válido/i)
  })

  // -- Resetting after scan -----------------------------------------------

  it('resets the result display when the event ID is changed after a scan', async () => {
    mockPost.mockResolvedValueOnce(successResponse)

    render(<StaffScan />)
    const user = userEvent.setup()

    await startScanning(user)

    await act(async () => {
      simulateQrScan()
    })

    expect(screen.getByText(/ticket válido/i)).toBeInTheDocument()

    // Change the event ID — result should clear
    await user.clear(screen.getByLabelText(/id del evento/i))
    await user.type(screen.getByLabelText(/id del evento/i), 'new-event-id')

    expect(screen.queryByText(/ticket válido/i)).not.toBeInTheDocument()
    // Start button should re-appear
    expect(
      screen.getByRole('button', { name: /iniciar escaneo/i })
    ).toBeInTheDocument()
  })

  // -- Event ID disabled during scanning ----------------------------------

  it('disables the event ID input while scanning is active', async () => {
    render(<StaffScan />)
    const user = userEvent.setup()

    await startScanning(user)

    expect(screen.getByLabelText(/id del evento/i)).toBeDisabled()
  })
})
