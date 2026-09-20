import {
  Check,
  LoaderCircle,
  MapPin,
  ShieldCheck,
  Users,
} from 'lucide-react'
import type { EventDetails, GroupSummary } from '../../types/contracts'
import { motion, useReducedMotion } from 'motion/react'
import { FlowBackButton } from '../../components/FlowBackButton'
import { Badge, Button, Card, Progress, Separator, Tooltip, TooltipContent, TooltipTrigger } from '../../components/ui'

export interface CrewViewProps {
  event: EventDetails
  groups: GroupSummary[]
  onToggleGroup: (group: GroupSummary) => void
  onBack: () => void
  isUpdating?: boolean
}

export function CrewView({
  event,
  groups,
  onToggleGroup,
  onBack,
  isUpdating = false,
}: CrewViewProps) {
  const reducedMotion = useReducedMotion()
  return (
    <section className="min-h-full w-full bg-background px-5 pb-8 pt-4 text-white">
      <header className="mb-7 flex items-center gap-3">
        <FlowBackButton label="Back to your route" onClick={onBack} />
        <div className="min-w-0">
          <p className="truncate text-sm text-slate-400">{event.name}</p>
        </div>
      </header>

      <div className="mb-7">
        <span className="mb-4 grid size-12 place-items-center rounded-2xl bg-white/[0.06] text-primary">
          <Users aria-hidden="true" size={24} />
        </span>
        <h1 className="text-3xl font-bold tracking-tight">Go together</h1>
        <p className="mt-2 text-sm leading-6 text-slate-400">
          Meet a small group before the event and make the journey together.
        </p>
      </div>

      {groups.length > 0 ? (
        <div className="space-y-4">
          {groups.map((group) => {
            const isFull = group.currentMembers >= group.maxMembers
            const cannotJoin = isFull && !group.joinedByCurrentUser

            return (
              <Card
                key={group.id}
                className={`p-5 transition ${
                  group.joinedByCurrentUser
                    ? 'bg-primary/[0.07] ring-1 ring-primary/25'
                    : 'bg-card'
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h2 className="text-lg font-bold text-white">{group.name}</h2>
                      {group.joinedByCurrentUser && (
                        <Badge className="h-auto gap-1 px-2 py-0.5 text-[10px] font-extrabold uppercase tracking-wide">
                          <Check aria-hidden="true" size={11} strokeWidth={3} />
                          Joined
                        </Badge>
                      )}
                    </div>
                    <p className="mt-2 text-sm leading-5 text-slate-400">
                      {group.description}
                    </p>
                  </div>
                  <Badge
                    variant={isFull ? 'destructive' : 'secondary'}
                    className="shrink-0"
                    aria-label={`${group.currentMembers} of ${group.maxMembers} members`}
                  >
                    {group.currentMembers}/{group.maxMembers}
                  </Badge>
                </div>

                <div className="mt-4" role="status" aria-label={`${group.currentMembers} of ${group.maxMembers} places filled`}>
                  <motion.div
                    initial={reducedMotion ? false : { opacity: 0 }}
                    animate={{ opacity: 1 }}
                    transition={{ duration: reducedMotion ? 0 : .2 }}
                  >
                    <Progress value={(group.currentMembers / group.maxMembers) * 100} />
                  </motion.div>
                  <p className="mt-2 text-xs text-slate-400">{group.joinedByCurrentUser ? 'Your place is confirmed. Meet your crew here.' : isFull ? 'All places are taken.' : `${group.maxMembers - group.currentMembers} places available`}</p>
                </div>

                {group.tags.length > 0 && (
                  <ul className="mt-4 flex flex-wrap gap-2" aria-label="Group tags">
                    {group.tags.map((tag) => (
                      <li key={tag}><Badge variant="secondary" className="text-[11px] font-medium text-slate-300">#{tag}</Badge></li>
                    ))}
                  </ul>
                )}

                <Separator className="my-4" />

                <div className="mb-4 flex items-start gap-2.5">
                  <Tooltip>
                    <TooltipTrigger asChild>
                    <span className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-xl bg-white/[0.06] text-primary">
                    <MapPin aria-hidden="true" size={16} />
                    </span>
                    </TooltipTrigger>
                    <TooltipContent>Meeting point</TooltipContent>
                  </Tooltip>
                  <div>
                    <p className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
                      Meeting point
                    </p>
                    <p className="mt-0.5 text-sm font-semibold text-slate-200">
                      {group.meetingPoint.name}
                    </p>
                  </div>
                </div>

                <Button
                  type="button"
                  variant={group.joinedByCurrentUser ? 'outline' : 'default'}
                  disabled={cannotJoin || isUpdating}
                  onClick={() => onToggleGroup(group)}
                  aria-label={`${group.joinedByCurrentUser ? 'Leave' : 'Join'} ${group.name}`}
                  className="w-full"
                >
                  {isUpdating ? (
                    <LoaderCircle className="animate-spin" aria-hidden="true" size={17} />
                  ) : group.joinedByCurrentUser ? (
                    'Leave crew'
                  ) : isFull ? (
                    'Group full'
                  ) : (
                    'Join crew'
                  )}
                </Button>
              </Card>
            )
          })}
        </div>
      ) : (
        <div className="rounded-3xl border border-dashed border-white/15 px-6 py-10 text-center">
          <span className="mx-auto grid size-12 place-items-center rounded-full bg-white/5 text-slate-400">
            <Users aria-hidden="true" size={22} />
          </span>
          <h2 className="mt-4 font-bold">No crews yet</h2>
          <p className="mt-2 text-sm leading-5 text-slate-500">
            Check back soon—new groups can appear before the event.
          </p>
        </div>
      )}

      <div className="mt-6 flex items-center justify-center gap-2 text-xs text-slate-500">
        <ShieldCheck aria-hidden="true" size={15} />
        Small groups for a safer shared journey
      </div>
    </section>
  )
}
