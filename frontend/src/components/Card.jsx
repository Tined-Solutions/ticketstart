export default function Card({
  children,
  header,
  footer,
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

  return (
    <div
      className={`border border-border rounded-xl bg-white overflow-hidden ${className}`}
      style={style}
      id={id}
      title={title}
      lang={lang}
      dir={dir}
      hidden={hidden}
    >
      {header && (
        <div className="px-6 py-4 border-b border-border font-semibold text-gray-900">
          {header}
        </div>
      )}

      <div className={paddingClasses[padding] || paddingClasses.md}>
        {children}
      </div>

      {footer && (
        <div className="px-6 py-4 border-t border-border bg-neutral-50">
          {footer}
        </div>
      )}
    </div>
  )
}
