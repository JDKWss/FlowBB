import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { AppShell } from './components/AppShell'
import { Alert, AlertDescription, StatePanel } from './components/ui'
import { EventDetailsView, EventListView } from './features/events'
const AttendanceView = lazy(() => import('./features/journey/AttendanceView').then(module => ({ default: module.AttendanceView })))
const RouteView = lazy(() => import('./features/journey/RouteView').then(module => ({ default: module.RouteView })))
const CrewView = lazy(() => import('./features/journey/CrewView').then(module => ({ default: module.CrewView })))
import {
  useEvent,
  useEvents,
  useGroups,
  useRoute,
  useSaveAttendance,
  useToggleGroup,
} from './hooks/useClientData'
import type {
  AttendanceResponse,
  GroupSummary,
  TransportMode,
} from './types/contracts'

type Screen = 'events' | 'details' | 'attendance' | 'route' | 'crew'

type NavigationState = {
  screen: Screen
  eventId: string | null
}

type FlowHistoryState = NavigationState & {
  flowBB: true
  depth: number
}

const screens: Screen[] = ['events', 'details', 'attendance', 'route', 'crew']

function readFlowHistoryState(value: unknown): FlowHistoryState | null {
  if (!value || typeof value !== 'object') return null

  const state = value as Partial<FlowHistoryState>
  if (
    state.flowBB !== true ||
    !screens.includes(state.screen as Screen) ||
    !Number.isInteger(state.depth) ||
    (state.depth ?? -1) < 0
  ) {
    return null
  }

  const eventId = state.eventId ?? null
  if (state.screen !== 'events' && !eventId) return null

  return {
    flowBB: true,
    screen: state.screen as Screen,
    eventId: state.screen === 'events' ? null : eventId,
    depth: state.depth as number,
  }
}

function getInitialNavigation(): NavigationState {
  const entry = readFlowHistoryState(window.history.state)
  return entry
    ? { screen: entry.screen, eventId: entry.eventId }
    : { screen: 'events', eventId: null }
}

const stepLabels: Record<Screen, string> = {
  events: 'Explore',
  details: 'Event',
  attendance: 'Transport',
  route: 'Route',
  crew: 'Crew',
}

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'Something went wrong. Please try again.'

function ScreenState({
  kind,
  title,
  description,
  onRetry,
}: {
  kind: 'loading' | 'error'
  title: string
  description: string
  onRetry?: () => void
}) {
  return (
    <div className="flex min-h-full items-center bg-slate-950 px-5 text-white">
      <StatePanel
        className="w-full bg-card text-white [&_h2]:text-white [&_p]:text-muted-foreground"
        kind={kind}
        title={title}
        description={description}
        actionLabel={onRetry ? 'Try again' : undefined}
        onAction={onRetry}
      />
    </div>
  )
}

