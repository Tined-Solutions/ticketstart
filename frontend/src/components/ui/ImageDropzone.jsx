import { useCallback } from 'react'
import { useDropzone } from 'react-dropzone'
import { ImagePlus, X } from 'lucide-react'

const ACCEPT = { 'image/jpeg': [], 'image/png': [], 'image/webp': [] }
const MAX_SIZE = 5 * 1024 * 1024

/**
 * Brand-styled image dropzone (drag & drop + click to browse).
 *
 * Headless logic comes from react-dropzone; every visual is a repo token so
 * it matches the TicketStart brand. Validation mirrors the event form rules
 * (JPEG/PNG/WebP, 5 MB max); rejections are reported through `onReject` so
 * the parent form shows them in its own feedback area.
 */
export default function ImageDropzone({
  preview = '',
  disabled = false,
  onSelect,
  onClear,
  onReject,
}) {
  const onDrop = useCallback(
    (accepted, rejections) => {
      if (rejections.length > 0) {
        const code = rejections[0]?.errors?.[0]?.code
        onReject?.(
          code === 'file-too-large'
            ? 'La imagen no debe superar los 5 MB.'
            : 'Formato de imagen no valido. Use JPEG, PNG o WebP.'
        )
        return
      }
      if (accepted.length > 0) {
        onSelect?.(accepted[0])
      }
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
            preview ? 'border-transparent bg-transparent p-0' : 'p-6',
          ].join(' '),
        })}
      >
        <input {...getInputProps({ id: 'eventImage', 'aria-describedby': 'eventImage-hint' })} />
        {preview ? (
          <div className="relative">
            <img
              src={preview}
              alt="Vista previa"
              className="h-56 w-full rounded-xl border border-gris-oscuro/15 object-cover"
            />
            <button
              type="button"
              aria-label="Quitar imagen"
              disabled={disabled}
              onClick={(e) => {
                e.stopPropagation()
                onClear?.()
              }}
              className="absolute right-2 top-2 flex h-9 w-9 items-center justify-center rounded-full bg-gris-oscuro/80 text-white transition-colors hover:bg-gris-oscuro disabled:cursor-not-allowed disabled:opacity-60"
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
              Formatos: JPEG, PNG, WebP. Máximo 5 MB.
            </p>
          </>
        )}
      </div>
    </div>
  )
}
