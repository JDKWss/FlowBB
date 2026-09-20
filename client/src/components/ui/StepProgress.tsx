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
      <div className="mb-2 flex items-center justify-end text-[0.7rem] font-bold text-ink-600">
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
