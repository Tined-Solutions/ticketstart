/**
 * Reusable styled tooltip. Wraps a trigger (e.g. an icon-only button) and shows
 * a label on hover. Extracted/improved from the category-chip tooltip in the
 * home hero — now a self-contained component with a top arrow and reduced-motion
 * support.
 *
 * Accessibility: the visual tooltip is decorative (aria-hidden); the underlying
 * interactive element must carry a proper accessible name (aria-label).
 */
export default function Tooltip({ label, className = '', children }) {
  return (
    <span className={`group relative inline-flex ${className}`}>
      {children}
      <span
        aria-hidden="true"
        className="pointer-events-none absolute -top-9 left-1/2 z-20 -translate-x-1/2 whitespace-nowrap rounded-md bg-gris-oscuro px-2.5 py-1 text-xs font-medium text-white opacity-0 shadow-lg transition-all duration-200 group-hover:-translate-y-0.5 group-hover:opacity-100 motion-reduce:transition-none"
      >
        {label}
        <span
          aria-hidden="true"
          className="absolute left-1/2 top-full -translate-x-1/2 border-4 border-transparent border-t-gris-oscuro"
        />
      </span>
    </span>
  )
}
