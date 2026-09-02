import { forwardRef } from 'react'

const variantClasses = {
  primary:
    'bg-primary text-primary-content hover:bg-primary-hover focus-visible:ring-primary',
  accent:
    'bg-accent text-white hover:bg-accent-hover focus-visible:ring-accent',
  secondary:
    'bg-primary/10 text-primary hover:bg-primary/20 focus-visible:ring-primary',
  danger:
    'bg-danger text-white hover:opacity-90 focus-visible:ring-danger',
  ghost:
    'bg-transparent text-neutral-700 hover:bg-neutral-100 focus-visible:ring-primary',
  glass:
    'backdrop-blur-sm bg-white/60 border border-gris-oscuro/15 text-purpura-dark hover:bg-white/80 hover:border-purpura/40 hover:shadow-[0_10px_24px_rgba(74,74,74,0.16)] focus-visible:ring-brand-1',
  gradient:
    'bg-gradient-to-r from-brand-1 to-brand-2 hover:brightness-95 text-white focus-visible:ring-brand-1',
}

const sizeClasses = {
  sm: 'px-4 py-1.5 text-sm rounded-full',
  md: 'px-6 py-2.5 text-sm rounded-full',
  lg: 'px-7 py-3 text-base rounded-full',
}

const Button = forwardRef(function Button(
  {
    children,
    variant = 'primary',
    size = 'md',
    loading = false,
    disabled = false,
    className = '',
    type = 'button',
    ...rest
  },
  ref
) {
  const isDisabled = disabled || loading

  return (
    <button
      ref={ref}
      type={type}
      disabled={isDisabled}
      className={`group inline-flex items-center justify-center gap-2 font-display font-semibold transition-all duration-300
        enabled:hover:-translate-y-0.5 enabled:active:translate-y-0
        focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2
        disabled:opacity-60 disabled:cursor-not-allowed
        ${variantClasses[variant] || variantClasses.primary}
        ${sizeClasses[size] || sizeClasses.md}
        ${className}`}
      {...rest}
    >
      {loading && (
        <svg
          className="animate-spin -ml-1 mr-2 h-4 w-4"
          fill="none"
          viewBox="0 0 24 24"
          aria-hidden="true"
        >
          <circle
            className="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            strokeWidth="4"
          />
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
          />
        </svg>
      )}
      {children}
    </button>
  )
})

export default Button
