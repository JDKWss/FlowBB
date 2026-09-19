import {
  ArrowLeft,
  ArrowRight,
  Bike,
  BusFront,
  CalendarDays,
  CarFront,
  CircleHelp,
  CircleCheck,
  CircleMinus,
  Clock3,
  Footprints,
  MapPin,
  Sparkles,
  UsersRound,
} from 'lucide-react'
import type { EventDetails } from '../../types/contracts'
import {
  formatEventDateLong,
  formatEventTimeRange,
  getCategoryLabel,
  transportLabels,
} from './eventViewUtils'

type EventDetailsViewProps = {
  event: EventDetails
  onBack: () => void
  onContinue: () => void
}

type TransportMode = EventDetails['availableTransportModes'][number]

function TransportIcon({ mode }: { mode: TransportMode }) {
  const iconClass = 'size-5'

  switch (mode) {
    case 'Walking':
      return <Footprints aria-hidden="true" className={iconClass} />
    case 'PublicTransport':
      return <BusFront aria-hidden="true" className={iconClass} />
    case 'Bike':
      return <Bike aria-hidden="true" className={iconClass} />
    case 'Car':
      return <CarFront aria-hidden="true" className={iconClass} />
    default:
      return <CircleHelp aria-hidden="true" className={iconClass} />
  }
}

export function EventDetailsView({ event, onBack, onContinue }: EventDetailsViewProps) {
  return (
    <section aria-labelledby="event-title" className="mx-auto w-full max-w-lg pb-36">
      <div className="relative overflow-hidden border-b border-white/10 bg-zinc-950 px-4 pb-8 pt-5 sm:px-6">
        <div aria-hidden="true" className="absolute -right-20 -top-28 size-64 rounded-full bg-emerald-400/15 blur-3xl" />
        <div aria-hidden="true" className="absolute -left-24 top-24 size-52 rounded-full bg-blue-500/10 blur-3xl" />

        <button
          type="button"
          onClick={onBack}
          aria-label="Back to events"
          className="relative grid size-11 place-items-center rounded-full border border-white/10 bg-black/40 text-white backdrop-blur transition hover:border-white/25 hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-300"
        >
          <ArrowLeft aria-hidden="true" className="size-5" />
        </button>

        <div className="relative mt-10">
          <div className="flex flex-wrap items-center gap-2">
            <span className="rounded-full border border-emerald-300/20 bg-emerald-300/10 px-3 py-1 text-[0.68rem] font-bold uppercase tracking-[0.18em] text-emerald-200">
              {getCategoryLabel(event.category)}
            </span>
            {event.source === 'Demo' && (
              <span className="inline-flex items-center gap-1.5 rounded-full border border-amber-300/20 bg-amber-300/10 px-3 py-1 text-[0.62rem] font-bold uppercase tracking-[0.14em] text-amber-200">
                <Sparkles aria-hidden="true" className="size-3.5" />
                DEMO DATA / SYMULACJA
              </span>
            )}
          </div>

          <h1 id="event-title" className="mt-5 text-4xl font-bold leading-[1.05] tracking-[-0.045em] text-white sm:text-5xl">
            {event.name}
          </h1>
          <p className="mt-4 text-base leading-7 text-zinc-300">{event.description}</p>
        </div>
      </div>

      <div className="space-y-7 px-4 pt-6 sm:px-6">
        <div className="grid gap-3">
          <article className="rounded-3xl border border-white/10 bg-zinc-900/80 p-5">
            <div className="flex items-start gap-4">
              <span className="grid size-11 shrink-0 place-items-center rounded-2xl bg-emerald-300 text-black">
                <CalendarDays aria-hidden="true" className="size-5" />
              </span>
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-zinc-500">When</p>
                <p className="mt-1 font-semibold capitalize text-white">{formatEventDateLong(event.startAt)}</p>
                <p className="mt-1 flex items-center gap-2 text-sm text-zinc-400">
                  <Clock3 aria-hidden="true" className="size-4" />
                  {formatEventTimeRange(event.startAt, event.endAt)}
                </p>
              </div>
            </div>
          </article>

          <article className="rounded-3xl border border-white/10 bg-zinc-900/80 p-5">
            <div className="flex items-start gap-4">
              <span className="grid size-11 shrink-0 place-items-center rounded-2xl bg-blue-400 text-black">
                <MapPin aria-hidden="true" className="size-5" />
              </span>
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-zinc-500">Where</p>
                <p className="mt-1 font-semibold text-white">{event.venueName}</p>
                <p className="mt-1 text-sm text-zinc-400">Bielsko-Biała</p>
              </div>
            </div>
          </article>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <article className="rounded-3xl border border-white/10 bg-white/[0.04] p-4">
            <UsersRound aria-hidden="true" className="size-5 text-emerald-300" />
            <p className="mt-4 text-2xl font-bold tracking-tight text-white">{event.participantsCount}</p>
            <p className="mt-1 text-xs leading-5 text-zinc-400">people are already going</p>
          </article>
          <article className="rounded-3xl border border-white/10 bg-white/[0.04] p-4">
            <span className="inline-flex size-5 items-center justify-center rounded-full bg-cyan-300 text-xs font-black text-black">
              {event.crewAvailable ? <CircleCheck aria-hidden="true" className="size-5" /> : <CircleMinus aria-hidden="true" className="size-5" />}
            </span>
            <p className="mt-4 text-base font-bold text-white">
              {event.crewAvailable ? 'CREW available' : 'No crews yet'}
            </p>
            <p className="mt-1 text-xs leading-5 text-zinc-400">
              {event.crewAvailable ? 'Join after confirming attendance' : 'Groups are not available yet'}
            </p>
          </article>
        </div>

        <div>
          <div className="mb-3 flex items-end justify-between gap-4">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-emerald-300">Available transport</p>
              <h2 className="mt-1 text-xl font-bold tracking-tight text-white">How can you get there?</h2>
            </div>
            <span className="text-xs text-zinc-500">Choose in the next step</span>
          </div>

          {event.availableTransportModes.length === 0 ? (
            <p className="rounded-2xl border border-dashed border-white/15 p-4 text-sm text-zinc-400">
              Transport options will appear soon.
            </p>
          ) : (
            <ul className="grid grid-cols-2 gap-2">
              {event.availableTransportModes.map((mode) => (
                <li key={mode} className="flex min-h-20 items-center gap-3 rounded-2xl border border-white/10 bg-zinc-900 px-4 py-3 text-sm font-medium text-zinc-200">
                  <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-white/10 text-emerald-300">
                    <TransportIcon mode={mode} />
                  </span>
                  {transportLabels[mode] ?? mode}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="fixed inset-x-0 bottom-0 z-20 mx-auto max-w-[520px] border-t border-white/10 bg-black/90 px-4 pb-[max(1rem,env(safe-area-inset-bottom))] pt-3 backdrop-blur-xl">
        <div className="mx-auto max-w-lg">
          <button
            type="button"
            onClick={onContinue}
            className="flex min-h-14 w-full items-center justify-between rounded-2xl bg-emerald-300 px-5 py-3.5 text-left font-bold text-black shadow-[0_12px_40px_rgba(110,231,183,0.22)] transition hover:bg-emerald-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-100 focus-visible:ring-offset-4 focus-visible:ring-offset-black active:scale-[0.99]"
          >
            <span>
              <span className="block text-base">Plan my trip</span>
              <span className="block text-xs font-medium text-black/60">Choose how you&apos;ll get there</span>
            </span>
            <ArrowRight aria-hidden="true" className="size-5" />
          </button>
        </div>
      </div>
    </section>
  )
}
