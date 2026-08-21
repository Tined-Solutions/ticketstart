export default function Card({
  children,
  header,
  footer,
  glass = false,
  padding = 'md',
  className = '',
  ...rest
}) {
  // Only pass known HTML-safe props to the DOM
  const { style, id, title, lang, dir, hidden } = rest

  const paddingClasses = {
    none: '',
    sm: 'p-4',
    md: 'p-6',
    lg: 'p-8',
  }

  const baseClasses = glass
    ? 'glass-surface'
    : 'border border-border rounded-[var(--radius-card)] bg-surface'

  return (
    <div
      className={`${baseClasses} overflow-hidden ${className}`}
      style={style}
      id={id}
      title={title}
      lang={lang}
      dir={dir}
      hidden={hidden}
    >
      {header && (
        <div className="px-6 py-4 border-b border-border font-semibold text-text-1 font-display">
          {header}
        </div>
      )}

      <div className={paddingClasses[padding] || paddingClasses.md}>
        {children}
      </div>

      {footer && (
        <div className="px-6 py-4 border-t border-border bg-canvas">
          {footer}
        </div>
      )}
    </div>
  )
}
