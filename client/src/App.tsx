import { lazy, Suspense, useEffect, useRef, useState } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { AppShell } from './components/AppShell'
import { StatePanel } from './components/ui'
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
    <div className="flex min-h-dvh items-center bg-slate-950 px-5 text-white">
      <StatePanel
        className="w-full border-white/10 bg-slate-900 text-white [&_h2]:text-white [&_p]:text-slate-400"
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
  const [screen, setScreen] = useState<Screen>('events')
  const [eventId, setEventId] = useState<string | null>(null)
  const [selectedMode, setSelectedMode] =
    useState<TransportMode>('PublicTransport')
  const [confirmation, setConfirmation] =
    useState<AttendanceResponse | null>(null)
  const screenRoot = useRef<HTMLDivElement>(null)
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
    setEventId(nextEventId)
    setConfirmation(null)
    setSelectedMode('PublicTransport')
    setScreen('details')
  }

  const startAttendance = () => {
    const modes = eventQuery.data?.availableTransportModes ?? []
    const defaultMode = modes.includes('PublicTransport')
      ? 'PublicTransport'
      : (modes.find((mode) => mode !== 'Unknown') ?? 'Walking')
    setSelectedMode(defaultMode)
    setConfirmation(null)
    attendanceMutation.reset()
    setScreen('attendance')
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
        onBack={() => setScreen('events')}
        onContinue={startAttendance}
      />
    )
  } else if (screen === 'attendance') {
    content = (
      <>
        {attendanceMutation.isError ? (
          <div
            className="fixed inset-x-5 top-4 z-50 mx-auto max-w-sm rounded-2xl border border-rose-300/30 bg-rose-950/95 p-4 text-sm text-rose-100 shadow-2xl"
            role="alert"
          >
            {errorMessage(attendanceMutation.error)}
          </div>
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
          onContinue={() => setScreen('route')}
          onBack={() => setScreen('details')}
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
        onBack={() => setScreen('attendance')}
        onContinue={() => setScreen('crew')}
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
          <div
            className="fixed inset-x-5 top-4 z-50 mx-auto max-w-sm rounded-2xl border border-rose-300/30 bg-rose-950/95 p-4 text-sm text-rose-100 shadow-2xl"
            role="alert"
          >
            {errorMessage(groupMutation.error)}
          </div>
        ) : null}
        <CrewView
          event={eventQuery.data}
          groups={groupsQuery.data}
          onToggleGroup={(group) => void toggleGroup(group)}
          onBack={() => setScreen('route')}
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
