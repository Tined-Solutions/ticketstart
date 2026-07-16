export default function GlassCard({ children, className = '', as: Component = 'div', ...rest }) {
  return (
    <Component className={`glass-surface ${className}`} {...rest}>
      {children}
    </Component>
  )
}
