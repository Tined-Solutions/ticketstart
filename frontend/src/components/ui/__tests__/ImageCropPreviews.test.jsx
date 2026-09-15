import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import ImageCropPreviews from '../ImageCropPreviews.jsx'

describe('ImageCropPreviews', () => {
  it('renders nothing when src is not provided', () => {
    const { container } = render(<ImageCropPreviews />)
    expect(container).toBeEmptyDOMElement()
  })

  it('renders the three frames with their labels', () => {
    render(<ImageCropPreviews src="https://example.com/rock.jpg" />)

    expect(screen.getByText('Banner — página del evento')).toBeInTheDocument()
    expect(screen.getByText('Card — listado de eventos')).toBeInTheDocument()
    expect(screen.getByText('Miniatura — resumen de compra')).toBeInTheDocument()
  })

  it('renders the image with the correct src in every frame', () => {
    render(<ImageCropPreviews src="https://example.com/rock.jpg" />)

    const images = screen.getAllByRole('img')
    expect(images).toHaveLength(3)
    for (const image of images) {
      expect(image).toHaveAttribute('src', 'https://example.com/rock.jpg')
    }
  })

  it('gives the frames the expected aspect-ratio classes', () => {
    const { container } = render(<ImageCropPreviews src="https://example.com/rock.jpg" />)

    const frames = [...container.querySelectorAll('li > div')]
    expect(frames).toHaveLength(3)
    // Banner and card both render the standard 16:9 event image; the
    // purchase thumbnail stays square.
    expect(frames[0].className).toContain('aspect-video')
    expect(frames[1].className).toContain('aspect-video')
    expect(frames[2].className).toContain('aspect-square')
  })
})