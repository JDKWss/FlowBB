import { useState, type FormEvent } from 'react'
import {
  ArrowRight,
  CalendarPlus,
  CheckCircle2,
  LoaderCircle,
  MapPin,
  TriangleAlert,
} from 'lucide-react'
import { useCreateEvent } from '../hooks/useDashboardData'
import {
  createEventRequestSchema,
  type EventCategory,
  type GeoPoint,
} from '../types/contracts'
import { EventLocationMap } from './EventLocationMap'

const categories: EventCategory[] = [
  'Culture',
  'Sport',
  'Education',
  'Community',
  'Other',
]

type FormState = {
  name: string
  description: string
  venueName: string
  category: EventCategory
  startAt: string
  endAt: string
}

const initialForm: FormState = {
  name: '',
  description: '',
  venueName: '',
  category: 'Community',
  startAt: '2026-09-20T19:00',
  endAt: '2026-09-20T22:00',
}

function toIso(localValue: string) {
  const date = new Date(localValue)
  return Number.isNaN(date.getTime()) ? localValue : date.toISOString()
}

export function CreateEventView({
  onViewEvent,
}: {
  onViewEvent: (eventId: string) => void
}) {
  const [form, setForm] = useState<FormState>(initialForm)
  const [location, setLocation] = useState<GeoPoint | null>(null)
  const [validationErrors, setValidationErrors] = useState<string[]>([])
  const createEvent = useCreateEvent()

  const update = <Key extends keyof FormState>(key: Key, value: FormState[Key]) => {
    setForm(current => ({ ...current, [key]: value }))
    createEvent.reset()
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const candidate = {
      name: form.name,
      description: form.description,
      venueName: form.venueName,
      category: form.category,
      startAt: toIso(form.startAt),
      endAt: form.endAt ? toIso(form.endAt) : null,
      location,
    }
    const parsed = createEventRequestSchema.safeParse(candidate)
    if (!parsed.success) {
      setValidationErrors([...new Set(parsed.error.issues.map(issue => issue.message))])
      return
    }

    setValidationErrors([])
    try {
      await createEvent.mutateAsync(parsed.data)
    } catch {
      // Mutation state renders the backend error without interrupting the form.
    }
  }

  return (
    <div className="create-view">
      <section className="section-heading">
        <div>
          <h1>Add an event</h1>
          <p>Create a real FlowBB event by choosing its place in Bielsko-Biała.</p>
        </div>
        <span className="demo-badge">MVP · NO LOGIN</span>
      </section>

      <div className="create-grid">
        <form className="card event-form" onSubmit={submit} noValidate>
          <header className="card-heading">
            <div>
              <h2>What is happening?</h2>
            </div>
            <span className="icon-tile"><CalendarPlus aria-hidden="true" size={20} /></span>
          </header>

          <div className="field">
            <label htmlFor="event-name">Name</label>
            <input
              id="event-name"
              value={form.name}
              maxLength={160}
              placeholder="FlowBB Demo Event"
              onChange={event => update('name', event.target.value)}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="event-description">Description</label>
            <textarea
              id="event-description"
              value={form.description}
              maxLength={1000}
              rows={4}
              placeholder="Tell residents what to expect."
              onChange={event => update('description', event.target.value)}
              required
            />
          </div>

          <div className="form-row">
            <div className="field">
              <label htmlFor="event-venue">Venue</label>
              <input
                id="event-venue"
                value={form.venueName}
                maxLength={160}
                placeholder="Plac Bolesława Chrobrego"
                onChange={event => update('venueName', event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="event-category">Category</label>
              <select
                id="event-category"
                value={form.category}
                onChange={event => update('category', event.target.value as EventCategory)}
              >
                {categories.map(category => (
                  <option key={category} value={category}>{category}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="form-row">
            <div className="field">
              <label htmlFor="event-start">Starts</label>
              <input
                id="event-start"
                type="datetime-local"
                value={form.startAt}
                onChange={event => update('startAt', event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="event-end">Ends</label>
              <input
                id="event-end"
                type="datetime-local"
                value={form.endAt}
                onChange={event => update('endAt', event.target.value)}
              />
            </div>
          </div>

          <div className={`location-status${location ? ' location-status--selected' : ''}`}>
            <MapPin aria-hidden="true" size={18} />
            {location ? (
              <span>
                Selected <strong>{location.latitude.toFixed(5)}, {location.longitude.toFixed(5)}</strong>
              </span>
            ) : (
              <span>Select a location on the map before saving.</span>
            )}
          </div>

          {validationErrors.length > 0 && (
            <div className="form-message form-message--error" role="alert">
              <TriangleAlert aria-hidden="true" size={18} />
              <div>
                <strong>Check the event details</strong>
                {validationErrors.map(error => <p key={error}>{error}</p>)}
              </div>
            </div>
          )}

          {createEvent.isError && (
            <div className="form-message form-message--error" role="alert">
              <TriangleAlert aria-hidden="true" size={18} />
              <div>
                <strong>Event was not created</strong>
                <p>{createEvent.error instanceof Error ? createEvent.error.message : 'The API request failed.'}</p>
              </div>
            </div>
          )}

          {createEvent.data && (
            <div className="form-message form-message--success" role="status">
              <CheckCircle2 aria-hidden="true" size={19} />
              <div>
                <strong>Event created live</strong>
                <p>{createEvent.data.name} is now available through FlowBB.</p>
                <button
                  type="button"
                  className="inline-action"
                  onClick={() => onViewEvent(createEvent.data.id)}
                >
                  View in PULSE <ArrowRight aria-hidden="true" size={15} />
                </button>
              </div>
            </div>
          )}

          <button
            className="button button--primary button--wide"
            type="submit"
            disabled={!location || createEvent.isPending}
          >
            {createEvent.isPending ? (
              <><LoaderCircle aria-hidden="true" className="spin" size={19} />Saving to FlowBB…</>
            ) : (
              <>Create event <ArrowRight aria-hidden="true" size={19} /></>
            )}
          </button>
        </form>

        <section className="card location-card" aria-labelledby="location-title">
          <header className="card-heading">
            <div>
              <h2 id="location-title">Choose the event location</h2>
            </div>
            {location && <span className="selection-badge">Pin selected</span>}
          </header>
          <EventLocationMap
            value={location}
            onChange={point => {
              setLocation(point)
              setValidationErrors([])
              createEvent.reset()
            }}
          />
          <p className="map-caption">
            The pin becomes the public event location. Participant origins are never shown here.
          </p>
        </section>
      </div>
    </div>
  )
}
