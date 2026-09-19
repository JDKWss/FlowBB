import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { LoaderCircle } from 'lucide-react'

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger'
export type ButtonSize = 'sm' | 'md' | 'lg'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
  fullWidth?: boolean
  loading?: boolean
  leadingIcon?: ReactNode
  trailingIcon?: ReactNode
}

const variants: Record<ButtonVariant, string> = {
  primary:
    'bg-brand-600 text-slate-950 shadow-soft hover:bg-brand-700 active:bg-brand-700 disabled:bg-brand-600',
  secondary:
    'border border-line bg-surface text-ink-950 shadow-sm hover:border-brand-600 hover:bg-brand-50 active:bg-brand-100',
  ghost: 'bg-transparent text-ink-800 hover:bg-brand-50 hover:text-brand-700 active:bg-brand-100',
  danger: 'bg-rose-300 text-slate-950 shadow-sm hover:brightness-95 active:brightness-90',
}

const sizes: Record<ButtonSize, string> = {
  sm: 'min-h-9 rounded-xl px-3 text-sm',
  md: 'min-h-11 rounded-[0.9rem] px-4 text-sm',
  lg: 'min-h-13 rounded-2xl px-5 text-base',
}

export function Button({
  children,
  className = '',
  variant = 'primary',
  size = 'md',
  fullWidth = false,
  loading = false,
  leadingIcon,
  trailingIcon,
  disabled,
  type = 'button',
  ...props
}: ButtonProps) {
  const isDisabled = disabled || loading

  return (
    <button
      {...props}
      type={type}
      disabled={isDisabled}
      aria-busy={loading || undefined}
      className={`inline-flex cursor-pointer items-center justify-center gap-2 font-bold transition duration-200 disabled:cursor-not-allowed disabled:opacity-55 ${variants[variant]} ${sizes[size]} ${fullWidth ? 'w-full' : ''} ${className}`}
    >
      {loading ? (
        <LoaderCircle aria-hidden="true" className="size-[1.1em] animate-spin" />
      ) : (
        leadingIcon
      )}
      <span>{children}</span>
      {!loading ? trailingIcon : null}
    </button>
  )
}
