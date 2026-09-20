import { ChevronLeft } from 'lucide-react'
import { Button } from './ui/button'

type FlowBackButtonProps = {
  label: string
  onClick: () => void
}

export function FlowBackButton({ label, onClick }: FlowBackButtonProps) {
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      onClick={onClick}
      aria-label={label}
      className="shrink-0 bg-white/[0.04] text-white hover:bg-white/10"
    >
      <ChevronLeft aria-hidden="true" className="size-5" />
    </Button>
  )
}
