import {
  CalendarClock,
  CheckCircle2,
  MapPin,
  RefreshCw,
  TriangleAlert,
} from 'lucide-react'
import {
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
} from 'recharts'
import {
  useAirQuality,
  useEventPulse,
  usePulseHexagons,
  usePulseSummary,
} from '../hooks/useDashboardData'
import type { EventSummary, ModalSplit } from '../types/contracts'
import { AirQualityCard } from './AirQualityCard'
import { PulseMap } from './PulseMap'

const splitConfig: Array<{
  key: keyof ModalSplit
  label: string
  color: string
}> = [
  { key: 'publicTransport', label: 'Public transport', color: '#a7f3d0' },
  { key: 'walking', label: 'Walking', color: '#6ee7b7' },
  { key: 'bike', label: 'Bike', color: '#3f9f7b' },
  { key: 'car', label: 'Car', color: '#5b6763' },
  { key: 'unknown', label: 'Unknown', color: '#303735' },
]

const formatEventTime = (event: EventSummary) => {
  const formatter = new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Europe/Warsaw',
  })
  return formatter.format(new Date(event.startAt))
}

function LoadingPanel() {
  return (
    <div className="state-panel" role="status">
      <RefreshCw aria-hidden="true" className="spin" />
      <strong>Loading live demand</strong>
      <p>Reading aggregate PULSE data from FlowBB.</p>
    </div>
  )
}

export function PulseView({
  events,
  selectedEvent,
  onSelectEvent,
}: {
  events: EventSummary[]
  selectedEvent: EventSummary
  onSelectEvent: (eventId: string) => void
}) {
  const summary = usePulseSummary()
  const pulse = useEventPulse(selectedEvent.id)
  const hexagons = usePulseHexagons(selectedEvent.id)
  const airQuality = useAirQuality(selectedEvent.id)

  if (pulse.isLoading) return <LoadingPanel />

  if (pulse.isError || !pulse.data) {
    return (
      <div className="state-panel state-panel--error" role="alert">
        <TriangleAlert aria-hidden="true" />
        <strong>PULSE is unavailable</strong>
        <p>{pulse.error instanceof Error ? pulse.error.message : 'Could not load event aggregates.'}</p>
        <button type="button" className="button button--secondary" onClick={() => void pulse.refetch()}>
          Try again
        </button>
      </div>
    )
  }

  const split = pulse.data.modalSplit
  const chartData = splitConfig
    .map(item => ({ ...item, value: split[item.key] }))
    .filter(item => item.value > 0)

  return (
    <div className="pulse-view">
      <section className="event-toolbar card">
        <div className="event-toolbar__selector">
          <label className="sr-only" htmlFor="event-select">Event</label>
          <select
            id="event-select"
            value={selectedEvent.id}
            onChange={event => onSelectEvent(event.target.value)}
          >
            {events.map(event => (
              <option key={event.id} value={event.id}>{event.name}</option>
            ))}
          </select>
        </div>
        <div className="event-toolbar__meta">
          <span><MapPin aria-hidden="true" size={16} />{selectedEvent.venueName}</span>
          <span><CalendarClock aria-hidden="true" size={16} />{formatEventTime(selectedEvent)}</span>
        </div>
      </section>

      <section className="section-heading">
        <div>
          <h1>{selectedEvent.name}</h1>
          <p>
            Aggregate mobility intent for the selected event.
            {summary.data
              ? ` ${summary.data.participantsCount} participants across ${summary.data.eventsCount} events citywide.`
              : ''}
          </p>
        </div>
      </section>

      <section className="pulse-grid">
        <article className="card map-card">
          <header className="card-heading">
            <div>
              <h2>Demand map</h2>
            </div>
          </header>
          <PulseMap
            key={selectedEvent.id}
            event={selectedEvent}
            hexagons={hexagons.data}
            isLoading={hexagons.isLoading || hexagons.isFetching}
            error={hexagons.error instanceof Error ? hexagons.error.message : null}
          />
        </article>

        <aside className="pulse-sidebar">
          <article className="card split-card">
            <header className="card-heading">
              <div>
                <h2>Transport split</h2>
              </div>
            </header>
            <div
              className="split-chart"
              role="img"
              aria-label={splitConfig.map(item => `${item.label}: ${split[item.key]}`).join(', ')}
            >
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={chartData}
                    dataKey="value"
                    nameKey="label"
                    innerRadius="62%"
                    outerRadius="88%"
                    paddingAngle={2}
                    stroke="none"
                  >
                    {chartData.map(item => <Cell key={item.key} fill={item.color} />)}
                  </Pie>
                  <Tooltip
                    contentStyle={{
                      background: '#171717',
                      border: '1px solid rgba(255,255,255,.1)',
                      borderRadius: 14,
                      color: '#fff',
                    }}
                  />
                </PieChart>
              </ResponsiveContainer>
              <div className="split-chart__total">
                <strong>{pulse.data.participantsCount}</strong>
                <span>people</span>
              </div>
            </div>
            <ul className="split-legend">
              {splitConfig.map(item => (
                <li key={item.key}>
                  <span style={{ background: item.color }} />
                  <em>{item.label}</em>
                  <strong>{split[item.key]}</strong>
                </li>
              ))}
            </ul>
          </article>

          <article className="card alerts-card">
            <header className="card-heading">
              <div>
                <h2>Return & alerts</h2>
              </div>
            </header>
            {pulse.data.alerts.length > 0 ? (
              <ul className="alert-list">
                {pulse.data.alerts.map(alert => (
                  <li key={`${alert.code}-${alert.message}`}>
                    <TriangleAlert aria-hidden="true" size={18} />
                    <div><strong>{alert.code}</strong><p>{alert.message}</p></div>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="healthy-state">
                <CheckCircle2 aria-hidden="true" size={21} />
                <div>
                  <strong>No active alerts</strong>
                  <p>Backend reports no return-gap or coverage alerts for this event.</p>
                </div>
              </div>
            )}
            <footer className="alerts-source">
              City-level return alert uses a fixed 22:00 rule, not the MZK timetable. A resident&apos;s route card in the
              client plans the return from the published timetable, so the two can differ.
            </footer>
          </article>

          <AirQualityCard
            data={airQuality.data}
            isLoading={airQuality.isLoading}
            error={airQuality.error instanceof Error ? airQuality.error.message : null}
            onRetry={() => void airQuality.refetch()}
          />
        </aside>
      </section>
    </div>
  )
}
