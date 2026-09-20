import {
  ArrowUpRight,
  CalendarDays,
  MapPin,
  UsersRound,
} from 'lucide-react'
import type { EventSummary } from '../../types/contracts'
import { motion, useReducedMotion } from 'motion/react'
import { Badge, Card, Skeleton, StatePanel } from '../../components/ui'
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
      <Card className="group relative p-0 transition duration-200 hover:-translate-y-0.5 hover:bg-neutral-800">
        <button
        type="button"
        onClick={() => onSelect(event.id)}
        aria-label={`Open event: ${event.name}`}
        className="w-full overflow-hidden rounded-3xl text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-4 focus-visible:ring-offset-background"
      >
        <span className="block p-5">
          <span className="mb-4 flex items-center justify-between gap-3">
            <Badge variant="secondary" className="h-auto bg-white/[0.06] px-3 py-1 text-[0.68rem] font-bold uppercase tracking-[0.18em] text-neutral-300">
              {getCategoryLabel(event.category)}
            </Badge>
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
            <span className="grid size-10 shrink-0 place-items-center rounded-full bg-white text-black transition-transform duration-200 group-hover:scale-105">
              <ArrowUpRight aria-hidden="true" className="size-5" />
            </span>
          </span>

          <span className="mt-5 grid gap-3 pt-3 text-sm text-neutral-300">
            <span className="flex items-center gap-3">
              <CalendarDays aria-hidden="true" className="size-4 shrink-0 text-primary" />
              <span>
                {formatEventDate(event.startAt)}
                <span className="mx-2 text-zinc-600">•</span>
                {formatEventTime(event.startAt)}
              </span>
            </span>
            <span className="flex items-center gap-3">
              <MapPin aria-hidden="true" className="size-4 shrink-0 text-primary" />
              <span className="truncate">{event.venueName}</span>
            </span>
            <span className="flex items-center gap-3">
              <UsersRound aria-hidden="true" className="size-4 shrink-0 text-primary" />
              <span>
                <strong className="font-semibold text-white">{event.participantsCount}</strong>{' '}
                people are going
              </span>
            </span>
          </span>
        </span>
        </button>
      </Card>
    </motion.li>
  )
}

function EventListSkeleton() {
  return (
    <div aria-label="Loading events" aria-live="polite" className="grid gap-4">
      {[0, 1, 2].map((item) => (
        <Skeleton
          key={item}
          className="h-64 rounded-3xl"
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
    <section aria-labelledby="events-title" className="w-full px-5 pb-28 pt-8">
      <header className="mb-7">
        <h1 id="events-title" className="text-4xl font-bold leading-[1.1] tracking-[-0.04em] text-white">
          Your city.<br /><span className="text-neutral-400">Your next plan.</span>
        </h1>
        <p className="mt-3 text-base leading-7 text-zinc-400">
          Choose an event. We&apos;ll help you plan the trip, get home, and find your crew.
        </p>
      </header>

      {isLoading ? (
        <EventListSkeleton />
      ) : error ? (
        <StatePanel kind="error" title="We couldn't load the events" description={error} actionLabel="Try again" onAction={onRetry} />
      ) : events.length === 0 ? (
        <Card className="p-7 text-center">
          <CalendarDays aria-hidden="true" className="mx-auto size-8 text-zinc-500" />
          <p className="mt-4 font-semibold text-white">No upcoming events</p>
          <p className="mt-1 text-sm text-zinc-400">Check back soon—the city never stays quiet for long.</p>
        </Card>
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
