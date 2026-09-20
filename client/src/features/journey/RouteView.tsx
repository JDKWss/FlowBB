import {
  Bike,
  BusFront,
  CarFront,
  ChevronRight,
  Circle,
  Clock3,
  Footprints,
  MapPin,
  Navigation,
  Route,
  Timer,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react'
import { DemoBadge } from '../../components/DemoBadge'
import { FlowBackButton } from '../../components/FlowBackButton'
import { RouteMap } from '../../components/route/RouteMap'
import { TransitStopsMap } from '../../components/route/TransitStopsMap'
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
  PlannerSource,
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

// Etykieta zrodla planera. Record zamiast switch: nowa wartosc PlannerSource nie skompiluje sie bez etykiety,
// wiec surowa nazwa enuma nigdy nie wycieknie do UI. Sparkles zostaje wylacznie znacznikiem danych demo (DemoBadge).
// Etykiety sa zapisane w docelowej wielkosci liter, bo globalny reset CSS w #root wylacza text-transform.
type PlannerBadgeDetails =
  | { kind: 'demo' }
  | { kind: 'planner'; label: string; description: string; icon: LucideIcon; className: string }

const plannerBadges: Record<PlannerSource, PlannerBadgeDetails> = {
  MzkTimetable: {
    kind: 'planner',
    label: 'MZK timetable',
    description: 'Planned from the published MZK timetable',
    icon: BusFront,
    className: 'bg-primary/15 text-primary',
  },
  RoadRouting: {
    kind: 'planner',
    label: 'Road routing',
    description: 'Planned on the road network',
    icon: Navigation,
    className: 'bg-white/[0.06] text-neutral-300',
  },
  OpenTripPlanner: {
    kind: 'planner',
    label: 'OpenTripPlanner',
    description: 'Planned by OpenTripPlanner',
    icon: Route,
    className: 'bg-white/[0.06] text-neutral-300',
  },
  Demo: { kind: 'demo' },
}

function PlannerSourceBadge({ source }: { source: PlannerSource }) {
  const details = plannerBadges[source]
  if (details.kind === 'demo') return <DemoBadge compact />

  const PlannerIcon = details.icon
  return (
    <Badge
      variant="secondary"
      title={details.description}
      className={`h-auto shrink-0 gap-1.5 px-2.5 py-1 text-[10px] font-bold tracking-wider ${details.className}`}
    >
      <PlannerIcon aria-hidden="true" size={11} />
      {details.label}
    </Badge>
  )
}

function formatWait(minutes: number) {
  if (minutes < 60) return `${minutes} min`
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`
}

// Czekanie to tylko roznica dwoch godzin, ktore ekran i tak pokazuje. Regule "czy jest luka" decyduje backend
// przez route.returnGap; klient jej nie odtwarza, wiec nie ma tu zadnego progu.
function waitMinutesBetween(endAt: string | null | undefined, departureAt: string | null | undefined) {
  if (!endAt || !departureAt) return null
  const end = new Date(endAt).getTime()
  const departure = new Date(departureAt).getTime()
  if (!Number.isFinite(end) || !Number.isFinite(departure)) return null
  return Math.max(0, Math.round((departure - end) / 60_000))
}

// fromTimetable rozroznia dwa zrodla luki: planer z rozkladu MZK (prawdziwe godziny) i regula demo (22:00, symulacja).
// Twierdzenie "rozklad nie ma odjazdu" jest prawdziwe tylko w pierwszym przypadku.
function ReturnGapAlert({ endAt, firstReturnAt, fromTimetable }: {
  endAt?: string | null
  firstReturnAt?: string | null
  fromTimetable: boolean
}) {
  const waitMinutes = waitMinutesBetween(endAt, firstReturnAt)
  const noDeparture = fromTimetable
    ? (endAt ? `The MZK timetable has no departure after ${formatTime(endAt)}. ` : 'The MZK timetable has no departure after this event. ')
    : 'There may be no convenient way home after this event. '

  return (
    <Alert className="mb-3 bg-amber-300/10 text-amber-100">
      <TriangleAlert className="text-amber-300" aria-hidden="true" size={19} />
      <AlertTitle>
        {firstReturnAt ? 'Long wait for your return' : 'No return connection after this event'}
      </AlertTitle>
      <AlertDescription className="text-amber-100/70">
        {firstReturnAt ? (
          <>
            <span className="block font-semibold text-amber-100">
              {endAt ? `Event ends ${formatTime(endAt)} · ` : ''}
              First return {formatTime(firstReturnAt)}
              {waitMinutes !== null ? ` · ${formatWait(waitMinutes)} wait` : ''}
            </span>
            <span className="mt-1 block">Check the options below, or arrange another way home.</span>
          </>
        ) : (
          <>
            {noDeparture}
            Arrange another way home before you go.
          </>
        )}
      </AlertDescription>
    </Alert>
  )
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
            <p className="mt-1 flex items-center gap-2 text-xs text-slate-500">
              <span>{step.durationMinutes} min</span>
              {step.line && (
                <Badge
                  variant="secondary"
                  data-testid="route-step-line"
                  className="h-auto bg-primary/15 px-2 py-0.5 text-[10px] font-bold text-primary"
                >
                  Line {step.line}
                </Badge>
              )}
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
  // Powroty w kolejnosci odjazdu, niezaleznie od kolejnosci w odpowiedzi.
  const sortedReturns = [...route.returns].sort(
    (a, b) => new Date(a.departureAt).getTime() - new Date(b.departureAt).getTime(),
  )

  return (
    <section className="min-h-full w-full bg-background px-5 pb-6 pt-4 text-white">
      <header className="mb-7 flex items-center justify-between gap-3">
        <FlowBackButton label="Back to transport selection" onClick={onBack} />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm text-slate-400">{event.name}</p>
        </div>
        <PlannerSourceBadge source={route.plannerSource} />
      </header>

      {selectedMode === 'PublicTransport' && route.outbound.stops && route.outbound.stops.length >= 2 && (
        <TransitStopsMap
          key={`${event.id}-transit`}
          event={event}
          stops={route.outbound.stops}
        />
      )}

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
            <h2 className="text-xl font-bold">Return options</h2>
          </div>
          <span className="text-xs text-slate-500">
            {route.returns.length} {route.returns.length === 1 ? 'option' : 'options'}
          </span>
        </div>

        {route.returnGap && (
          <ReturnGapAlert
            endAt={event.endAt}
            firstReturnAt={sortedReturns[0]?.departureAt}
            fromTimetable={route.plannerSource === 'MzkTimetable'}
          />
        )}

        {route.returns.length > 0 ? (
          <div className="space-y-3">
            {sortedReturns.map((journey, index) => (
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
          !route.returnGap && (
            <div className="rounded-2xl border border-dashed border-white/15 p-5 text-center text-sm text-slate-400">
              No return journeys are available yet.
            </div>
          )
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
