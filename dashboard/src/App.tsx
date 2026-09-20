import { useState } from 'react'
import {
  Activity,
  Building2,
  CalendarPlus,
  RefreshCw,
  Route,
  TriangleAlert,
} from 'lucide-react'
import { CreateEventView } from './components/CreateEventView'
import { PulseView } from './components/PulseView'
import { useEvents } from './hooks/useDashboardData'
import { usePulseConnection } from './hooks/usePulseConnection'

const GOLDEN_EVENT_ID = '11111111-1111-1111-1111-111111111111'

type Section = 'pulse' | 'create'

export default function App() {
  const [section, setSection] = useState<Section>('pulse')
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)
  const events = useEvents()

  const defaultEvent = events.data?.find(event => event.id === GOLDEN_EVENT_ID)
    ?? events.data?.[0]
  const selectedEvent = events.data?.find(event => event.id === selectedEventId)
    ?? defaultEvent
  const activeEventId = selectedEvent?.id ?? null
  usePulseConnection(activeEventId)

  return (
    <div className="dashboard-shell">
      <header className="product-header">
        <a className="brand" href="/" aria-label="FlowBB dashboard home">
          <span className="brand__mark"><Route aria-hidden="true" size={22} /></span>
          <span>FlowBB<span>.</span></span>
        </a>

        <nav className="primary-nav" aria-label="Dashboard sections">
          <button
            type="button"
            className={section === 'pulse' ? 'is-active' : ''}
            onClick={() => setSection('pulse')}
          >
            <Activity aria-hidden="true" size={17} />
            PULSE
          </button>
          <button
            type="button"
            className={section === 'create' ? 'is-active' : ''}
            onClick={() => setSection('create')}
          >
            <CalendarPlus aria-hidden="true" size={17} />
            Add event
          </button>
        </nav>

        <div className="workspace-label">
          <Building2 aria-hidden="true" size={17} />
          City & organizer
        </div>
      </header>

      <main className="dashboard-main">
        {section === 'create' ? (
          <CreateEventView
            onViewEvent={eventId => {
              setSelectedEventId(eventId)
              setSection('pulse')
            }}
          />
        ) : events.isLoading ? (
          <div className="state-panel" role="status">
            <RefreshCw aria-hidden="true" className="spin" />
            <strong>Connecting to FlowBB</strong>
            <p>Loading real events from the ASP.NET API.</p>
          </div>
        ) : events.isError ? (
          <div className="state-panel state-panel--error" role="alert">
            <TriangleAlert aria-hidden="true" />
            <strong>Events are unavailable</strong>
            <p>{events.error instanceof Error ? events.error.message : 'Could not reach the API.'}</p>
            <button type="button" className="button button--secondary" onClick={() => void events.refetch()}>
              Try again
            </button>
          </div>
        ) : !events.data?.length ? (
          <div className="state-panel">
            <CalendarPlus aria-hidden="true" />
            <strong>No events yet</strong>
            <p>Create the first event from the organizer workspace.</p>
            <button type="button" className="button button--primary" onClick={() => setSection('create')}>
              Add event
            </button>
          </div>
        ) : selectedEvent ? (
          <PulseView
            events={events.data}
            selectedEvent={selectedEvent}
            onSelectEvent={setSelectedEventId}
          />
        ) : null}
      </main>
    </div>
  )
}
