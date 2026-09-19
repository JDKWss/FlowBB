import type { ReactNode } from 'react'
import { ArrowLeft, Route } from 'lucide-react'
import { StepProgress } from './ui/StepProgress'

export interface AppShellProps {
  children: ReactNode
  bare?: boolean
  title?: string
  subtitle?: string
  onBack?: () => void
  backLabel?: string
  currentStep?: number
  totalSteps?: number
  stepLabel?: string
  headerAction?: ReactNode
  footer?: ReactNode
}

export function AppShell({
  children,
  bare = false,
  title = 'FlowBB',
  subtitle,
  onBack,
  backLabel = 'Wróć',
  currentStep,
  totalSteps = 4,
  stepLabel,
  headerAction,
  footer,
}: AppShellProps) {
  return (
    <div className="min-h-dvh bg-canvas text-ink-950">
      <div className="mx-auto flex min-h-dvh w-full max-w-[480px] flex-col bg-surface shadow-lifted sm:my-6 sm:min-h-[calc(100dvh-3rem)] sm:overflow-hidden sm:rounded-[2rem] sm:border sm:border-line lg:max-w-[520px]">
        {bare && <header className="border-b border-line bg-slate-950 px-5 pb-4 pt-[max(1.25rem,env(safe-area-inset-top))]">
          <div className="mb-5 flex items-center justify-between gap-3">
            <div className="flex items-center gap-2.5"><Route aria-hidden="true" className="size-7 text-cyan-300" /><span className="text-xl font-extrabold tracking-tight">FlowBB<span className="text-cyan-300">.</span></span></div>
            <span className="max-w-32 text-right text-[10px] font-semibold tracking-wide text-amber-200">DEMO DATA / SYMULACJA</span>
          </div>
          {currentStep !== undefined && <StepProgress currentStep={currentStep} totalSteps={totalSteps} label={stepLabel} />}
        </header>}
        {!bare ? <header className="sticky top-0 z-20 border-b border-line/80 bg-surface/95 px-5 pb-4 pt-[max(1rem,env(safe-area-inset-top))] backdrop-blur-xl sm:static sm:pt-5">
          <div className="flex min-h-10 items-center gap-3">
            {onBack ? (
              <button
                type="button"
                onClick={onBack}
                aria-label={backLabel}
                className="-ml-1 grid size-10 shrink-0 cursor-pointer place-items-center rounded-full text-ink-800 transition-colors hover:bg-brand-50 hover:text-brand-700 active:bg-brand-100"
              >
                <ArrowLeft aria-hidden="true" className="size-5" strokeWidth={2.25} />
              </button>
            ) : (
              <div
                className="grid size-10 shrink-0 place-items-center rounded-[0.9rem] bg-brand-600 text-white shadow-soft"
                aria-hidden="true"
              >
                <Route className="size-5" strokeWidth={2.4} />
              </div>
            )}

            <div className="min-w-0 flex-1">
              <h1 className="truncate text-[1.05rem] font-bold tracking-[-0.02em] text-ink-950">
                {title}
              </h1>
              {subtitle ? (
                <p className="mt-0.5 truncate text-xs font-medium text-ink-600">{subtitle}</p>
              ) : null}
            </div>

            {headerAction ? <div className="shrink-0">{headerAction}</div> : null}
          </div>

          {currentStep !== undefined ? (
            <StepProgress
              className="mt-4"
              currentStep={currentStep}
              totalSteps={totalSteps}
              label={stepLabel}
            />
          ) : null}
        </header> : null}

        <main className={bare ? 'flex-1 bg-slate-950' : 'flex-1 px-5 py-6'}>{children}</main>

        {!bare && footer ? (
          <footer className="sticky bottom-0 z-20 border-t border-line/80 bg-surface/95 px-5 pb-[max(1rem,env(safe-area-inset-bottom))] pt-4 backdrop-blur-xl">
            {footer}
          </footer>
        ) : null}
      </div>
    </div>
  )
}
