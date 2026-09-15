/**
 * Reads the intrinsic pixel dimensions of an image File.
 *
 * Creates an off-DOM Image from a temporary object URL and resolves with the
 * decoded naturalWidth/naturalHeight. The object URL is always revoked once
 * the browser has decoded the file, so no blob stays alive.
 *
 * @param {File} file — the image file to measure
 * @returns {Promise<{ width: number, height: number }>}
 */
export function readImageDimensions(file) {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const image = new Image()

    image.onload = () => {
      URL.revokeObjectURL(url)
      resolve({ width: image.naturalWidth, height: image.naturalHeight })
    }

    image.onerror = () => {
      URL.revokeObjectURL(url)
      reject(new Error('No se pudo leer la imagen.'))
    }

    image.src = url
  })
}
