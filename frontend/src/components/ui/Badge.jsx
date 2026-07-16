const variantClasses = {
  success: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
  warning: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  error: 'bg-rose-100 text-rose-700 dark:bg-rose-500/20 dark:text-rose-400',
  info: 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-400',
}

export default function Badge({ children, variant = 'info', className = '' }) {
  return (
    <span
      className={`inline-flex items-center text-xs font-semibold px-2.5 py-0.5 rounded-full whitespace-nowrap
        ${variantClasses[variant] || variantClasses.info} ${className}`}
    >
      {children}
    </span>
  )
}
