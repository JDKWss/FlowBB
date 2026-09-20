import {
  Bike,
  BusFront,
  CarFront,
  ChevronRight,
  Circle,
  Clock3,
  Footprints,
  MapPin,
  Route,
  Sparkles,
  Timer,
  TriangleAlert,
} from 'lucide-react'
import { FlowBackButton } from '../../components/FlowBackButton'
import { RouteMap } from '../../components/route/RouteMap'
import {
  Alert,
  AlertDescription,
  AlertTitle,
  Badge,
  Button,
  Card,
  Separator,
} from '../../components/ui'
import type {
  EventDetails,
  JourneyOption,
  RouteResponse,
  RouteStep,
  TransportMode,
} from '../../types/contracts'

export interface RouteViewProps {
  event: EventDetails
  route: RouteResponse
  selectedMode: TransportMode
  onBack: () => void
  onContinue: () => void
}

function supportsRouteMap(
  mode: TransportMode,
): mode is Extract<TransportMode, 'Walking' | 'Bike' | 'Car'> {
  return mode === 'Walking' || mode === 'Bike' || mode === 'Car'
}

const timeFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/Warsaw',
  hour: '2-digit',
  minute: '2-digit',
})

const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/Warsaw',
  weekday: 'short',
  day: 'numeric',
  month: 'short',
})

function formatTime(value: string) {
  return timeFormatter.format(new Date(value))
}

function StepIcon({ type }: Pick<RouteStep, 'type'>) {
  const className = 'size-4'

  switch (type) {
    case 'Transit':
      return <BusFront className={className} aria-hidden="true" />
    case 'Bike':
      return <Bike className={className} aria-hidden="true" />
    case 'Car':
      return <CarFront className={className} aria-hidden="true" />
    case 'Wait':
      return <Clock3 className={className} aria-hidden="true" />
    default:
      return <Footprints className={className} aria-hidden="true" />
  }
}

function JourneyTimeline({ journey }: { journey: JourneyOption }) {
  return (
    <ol className="mt-5 space-y-0">
      {journey.steps.map((step, index) => (
        <li
          key={`${step.type}-${step.instruction}-${index}`}
          className="relative flex gap-3 pb-5 last:pb-0"
        >
          {index < journey.steps.length - 1 && (
            <span
              aria-hidden="true"
              className="absolute left-[17px] top-9 h-[calc(100%-2rem)] w-px bg-white/10"
            />
          )}
          <span className="z-10 grid size-9 shrink-0 place-items-center rounded-full bg-neutral-800 text-primary">
            <StepIcon type={step.type} />
          </span>
          <div className="min-w-0 pt-0.5">
            <p className="text-sm font-semibold leading-5 text-slate-100">
              {step.instruction}
            </p>
            <p className="mt-1 text-xs text-slate-500">
              {step.durationMinutes} min
              {step.line ? ` · Line ${step.line}` : ''}
            </p>
          </div>
        </li>
      ))}
    </ol>
  )
}

function JourneyTimes({ journey }: { journey: JourneyOption }) {
  return (
    <div className="flex items-center gap-3">
      <div>
        <p className="text-2xl font-bold tracking-tight text-white">
          {formatTime(journey.departureAt)}
        </p>
        <p className="text-xs text-slate-500">Departure</p>
      </div>
      <div className="flex flex-1 items-center gap-2" aria-hidden="true">
        <Circle className="size-2 shrink-0 fill-primary text-primary" strokeWidth={0} />
        <Separator className="min-w-0 flex-1 bg-neutral-600" />
        <Circle className="size-2 shrink-0 fill-white text-white" strokeWidth={0} />
      </div>
      <div className="text-right">
        <p className="text-2xl font-bold tracking-tight text-white">
          {formatTime(journey.arrivalAt)}
        </p>
        <p className="text-xs text-slate-500">Arrival</p>
      </div>
    </div>
  )
}

