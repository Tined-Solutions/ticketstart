import { describe, it, expect, vi, afterEach } from 'vitest'
import { readImageDimensions } from '../readImageDimensions.js'

// jsdom neither decodes images nor implements createObjectURL, so both are
// stubbed: FakeImage lets the test drive onload/onerror deterministically.
function installFakeImage() {
  const instances = []

  class FakeImage {
    constructor() {
      this.onload = null
      this.onerror = null
      this.naturalWidth = 0
      this.naturalHeight = 0
      instances.push(this)
    }
  }

  vi.stubGlobal('Image', FakeImage)
  return instances
}

function installFakeUrl() {
  class FakeUrl {}
  FakeUrl.createObjectURL = vi.fn(() => 'blob:fake-url')
  FakeUrl.revokeObjectURL = vi.fn()

  vi.stubGlobal('URL', FakeUrl)
  return FakeUrl
}

describe('readImageDimensions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('resolves the natural width and height and revokes the object URL', async () => {
    const images = installFakeImage()
    const FakeUrl = installFakeUrl()

    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    const promise = readImageDimensions(file)

    expect(FakeUrl.createObjectURL).toHaveBeenCalledWith(file)

    images[0].naturalWidth = 1920
    images[0].naturalHeight = 1080
    images[0].onload()

    await expect(promise).resolves.toEqual({ width: 1920, height: 1080 })
    expect(FakeUrl.revokeObjectURL).toHaveBeenCalledWith('blob:fake-url')
  })

  it('rejects with a descriptive error when the image cannot be decoded', async () => {
    const images = installFakeImage()
    installFakeUrl()

    const file = new File(['dummy'], 'broken.jpg', { type: 'image/jpeg' })
    const promise = readImageDimensions(file)

    images[0].onerror()

    await expect(promise).rejects.toThrow('No se pudo leer la imagen.')
  })
})
