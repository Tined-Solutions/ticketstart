import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, act, waitFor } from '@testing-library/react'
import ImageDropzone from '../ImageDropzone.jsx'

const mockReadImageDimensions = vi.fn()

vi.mock('../../../lib/readImageDimensions.js', () => ({
  readImageDimensions: (...args) => mockReadImageDimensions(...args),
}))

function renderDropzone(overrides = {}) {
  const props = {
    preview: '',
    disabled: false,
    onSelect: vi.fn(),
    onClear: vi.fn(),
    onReject: vi.fn(),
    ...overrides,
  }

  // The real label lives in EventForm; wrap it here so the file input is
  // reachable by its accessible name, matching production usage.
  render(
    <label>
      Imagen del evento
      <ImageDropzone {...props} />
    </label>
  )

  return props
}

function makeFile(name, type) {
  return new File(['dummy'], name, { type })
}

async function selectFile(file) {
  await act(async () => {
    fireEvent.change(screen.getByLabelText(/imagen del evento/i), {
      target: { files: [file] },
    })
  })
}

describe('ImageDropzone — dimension validation (16:9, ≥1920×1080)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockReadImageDimensions.mockReset()
  })

  it('accepts a conforming 16:9 image at or above 1920×1080', async () => {
    mockReadImageDimensions.mockResolvedValue({ width: 1920, height: 1080 })
    const props = renderDropzone()

    await selectFile(makeFile('event.jpg', 'image/jpeg'))

    await waitFor(() => expect(props.onSelect).toHaveBeenCalledTimes(1))
    expect(props.onSelect).toHaveBeenCalledWith(expect.any(File))
    expect(props.onReject).not.toHaveBeenCalled()
  })

  it('rejects an image whose aspect ratio is outside the 16:9 tolerance', async () => {
    // 4:3 — valid size, wrong ratio.
    mockReadImageDimensions.mockResolvedValue({ width: 1600, height: 1200 })
    const props = renderDropzone()

    await selectFile(makeFile('event.jpg', 'image/jpeg'))

    await waitFor(() =>
      expect(props.onReject).toHaveBeenCalledWith(
        'La imagen debe tener proporción 16:9 (por ej. 1920×1080 o mayor).'
      )
    )
    expect(props.onSelect).not.toHaveBeenCalled()
  })

  it('rejects a 16:9 image below the minimum resolution', async () => {
    // 1280×720 is exactly 16:9 but too small.
    mockReadImageDimensions.mockResolvedValue({ width: 1280, height: 720 })
    const props = renderDropzone()

    await selectFile(makeFile('event.jpg', 'image/jpeg'))

    await waitFor(() =>
      expect(props.onReject).toHaveBeenCalledWith(
        'La imagen debe ser de al menos 1920×1080 px.'
      )
    )
    expect(props.onSelect).not.toHaveBeenCalled()
  })

  it('rejects an unreadable image with a dedicated message', async () => {
    mockReadImageDimensions.mockRejectedValue(new Error('No se pudo leer la imagen.'))
    const props = renderDropzone()

    await selectFile(makeFile('broken.jpg', 'image/jpeg'))

    await waitFor(() =>
      expect(props.onReject).toHaveBeenCalledWith(
        'No se pudo leer la imagen. Probá con otro archivo.'
      )
    )
    expect(props.onSelect).not.toHaveBeenCalled()
  })

  it('does not read dimensions for a file rejected by type', async () => {
    const props = renderDropzone()

    await selectFile(makeFile('event.pdf', 'application/pdf'))

    await waitFor(() =>
      expect(props.onReject).toHaveBeenCalledWith(
        'Formato de imagen no valido. Use JPEG, PNG o WebP.'
      )
    )
    expect(mockReadImageDimensions).not.toHaveBeenCalled()
    expect(props.onSelect).not.toHaveBeenCalled()
  })

  it('does not read dimensions for a file rejected by size', async () => {
    const props = renderDropzone()
    const largeFile = new File(['x'.repeat(6 * 1024 * 1024)], 'large.jpg', {
      type: 'image/jpeg',
    })

    await selectFile(largeFile)

    await waitFor(() =>
      expect(props.onReject).toHaveBeenCalledWith(
        'La imagen no debe superar los 5 MB.'
      )
    )
    expect(mockReadImageDimensions).not.toHaveBeenCalled()
    expect(props.onSelect).not.toHaveBeenCalled()
  })
})

describe('ImageDropzone — inline error prop', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockReadImageDimensions.mockReset()
  })

  it('renders the error message with role="alert" when error is set', () => {
    renderDropzone({ error: 'La imagen debe tener proporción 16:9.' })

    expect(screen.getByRole('alert')).toHaveTextContent(
      'La imagen debe tener proporción 16:9.'
    )
  })

  it('renders no error element when error is empty', () => {
    renderDropzone()

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('applies the red error signal to the dropzone root when error is set', () => {
    renderDropzone({ error: 'La imagen debe ser de al menos 1920×1080 px.' })

    // The file input lives directly inside the dropzone root.
    const root = screen.getByLabelText(/imagen del evento/i).parentElement
    expect(root.className).toContain('border-danger')
    expect(root.className).toContain('bg-danger/5')
  })

  it('keeps the preview layout while the red error signal wins over it', () => {
    renderDropzone({
      error: 'La imagen debe tener proporción 16:9.',
      preview: 'https://example.com/img.jpg',
    })

    const root = screen.getByLabelText(/imagen del evento/i).parentElement
    expect(root.className).toContain('border-danger')
    expect(root.className).toContain('bg-danger/5')
    // Preview keeps its zero padding; only the transparent border/bg is
    // swapped for the error tint.
    expect(root.className).toContain('p-0')
    expect(root.className).not.toContain('border-transparent')
    expect(root.className).not.toContain('bg-transparent')
  })
})
