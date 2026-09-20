import {
  createEventRequestSchema,
  eventDetailsSchema,
  eventPulseSchema,
  eventSummarySchema,
  hexagonFeatureCollectionSchema,
  pulseSummarySchema,
  type CreateEventRequest,
} from '../types/contracts'

export const apiBaseUrl = (
  import.meta.env.VITE_API_URL ?? 'http://localhost:8080'
).replace(/\/+$/, '')

type Parser<T> = { parse(value: unknown): T }

export class DashboardHttpError extends Error {
  readonly status?: number

  constructor(message: string, status?: number) {
    super(message)
    this.name = 'DashboardHttpError'
    this.status = status
  }
}

async function request<T>(
  path: string,
  parser: Parser<T>,
  init?: RequestInit,
): Promise<T> {
  const headers = new Headers(init?.headers)
  headers.set('Accept', 'application/json')
  if (init?.body) headers.set('Content-Type', 'application/json')

  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers })
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'Network request failed.'
    throw new DashboardHttpError(`API unavailable: ${detail}`)
  }

  const text = await response.text()
  let payload: unknown
  if (text) {
    try {
      payload = JSON.parse(text)
    } catch {
      throw new DashboardHttpError(
        `API returned invalid JSON (HTTP ${response.status}).`,
        response.status,
      )
    }
  }

  if (!response.ok) {
    const problem = payload && typeof payload === 'object'
      ? payload as { title?: unknown; detail?: unknown }
      : null
    const message = typeof problem?.detail === 'string'
      ? problem.detail
      : typeof problem?.title === 'string'
        ? problem.title
        : `API request failed (HTTP ${response.status}).`
    throw new DashboardHttpError(message, response.status)
  }

  return parser.parse(payload)
}

export const dashboardService = {
  getEvents: () => request('/api/events', eventSummarySchema.array()),
  getPulseSummary: () => request('/api/pulse/summary', pulseSummarySchema),
  getEventPulse: (eventId: string) =>
    request(`/api/pulse/events/${encodeURIComponent(eventId)}`, eventPulseSchema),
  getPulseHexagons: (eventId: string) =>
    request(
      `/api/pulse/hexagons?eventId=${encodeURIComponent(eventId)}`,
      hexagonFeatureCollectionSchema,
    ),
  createEvent: (input: CreateEventRequest) => {
    const body = createEventRequestSchema.parse(input)
    return request('/api/events', eventDetailsSchema, {
      method: 'POST',
      body: JSON.stringify(body),
    })
  },
}
