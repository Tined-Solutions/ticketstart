export default function EmptyState({
  icon,
  title,
  description,
  action,
  className = '',
}) {
  return (
    <div className={`text-center py-12 px-4 ${className}`}>
      {icon && (
        <div className="text-5xl mb-4 text-text-muted" aria-hidden="true">
          {icon}
        </div>
      )}

      {title && (
        <h2 className="text-xl font-heading text-text-1 mb-2">
          {title}
        </h2>
      )}

      {description && (
        <p className="text-text-2 mb-6 max-w-md mx-auto">
          {description}
        </p>
      )}

      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}
