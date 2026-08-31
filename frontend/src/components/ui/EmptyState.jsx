export default function EmptyState({
  icon,
  title,
  description,
  action,
  className = '',
}) {
  return (
    <div className={`text-center py-8 px-4 ${className}`}>
      {icon && (
        <div className="text-3xl mb-4 text-text-muted" aria-hidden="true">
          {icon}
        </div>
      )}

      {title && (
        <h2 className="text-base font-display font-bold text-gris-oscuro mb-2">
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
