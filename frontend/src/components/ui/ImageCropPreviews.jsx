/**
 * ImageCropPreviews — live crop preview for the event image.
 *
 * The organizer uploads ONE image that production renders in three contexts
 * (banner, listing card, purchase thumbnail). Banner and card share the
 * standard 16:9 frame; the thumbnail is square. Each frame here replicates
 * the exact container classes used in production AND the real rendered width
 * (banner = EventDetail max-w-5xl content, card = catalog 3-column grid width,
 * thumbnail = 80px), so the organizer sees the real crop and scale BEFORE
 * saving.
 *
 * Renders nothing when `src` is empty.
 */
const FRAMES = [
  {
    id: 'banner',
    label: 'Banner — página del evento',
    // Real width: EventDetail renders inside max-w-5xl (1024px) with px-6 →
    // 976px usable. The create form (max-w-3xl) is narrower, so the frame
    // caps at the production width and scales down when space is limited.
    frameClassName: 'aspect-video w-full max-w-[976px] overflow-hidden rounded-xl',
    altSuffix: 'como banner en la página del evento',
  },
  {
    id: 'card',
    label: 'Card — listado de eventos',
    // Real width: catalog grid max-w-7xl (1280px), 3 columns, gap-6 →
    // (1216 - 48) / 3 = 389px per card at desktop. Same 16:9 as the banner,
    // matching the standard event image.
    frameClassName: 'aspect-video w-full max-w-[389px] overflow-hidden rounded-xl',
    altSuffix: 'como tarjeta en el listado de eventos',
  },
  {
    id: 'thumbnail',
    label: 'Miniatura — resumen de compra',
    // Exact production size: EventSummaryTicket uses w-20 h-20 (80px).
    frameClassName: 'aspect-square w-20 overflow-hidden rounded-lg',
    altSuffix: 'como miniatura en el resumen de compra',
  },
]

export default function ImageCropPreviews({ src, alt = 'Imagen del evento' }) {
  if (!src) return null

  return (
    <div className="mt-4">
      <p className="mb-2 font-display text-sm font-semibold text-text-1">
        Así se va a ver la imagen en cada lugar:
      </p>
      <ul className="flex flex-wrap items-start gap-5">
        {FRAMES.map((frame) => (
          <li key={frame.id}>
            <div
              className={`${frame.frameClassName} border border-gris-oscuro/15 bg-canvas`}
              aria-labelledby={`image-crop-preview-${frame.id}`}
            >
              <img
                src={src}
                alt={`${alt} ${frame.altSuffix}`}
                className="h-full w-full object-cover"
              />
            </div>
            <p
              id={`image-crop-preview-${frame.id}`}
              className="mt-1.5 text-sm text-text-2"
            >
              {frame.label}
            </p>
          </li>
        ))}
      </ul>
    </div>
  )
}