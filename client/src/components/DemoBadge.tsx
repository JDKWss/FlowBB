import { Sparkles } from 'lucide-react'
import { Badge } from './ui/badge'

export function DemoBadge({ compact = false }: { compact?: boolean }) {
  return (
    <Badge
      variant="secondary"
      className="h-auto gap-1.5 bg-white/[0.06] px-2.5 py-1 text-[0.62rem] font-bold uppercase tracking-[0.12em] text-neutral-300"
    >
      <Sparkles aria-hidden="true" className="size-3" />
      {compact ? 'Demo' : 'Demo data / Symulacja'}
    </Badge>
  )
}
