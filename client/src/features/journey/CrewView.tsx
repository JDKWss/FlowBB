import {
  Check,
  ChevronLeft,
  LoaderCircle,
  MapPin,
  ShieldCheck,
  Users,
} from 'lucide-react'
import type { EventDetails, GroupSummary } from '../../types/contracts'
import { motion, useReducedMotion } from 'motion/react'

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
    <section className="mx-auto min-h-dvh w-full max-w-md bg-slate-950 px-5 pb-8 pt-4 text-white">
      <header className="mb-7 flex items-center gap-3">
        <button
          type="button"
          onClick={onBack}
          className="grid size-11 shrink-0 place-items-center rounded-full border border-white/10 bg-white/5 text-slate-200 transition hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-300"
          aria-label="Back to your route"
        >
          <ChevronLeft aria-hidden="true" size={22} />
        </button>
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-cyan-300">
            Crew
          </p>
          <p className="truncate text-sm text-slate-400">{event.name}</p>
        </div>
      </header>

      <div className="mb-7">
        <span className="mb-4 grid size-12 place-items-center rounded-2xl bg-violet-400/15 text-violet-300">
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
              <article
                key={group.id}
                className={`rounded-3xl border p-5 transition ${
                  group.joinedByCurrentUser
                    ? 'border-cyan-300/40 bg-cyan-300/[0.07]'
                    : 'border-white/10 bg-slate-900'
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h2 className="text-lg font-bold text-white">{group.name}</h2>
                      {group.joinedByCurrentUser && (
                        <span className="inline-flex items-center gap-1 rounded-full bg-cyan-300 px-2 py-0.5 text-[10px] font-extrabold uppercase tracking-wide text-slate-950">
                          <Check aria-hidden="true" size={11} strokeWidth={3} />
                          Joined
                        </span>
                      )}
                    </div>
                    <p className="mt-2 text-sm leading-5 text-slate-400">
                      {group.description}
                    </p>
                  </div>
                  <span
                    className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-bold ${
                      isFull
                        ? 'bg-amber-300/10 text-amber-200'
                        : 'bg-white/5 text-slate-300'
                    }`}
                    aria-label={`${group.currentMembers} of ${group.maxMembers} members`}
                  >
                    {group.currentMembers}/{group.maxMembers}
                  </span>
                </div>

                <div className="mt-4" role="status" aria-label={`${group.currentMembers} of ${group.maxMembers} places filled`}>
                  <div className="flex gap-1.5" aria-hidden="true">
                    {Array.from({ length: group.maxMembers }, (_, index) => <motion.span key={index}
                      animate={{ opacity: index < group.currentMembers ? 1 : .2 }}
                      transition={{ duration: reducedMotion ? 0 : .2 }}
                      className="h-1.5 flex-1 rounded-full bg-cyan-300" />)}
                  </div>
                  <p className="mt-2 text-xs text-slate-400">{group.joinedByCurrentUser ? 'Your place is confirmed. Meet your crew here.' : isFull ? 'All places are taken.' : `${group.maxMembers - group.currentMembers} places available`}</p>
                </div>

                {group.tags.length > 0 && (
                  <ul className="mt-4 flex flex-wrap gap-2" aria-label="Group tags">
                    {group.tags.map((tag) => (
                      <li
                        key={tag}
                        className="rounded-full border border-white/10 bg-white/[0.04] px-2.5 py-1 text-[11px] font-medium text-slate-300"
                      >
                        #{tag}
                      </li>
                    ))}
                  </ul>
                )}

                <div className="my-4 h-px bg-white/10" />

                <div className="mb-4 flex items-start gap-2.5">
                  <span className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-xl bg-violet-400/10 text-violet-300">
                    <MapPin aria-hidden="true" size={16} />
                  </span>
                  <div>
                    <p className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
                      Meeting point
                    </p>
                    <p className="mt-0.5 text-sm font-semibold text-slate-200">
                      {group.meetingPoint.name}
                    </p>
                  </div>
                </div>

                <button
                  type="button"
                  disabled={cannotJoin || isUpdating}
                  onClick={() => onToggleGroup(group)}
                  aria-label={`${group.joinedByCurrentUser ? 'Leave' : 'Join'} ${group.name}`}
                  className={`flex min-h-11 w-full items-center justify-center gap-2 rounded-xl px-4 text-sm font-extrabold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-300 disabled:cursor-not-allowed disabled:opacity-55 ${
                    group.joinedByCurrentUser
                      ? 'border border-white/15 bg-transparent text-slate-200 hover:bg-white/5'
                      : 'bg-white text-slate-950 hover:bg-cyan-200'
                  }`}
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
                </button>
              </article>
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