export default function App() {
  const [navigation, setNavigation] = useState(getInitialNavigation)
  const { screen, eventId } = navigation
  const [selectedMode, setSelectedMode] =
    useState<TransportMode>('PublicTransport')
  const [confirmation, setConfirmation] =
    useState<AttendanceResponse | null>(null)
  const initialNavigation = useRef(navigation)
  const screenRoot = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const currentEntry = readFlowHistoryState(window.history.state)
    const initialEntry = initialNavigation.current
    window.history.replaceState(
      {
        flowBB: true,
        screen: initialEntry.screen,
        eventId: initialEntry.eventId,
        depth: currentEntry?.depth ?? 0,
      } satisfies FlowHistoryState,
      '',
    )

    const handlePopState = (event: PopStateEvent) => {
      const entry = readFlowHistoryState(event.state)
      if (!entry) return
      setNavigation({ screen: entry.screen, eventId: entry.eventId })
    }

    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
    // This initializes history once; later changes are written by navigate().
  }, [])

  const navigate = useCallback(
    (nextScreen: Screen, nextEventId: string | null = eventId) => {
      const normalizedEventId = nextScreen === 'events' ? null : nextEventId
      if (nextScreen !== 'events' && !normalizedEventId) return

      const currentEntry = readFlowHistoryState(window.history.state)
      if (
        currentEntry?.screen === nextScreen &&
        currentEntry.eventId === normalizedEventId
      ) {
        setNavigation({ screen: nextScreen, eventId: normalizedEventId })
        return
      }

      const nextEntry: FlowHistoryState = {
        flowBB: true,
        screen: nextScreen,
        eventId: normalizedEventId,
        depth: (currentEntry?.depth ?? -1) + 1,
      }
      window.history.pushState(nextEntry, '')
      setNavigation({ screen: nextScreen, eventId: normalizedEventId })
    },
    [eventId],
  )

  const goBack = useCallback(
    (fallbackScreen: Screen, fallbackEventId: string | null = eventId) => {
      const currentEntry = readFlowHistoryState(window.history.state)
      if (currentEntry && currentEntry.depth > 0) {
        window.history.back()
        return
      }

      navigate(fallbackScreen, fallbackEventId)
    },
    [eventId, navigate],
  )

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'instant' })
    screenRoot.current?.focus({ preventScroll: true })
  }, [screen])

  const eventsQuery = useEvents()
  const eventQuery = useEvent(eventId)
  const routeQuery = useRoute(eventId, screen === 'route' || screen === 'crew')
  const groupsQuery = useGroups(eventId, screen === 'crew')
  const attendanceMutation = useSaveAttendance()
  const groupMutation = useToggleGroup(eventId ?? 'none')

  const openEvent = (nextEventId: string) => {
    setConfirmation(null)
    setSelectedMode('PublicTransport')
    navigate('details', nextEventId)
  }

  const startAttendance = () => {
    const modes = eventQuery.data?.availableTransportModes ?? []
    const defaultMode = modes.includes('PublicTransport')
      ? 'PublicTransport'
      : (modes.find((mode) => mode !== 'Unknown') ?? 'Walking')
    setSelectedMode(defaultMode)
    setConfirmation(null)
    attendanceMutation.reset()
    navigate('attendance')
  }

  const saveAttendance = async () => {
    if (!eventId) return
    try {
      const result = await attendanceMutation.mutateAsync({
        eventId,
        transportMode: selectedMode,
      })
      setConfirmation(result)
    } catch {
      // The mutation exposes its error state below so the user can retry.
    }
  }

  const toggleGroup = async (group: GroupSummary) => {
    try {
      await groupMutation.mutateAsync(group)
    } catch {
      // The mutation exposes its error state below without breaking navigation.
    }
  }

  let content

  if (screen === 'events') {
    content = (
      <EventListView
        events={eventsQuery.data ?? []}
        isLoading={eventsQuery.isLoading}
        error={eventsQuery.error ? errorMessage(eventsQuery.error) : null}
        onSelectEvent={openEvent}
        onRetry={() => void eventsQuery.refetch()}
      />
    )
  } else if (eventQuery.isLoading || !eventQuery.data) {
    content = eventQuery.isError ? (
      <ScreenState
        kind="error"
        title="Event unavailable"
        description={errorMessage(eventQuery.error)}
        onRetry={() => void eventQuery.refetch()}
      />
    ) : (
      <ScreenState
        kind="loading"
        title="Opening event"
        description="Preparing the details for your plan."
      />
    )
  } else if (screen === 'details') {
    content = (
      <EventDetailsView
        event={eventQuery.data}
        onBack={() => goBack('events', null)}
        onContinue={startAttendance}
      />
    )
  } else if (screen === 'attendance') {
    content = (
      <>
        {attendanceMutation.isError ? (
          <Alert variant="destructive" className="fixed inset-x-5 top-[var(--phone-safe-top,1rem)] z-50 bg-rose-950/95 shadow-2xl">
            <AlertDescription className="text-rose-100">{errorMessage(attendanceMutation.error)}</AlertDescription>
          </Alert>
        ) : null}
        <AttendanceView
          event={eventQuery.data}
          selectedMode={selectedMode}
          onSelectMode={(mode) => {
            setSelectedMode(mode)
            setConfirmation(null)
            attendanceMutation.reset()
          }}
          onSubmit={() => void saveAttendance()}
          onContinue={() => navigate('route')}
          onBack={() => goBack('details')}
          isSubmitting={attendanceMutation.isPending}
          confirmation={confirmation}
        />
      </>
    )
  } else if (screen === 'route') {
    content = routeQuery.isError ? (
      <ScreenState
        kind="error"
        title="Route unavailable"
        description={errorMessage(routeQuery.error)}
        onRetry={() => void routeQuery.refetch()}
      />
    ) : routeQuery.data ? (
      <RouteView
        event={eventQuery.data}
        route={routeQuery.data}
        onBack={() => goBack('attendance')}
        onContinue={() => navigate('crew')}
      />
    ) : (
      <ScreenState
        kind="loading"
        title="Planning your route"
        description="Checking the outbound trip and return options."
      />
    )
  } else {
    content = groupsQuery.isError ? (
      <ScreenState
        kind="error"
        title="Crews unavailable"
        description={errorMessage(groupsQuery.error)}
        onRetry={() => void groupsQuery.refetch()}
      />
    ) : groupsQuery.data ? (
      <>
        {groupMutation.isError ? (
          <Alert variant="destructive" className="fixed inset-x-5 top-[var(--phone-safe-top,1rem)] z-50 bg-rose-950/95 shadow-2xl">
            <AlertDescription className="text-rose-100">{errorMessage(groupMutation.error)}</AlertDescription>
          </Alert>
        ) : null}
        <CrewView
          event={eventQuery.data}
          groups={groupsQuery.data}
          onToggleGroup={(group) => void toggleGroup(group)}
          onBack={() => goBack('route')}
          isUpdating={groupMutation.isPending}
        />
      </>
    ) : (
      <ScreenState
        kind="loading"
        title="Finding your crew"
        description="Looking for small groups connected to this event."
      />
    )
  }

  const step = ['events', 'details', 'attendance', 'route', 'crew'].indexOf(screen) + 1
  return (
    <AppShell bare currentStep={step} totalSteps={5} stepLabel={stepLabels[screen]}>
      <AnimatePresence mode="wait" initial={false}>
        <motion.div key={screen} ref={screenRoot} tabIndex={-1}
          className="outline-none focus-visible:outline-none"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
          transition={{ duration: 0.16 }}>
          <Suspense fallback={<ScreenState kind="loading" title="Preparing your plan" description="Just a moment…" />}>{content}</Suspense>
        </motion.div>
      </AnimatePresence>
    </AppShell>
  )
}
