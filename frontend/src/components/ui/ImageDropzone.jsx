import { useCallback } from 'react'
import { useDropzone } from 'react-dropzone'
import { Image as ImageIcon, ImagePlus, X } from 'lucide-react'
import { readImageDimensions } from '../../lib/readImageDimensions.js'

const ACCEPT = { 'image/jpeg': [], 'image/png': [], 'image/webp': [] }
const MAX_SIZE = 5 * 1024 * 1024

// Standard event image: 16:9, 1920×1080 or larger. The ratio tolerance
// absorbs rounding in real photos (accepted range ≈ 1.68–1.88).
const RATIO_TARGET = 16 / 9
const RATIO_TOLERANCE = 0.1
const MIN_WIDTH = 1920
const MIN_HEIGHT = 1080

/**
 * Brand-styled image dropzone (drag & drop + click to browse).
 *
 * Headless logic comes from react-dropzone; every visual is a repo token so
 * it matches the TicketStart brand. Validation mirrors the event form rules
 * (JPEG/PNG/WebP, 5 MB max) and additionally enforces the standard event
 * image format (16:9, ≥1920×1080). Rejections are reported through `onReject`
 * and the parent surfaces them inline here via the `error` prop.
 */
export default function ImageDropzone({
  preview = '',
  disabled = false,
  error = '',
  onSelect,
  onClear,
  onReject,
}) {
  const onDrop = useCallback(
    async (accepted, rejections) => {
      if (rejections.length > 0) {
        const code = rejections[0]?.errors?.[0]?.code
        onReject?.(
          code === 'file-too-large'
            ? 'La imagen no debe superar los 5 MB.'
            : 'Formato de imagen no valido. Use JPEG, PNG o WebP.'
        )
        return
      }
      if (accepted.length === 0) return

      const file = accepted[0]

      let dimensions
      try {
        dimensions = await readImageDimensions(file)
      } catch {
        onReject?.('No se pudo leer la imagen. Probá con otro archivo.')
        return
      }

      const ratio = dimensions.width / dimensions.height
      if (Math.abs(ratio - RATIO_TARGET) > RATIO_TOLERANCE) {
        onReject?.(
          'La imagen debe tener proporción 16:9 (por ej. 1920×1080 o mayor).'
        )
        return
      }

      if (dimensions.width < MIN_WIDTH || dimensions.height < MIN_HEIGHT) {
        onReject?.('La imagen debe ser de al menos 1920×1080 px.')
        return
      }

      onSelect?.(file)
    },
    [onSelect, onReject]
  )

  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    onDrop,
    accept: ACCEPT,
    maxSize: MAX_SIZE,
    multiple: false,
    disabled,
  })

  return (
    <div>
      <div
        {...getRootProps({
          className: [
            'rounded-xl border-2 border-dashed bg-canvas text-center transition-colors duration-200',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2',
            disabled
              ? 'cursor-not-allowed opacity-60'
              : 'cursor-pointer hover:border-purpura-dark/50 hover:bg-purpura-dark/5',
            !preview && !isDragActive && !isDragReject ? 'border-gris-oscuro/25' : '',
            isDragActive && !isDragReject ? 'border-purpura-dark bg-purpura-dark/10' : '',
            isDragReject ? 'border-danger bg-danger/5' : '',
            // Loaded state is a compact bar (the crop previews below show the
            // image). The red error signal must still win over it.
            preview
              ? error
                ? 'px-4 py-3'
                : 'border-gris-oscuro/25 px-4 py-3'
              : 'p-6',
            error ? 'border-danger bg-danger/5' : '',
          ].join(' '),
        })}
      >
        <input {...getInputProps({ id: 'eventImage', 'aria-describedby': 'eventImage-hint' })} />
        {preview ? (
          // Compact loaded state: the image itself is shown only once, in the
          // crop previews rendered by the parent below the dropzone.
          <div className="flex items-center gap-3 text-left">
            <ImageIcon className="h-6 w-6 shrink-0 text-purpura-dark" aria-hidden="true" />
            <div className="min-w-0 flex-1">
              <p className="font-medium text-gris-oscuro">Imagen cargada</p>
              <p id="eventImage-hint" className="text-[13px] text-text-2">
                Tocá o arrastrá otra para cambiarla.
              </p>
            </div>
            <button
              type="button"
              aria-label="Quitar imagen"
              disabled={disabled}
              onClick={(e) => {
                e.stopPropagation()
                onClear?.()
              }}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-gris-oscuro/80 text-white transition-colors hover:bg-gris-oscuro disabled:cursor-not-allowed disabled:opacity-60"
            >
              <X className="h-4 w-4" aria-hidden="true" />
            </button>
          </div>
        ) : (
          <>
            <ImagePlus
              className={`mx-auto mb-3 h-10 w-10 ${isDragReject ? 'text-danger' : isDragActive ? 'text-purpura-dark' : 'text-text-muted'}`}
              aria-hidden="true"
            />
            <p className="font-medium text-gris-oscuro">
              {isDragReject
                ? 'Ese archivo no sirve — probá con otro'
                : isDragActive
                  ? 'Soltá la imagen acá'
                  : 'Arrastrá la imagen acá o tocá para elegir'}
            </p>
            <p id="eventImage-hint" className="mt-1 text-[13px] text-text-2">
              Formatos: JPEG, PNG o WebP. Máximo 5 MB.
              <br />
              Proporción: 16:9 (1920×1080 o mayor).
            </p>
          </>
        )}
      </div>
      {error && (
        <p role="alert" className="mt-2 text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </div>
  )
}
