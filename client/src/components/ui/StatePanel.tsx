import type { ReactNode } from 'react'
import { CircleAlert, Inbox, LoaderCircle } from 'lucide-react'
import { Button } from './Button'

export type StatePanelKind = 'loading' | 'empty' | 'error'

export interface StatePanelProps {
  kind: StatePanelKind
  title?: string
  description?: string
  actionLabel?: string
  onAction?: () => void
  icon?: ReactNode
  className?: string
}

const defaults: Record<StatePanelKind, { title: string; description: string }> = {
  loading: { title: 'Chwila moment', description: 'Pobieramy najnowsze informacje.' },
  empty: { title: 'Jeszcze tu pusto', description: 'Wróć później lub wybierz inną opcję.' },
  error: { title: 'Coś poszło nie tak', description: 'Spróbuj ponownie za chwilę.' },
}

function DefaultIcon({ kind }: { kind: StatePanelKind }) {
  if (kind === 'loading') {
    return <LoaderCircle aria-hidden="true" className="size-6 animate-spin" />
  }

  if (kind === 'error') {
    return <CircleAlert aria-hidden="true" className="size-6" />
  }

  return <Inbox aria-hidden="true" className="size-6" />
}

export function StatePanel({
  kind,
  title,
  description,
  actionLabel,
  onAction,
  icon,
  className = '',
}: StatePanelProps) {
  const copy = defaults[kind]
  const isError = kind === 'error'

  return (
    <section
      role={isError ? 'alert' : 'status'}
      aria-live={isError ? 'assertive' : 'polite'}
      className={`rounded-[1.5rem] border p-6 text-center ${
        isError ? 'border-rose-400/30 bg-rose-400/10' : 'border-line bg-surface'
      } ${className}`}
    >
      <div
        className={`mx-auto mb-4 grid size-12 place-items-center rounded-2xl ${
          isError ? 'bg-rose-400/10 text-rose-300' : 'bg-brand-100 text-brand-700'
        }`}
      >
        {icon ?? <DefaultIcon kind={kind} />}
      </div>
      <h2 className="text-lg font-extrabold tracking-[-0.02em] text-ink-950">
        {title ?? copy.title}
      </h2>
      <p className="mx-auto mt-2 max-w-64 text-sm leading-6 text-ink-600">
        {description ?? copy.description}
      </p>
      {actionLabel && onAction ? (
        <Button className="mt-5" variant={isError ? 'danger' : 'secondary'} onClick={onAction}>
          {actionLabel}
        </Button>
      ) : null}
    </section>
  )
}
