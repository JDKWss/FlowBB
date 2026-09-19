import type { ReactNode } from 'react'
import { ArrowLeft, Route } from 'lucide-react'
import { DemoBadge } from './DemoBadge'
import { Button } from './ui/button'
import { TooltipProvider } from './ui/tooltip'
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
    <TooltipProvider>
    <div className="h-full min-h-0 overflow-hidden bg-canvas text-ink-950">
      <div className="flex h-full min-h-0 w-full flex-col overflow-hidden bg-surface">
        {bare && <header className="shrink-0 bg-background px-5 pb-4 pt-[var(--phone-safe-top,1.25rem)]">
          <div className="mb-5 flex items-center justify-between gap-3">
            <div className="flex items-center gap-2.5"><Route aria-hidden="true" className="size-7 text-primary" /><span className="text-xl font-extrabold tracking-tight">FlowBB<span className="text-primary">.</span></span></div>
            <DemoBadge compact />
          </div>
          {currentStep !== undefined && <StepProgress currentStep={currentStep} totalSteps={totalSteps} label={stepLabel} />}
        </header>}
        {!bare ? <header className="sticky top-0 z-20 shrink-0 border-b border-line/80 bg-surface/95 px-5 pb-4 pt-[var(--phone-safe-top,1rem)] backdrop-blur-xl">
          <div className="flex min-h-10 items-center gap-3">
            {onBack ? (
              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={onBack}
                aria-label={backLabel}
                className="-ml-1 shrink-0 text-ink-800 hover:bg-brand-50 hover:text-brand-700"
              >
                <ArrowLeft aria-hidden="true" className="size-5" strokeWidth={2.25} />
              </Button>
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

        <main className={bare ? 'min-h-0 flex-1 overflow-y-auto bg-slate-950' : 'min-h-0 flex-1 overflow-y-auto px-5 py-6'}>{children}</main>

        {!bare && footer ? (
          <footer className="sticky bottom-0 z-20 shrink-0 border-t border-line/80 bg-surface/95 px-5 pb-[var(--phone-safe-bottom,1rem)] pt-4 backdrop-blur-xl">
            {footer}
          </footer>
        ) : null}
      </div>
    </div>
    </TooltipProvider>
  )
}
