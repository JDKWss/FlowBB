export interface StepProgressProps {
  currentStep: number
  totalSteps: number
  label?: string
  className?: string
}

export function StepProgress({
  currentStep,
  totalSteps,
  label,
  className = '',
}: StepProgressProps) {
  const safeTotal = Math.max(1, totalSteps)
  const safeCurrent = Math.min(Math.max(1, currentStep), safeTotal)
  const progress = (safeCurrent / safeTotal) * 100
  const accessibilityLabel = label
    ? `${label}. Krok ${safeCurrent} z ${safeTotal}`
    : `Krok ${safeCurrent} z ${safeTotal}`

  return (
    <div className={className}>
      <div className="mb-2 flex items-center justify-between gap-3 text-[0.7rem] font-bold tracking-[0.08em] text-ink-600 uppercase">
        <span className="truncate">{label ?? 'Twój plan'}</span>
        <span aria-hidden="true" className="shrink-0">
          {safeCurrent}/{safeTotal}
        </span>
      </div>
      <Progress
        aria-label={accessibilityLabel}
        value={progress}
      />
    </div>
  )
}
import { Progress } from './progress'
