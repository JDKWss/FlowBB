import {
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
  UsersRound,
} from 'lucide-react'
import { DemoBadge } from '../../components/DemoBadge'
import { FlowBackButton } from '../../components/FlowBackButton'
import { Badge, Button, Card } from '../../components/ui'
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
    <section aria-labelledby="event-title" className="w-full pb-36">
      <div className="bg-background px-5 pb-8 pt-5">
        <FlowBackButton label="Back to events" onClick={onBack} />

        <div className="relative mt-10">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="secondary" className="h-auto bg-white/[0.06] px-3 py-1 text-[0.68rem] font-bold uppercase tracking-[0.18em] text-neutral-300">
              {getCategoryLabel(event.category)}
            </Badge>
            {event.source === 'Demo' && (
              <DemoBadge />
            )}
          </div>

          <h1 id="event-title" className="mt-5 text-4xl font-bold leading-[1.05] tracking-[-0.045em] text-white">
            {event.name}
          </h1>
          <p className="mt-4 text-base leading-7 text-zinc-300">{event.description}</p>
        </div>
      </div>

      <div className="space-y-7 px-5 pt-6">
        <div className="grid gap-3">
          <Card className="p-5">
            <div className="flex items-start gap-4">
              <span className="grid size-11 shrink-0 place-items-center rounded-2xl bg-primary text-primary-foreground">
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
          </Card>

          <Card className="p-5">
            <div className="flex items-start gap-4">
              <span className="grid size-11 shrink-0 place-items-center rounded-2xl bg-white text-black">
                <MapPin aria-hidden="true" className="size-5" />
              </span>
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-zinc-500">Where</p>
                <p className="mt-1 font-semibold text-white">{event.venueName}</p>
                <p className="mt-1 text-sm text-zinc-400">Bielsko-Biała</p>
              </div>
            </div>
          </Card>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Card className="p-4">
            <UsersRound aria-hidden="true" className="size-5 text-primary" />
            <p className="mt-4 text-2xl font-bold tracking-tight text-white">{event.participantsCount}</p>
            <p className="mt-1 text-xs leading-5 text-zinc-400">people are already going</p>
          </Card>
          <Card className="p-4">
            <span className="inline-flex size-5 items-center justify-center rounded-full bg-primary text-xs font-black text-primary-foreground">
              {event.crewAvailable ? <CircleCheck aria-hidden="true" className="size-5" /> : <CircleMinus aria-hidden="true" className="size-5" />}
            </span>
            <p className="mt-4 text-base font-bold text-white">
              {event.crewAvailable ? 'CREW available' : 'No crews yet'}
            </p>
            <p className="mt-1 text-xs leading-5 text-zinc-400">
              {event.crewAvailable ? 'Join after confirming attendance' : 'Groups are not available yet'}
            </p>
          </Card>
        </div>

        <div>
          <div className="mb-3 flex items-end justify-between gap-4">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-primary">Available transport</p>
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
                <li key={mode} className="flex min-h-20 items-center gap-3 rounded-2xl bg-card px-4 py-3 text-sm font-medium text-zinc-200">
                  <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-white/[0.06] text-primary">
                    <TransportIcon mode={mode} />
                  </span>
                  {transportLabels[mode] ?? mode}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="fixed inset-x-0 bottom-0 z-20 w-full bg-black/90 px-5 pb-[var(--phone-safe-bottom,1rem)] pt-3 backdrop-blur-xl">
        <div className="w-full">
          <Button
            type="button"
            size="lg"
            onClick={onContinue}
            className="h-auto min-h-14 w-full justify-between py-3.5 text-left"
          >
            <span>
              <span className="block text-base">Plan my trip</span>
              <span className="block text-xs font-medium text-black/60">Choose how you&apos;ll get there</span>
            </span>
            <ArrowRight aria-hidden="true" className="size-5" />
          </Button>
        </div>
      </div>
    </section>
  )
}
