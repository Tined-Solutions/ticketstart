const sizeClasses = {
  sm: 'h-4 w-4 border-2',
  md: 'h-8 w-8 border-[3px]',
  lg: 'h-12 w-12 border-4',
}

export default function Spinner({ size = 'md', className = '', label = 'Cargando...' }) {
  return (
    <div
      role="status"
      aria-label={label}
      className={`inline-flex items-center justify-center ${className}`}
    >
      <div
        className={`animate-spin rounded-full border-primary/25 border-t-primary ${sizeClasses[size] || sizeClasses.md}`}
        aria-hidden="true"
      />
      <span className="sr-only">{label}</span>
    </div>
  )
}
