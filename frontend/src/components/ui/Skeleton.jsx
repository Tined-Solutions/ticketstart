const variantStyles = {
  text: 'h-4 rounded',
  circular: 'rounded-full',
  rectangular: 'rounded-md',
}

/**
 * Skeleton placeholder with pulse animation.
 * Respects `prefers-reduced-motion` — renders static when the user prefers reduced motion.
 */
export default function Skeleton({
  width = '100%',
  height,
  variant = 'text',
  className = '',
}) {
  const variantClass = variantStyles[variant] || variantStyles.text

  return (
    <div
      role="status"
      aria-label="Loading…"
      className={`bg-neutral-200 dark:bg-neutral-700
        motion-safe:animate-pulse
        ${variantClass} ${className}`}
      style={{ width, height: height || undefined }}
    >
      <span className="sr-only">Loading…</span>
    </div>
  )
}