export function RouteView({
  event,
  route,
  selectedMode,
  onBack,
  onContinue,
}: RouteViewProps) {
  return (
    <section className="min-h-full w-full bg-background px-5 pb-6 pt-4 text-white">
      <header className="mb-7 flex items-center justify-between gap-3">
        <FlowBackButton label="Back to transport selection" onClick={onBack} />
        <div className="min-w-0 flex-1">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-primary">
            Your route
          </p>
          <p className="truncate text-sm text-slate-400">{event.name}</p>
        </div>
        <Badge variant="secondary" className="h-auto bg-white/[0.06] px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-neutral-300">
          <Sparkles aria-hidden="true" size={11} />
          {route.plannerSource}
        </Badge>
      </header>

      {supportsRouteMap(selectedMode) && route.outbound.geometry && route.outbound.distanceMeters != null && (
        <RouteMap
          key={`${event.id}-${selectedMode}`}
          mode={selectedMode}
          event={event}
          journey={route.outbound}
        />
      )}

      <Card className="mb-4 p-5">
        <div className="mb-5 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="grid size-9 place-items-center rounded-xl bg-primary text-primary-foreground">
              <Route aria-hidden="true" size={19} />
            </span>
            <div>
              <h1 className="font-bold text-white">To the event</h1>
              <p className="text-xs text-slate-500">
                {dateFormatter.format(new Date(route.outbound.departureAt))}
              </p>
            </div>
          </div>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-white/5 px-3 py-1.5 text-xs font-semibold text-slate-300">
            <Timer aria-hidden="true" size={14} />
            {route.outbound.durationMinutes} min
          </span>
        </div>

        <JourneyTimes journey={route.outbound} />
        <Separator className="my-5" />
        <div>
          <JourneyTimeline journey={route.outbound} />
        </div>

        <div className="mt-5 flex items-center gap-2 rounded-2xl bg-white/[0.04] p-3 text-sm text-slate-300">
          <MapPin className="shrink-0 text-primary" aria-hidden="true" size={17} />
          <span className="truncate">{event.venueName}</span>
        </div>
      </Card>

      <div className="mb-4">
        <div className="mb-3 flex items-end justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-neutral-400">
              After the event
            </p>
            <h2 className="mt-1 text-xl font-bold">Return options</h2>
          </div>
          <span className="text-xs text-slate-500">
            {route.returns.length} {route.returns.length === 1 ? 'option' : 'options'}
          </span>
        </div>

        {route.returnGap && (
          <Alert className="mb-3 bg-amber-300/10 text-amber-100">
            <TriangleAlert className="text-amber-300" aria-hidden="true" size={19} />
            <AlertTitle>Limited return connection</AlertTitle>
            <AlertDescription className="text-amber-100/70">
                There may not be a convenient connection after this event. Check
                the options before you go.
            </AlertDescription>
          </Alert>
        )}

        {route.returns.length > 0 ? (
          <div className="space-y-3">
            {route.returns.map((journey, index) => (
              <details
                open={index === 0}
                key={`${journey.departureAt}-${index}`}
                className="group rounded-2xl bg-card p-4"
              >
                <summary className="flex cursor-pointer list-none items-center gap-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
                  <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-white/[0.06] text-primary">
                    <StepIcon type={journey.steps.find(step => step.type !== 'Wait')?.type ?? 'Walk'} />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="font-bold text-white">
                      {formatTime(journey.departureAt)} → {formatTime(journey.arrivalAt)}
                    </p>
                    <p className="text-xs text-slate-500">
                      {journey.durationMinutes} min · {journey.steps.length}{' '}
                      {journey.steps.length === 1 ? 'step' : 'steps'}
                    </p>
                  </div>
                  <ChevronRight
                    className="text-slate-500 transition-transform group-open:rotate-90"
                    aria-hidden="true"
                    size={18}
                  />
                </summary>
                <Separator className="my-4" />
                <div>
                  <JourneyTimeline journey={journey} />
                </div>
              </details>
            ))}
          </div>
        ) : (
          <div className="rounded-2xl border border-dashed border-white/15 p-5 text-center text-sm text-slate-400">
            No return journeys are available yet.
          </div>
        )}
      </div>

      <Button
        type="button"
        size="lg"
        onClick={onContinue}
        className="w-full"
      >
        Find your crew
        <ChevronRight aria-hidden="true" size={20} />
      </Button>
    </section>
  )
}
