import {
  ArrowUpRight,
  CalendarDays,
  MapPin,
  Sparkles,
  UsersRound,
} from 'lucide-react'
import type { EventSummary } from '../../types/contracts'
import { motion, useReducedMotion } from 'motion/react'
import { StatePanel } from '../../components/ui'
import {
  formatEventDate,
  formatEventTime,
  getCategoryLabel,
} from './eventViewUtils'

type EventListViewProps = {
  events: EventSummary[]
  onSelectEvent: (id: string) => void
  isLoading?: boolean
  error?: string | null
  onRetry?: () => void
}

function EventCard({
  event,
  onSelect,
}: {
  event: EventSummary
  onSelect: (id: string) => void
}) {
  const reducedMotion = useReducedMotion()
  return (
    <motion.li initial={reducedMotion ? false : { opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: .24 }}>
      <button
        type="button"
        onClick={() => onSelect(event.id)}
        aria-label={`Open event: ${event.name}`}
        className="group w-full overflow-hidden rounded-[1.75rem] border border-white/10 bg-zinc-900/80 text-left shadow-[0_18px_50px_rgba(0,0,0,0.24)] transition duration-200 hover:-translate-y-0.5 hover:border-emerald-300/40 hover:bg-zinc-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-300 focus-visible:ring-offset-4 focus-visible:ring-offset-black active:translate-y-0"
      >
        <span className="block h-1.5 bg-gradient-to-r from-emerald-300 via-cyan-300 to-blue-400" />

        <span className="block p-5">
          <span className="mb-4 flex items-center justify-between gap-3">
            <span className="rounded-full border border-emerald-300/20 bg-emerald-300/10 px-3 py-1 text-[0.68rem] font-bold uppercase tracking-[0.18em] text-emerald-200">
              {getCategoryLabel(event.category)}
            </span>
            {event.source === 'Demo' && (
              <span className="inline-flex items-center gap-1.5 text-[0.62rem] font-bold uppercase tracking-[0.14em] text-amber-200">
                <Sparkles aria-hidden="true" className="size-3.5" />
                Demo data
              </span>
            )}
          </span>

          <span className="flex items-start justify-between gap-4">
            <span className="min-w-0">
              <span className="block text-xl font-bold leading-tight tracking-tight text-white">
                {event.name}
              </span>
              <span className="mt-2 line-clamp-2 block text-sm leading-6 text-zinc-400">
                {event.description}
              </span>
            </span>
            <span className="grid size-10 shrink-0 place-items-center rounded-full bg-white text-black transition-transform duration-200 group-hover:rotate-6 group-hover:scale-105">
              <ArrowUpRight aria-hidden="true" className="size-5" />
            </span>
          </span>

          <span className="mt-5 grid gap-3 border-t border-white/10 pt-4 text-sm text-zinc-300">
            <span className="flex items-center gap-3">
              <CalendarDays aria-hidden="true" className="size-4 shrink-0 text-emerald-300" />
              <span>
                {formatEventDate(event.startAt)}
                <span className="mx-2 text-zinc-600">•</span>
                {formatEventTime(event.startAt)}
              </span>
            </span>
            <span className="flex items-center gap-3">
              <MapPin aria-hidden="true" className="size-4 shrink-0 text-emerald-300" />
              <span className="truncate">{event.venueName}</span>
            </span>
            <span className="flex items-center gap-3">
              <UsersRound aria-hidden="true" className="size-4 shrink-0 text-emerald-300" />
              <span>
                <strong className="font-semibold text-white">{event.participantsCount}</strong>{' '}
                people are going
              </span>
            </span>
          </span>
        </span>
      </button>
    </motion.li>
  )
}

function EventListSkeleton() {
  return (
    <div aria-label="Loading events" aria-live="polite" className="grid gap-4">
      {[0, 1, 2].map((item) => (
        <div
          key={item}
          className="h-64 animate-pulse rounded-[1.75rem] border border-white/10 bg-zinc-900/70"
        />
      ))}
    </div>
  )
}

export function EventListView({
  events,
  onSelectEvent,
  isLoading = false,
  error = null,
  onRetry,
}: EventListViewProps) {
  return (
    <section aria-labelledby="events-title" className="mx-auto w-full max-w-lg px-4 pb-28 pt-8 sm:px-6">
      <header className="mb-7">
        <div className="mb-4 flex items-center justify-between gap-4">
          <span className="text-xs font-bold uppercase tracking-[0.24em] text-emerald-300">
            Bielsko-Biała · 19–21 September
          </span>
        </div>
        <h1 id="events-title" className="text-4xl font-bold leading-[1.1] tracking-[-0.04em] text-white">
          Your city.<br /><span className="text-cyan-300">Your next plan.</span>
        </h1>
        <p className="mt-3 max-w-md text-base leading-7 text-zinc-400">
          Choose an event. We&apos;ll help you plan the trip, get home, and find your crew.
        </p>
      </header>

      {isLoading ? (
        <EventListSkeleton />
      ) : error ? (
        <StatePanel kind="error" title="We couldn't load the events" description={error} actionLabel="Try again" onAction={onRetry} />
      ) : events.length === 0 ? (
        <div className="rounded-3xl border border-dashed border-white/15 bg-white/[0.03] p-7 text-center">
          <CalendarDays aria-hidden="true" className="mx-auto size-8 text-zinc-500" />
          <p className="mt-4 font-semibold text-white">No upcoming events</p>
          <p className="mt-1 text-sm text-zinc-400">Check back soon—the city never stays quiet for long.</p>
        </div>
      ) : (
        <ul className="grid gap-4">
          {events.map((event) => (
            <EventCard key={event.id} event={event} onSelect={onSelectEvent} />
          ))}
        </ul>
      )}
    </section>
  )
}
