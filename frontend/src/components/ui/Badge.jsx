const variantClasses = {
  success: 'bg-verde/15 text-verde-dark',
  warning: 'bg-amarillo/15 text-amarillo-dark',
  error: 'bg-rose-100 text-rose-700',
  info: 'bg-cian/15 text-cian-dark',
}

export default function Badge({ children, variant = 'info', className = '', ...rest }) {
  return (
    <span
      className={`inline-flex items-center text-xs font-semibold px-2.5 py-0.5 rounded-full whitespace-nowrap
        ${variantClasses[variant] || variantClasses.info} ${className}`}
      {...rest}
    >
      {children}
    </span>
  )
}
