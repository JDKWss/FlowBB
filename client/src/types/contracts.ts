export type EventCategory =
  | 'Culture'
  | 'Sport'
  | 'Education'
  | 'Community'
  | 'Other'

export type TransportMode =
  | 'Walking'
  | 'PublicTransport'
  | 'Bike'
  | 'Car'
  | 'Unknown'

export type EventSource = 'Demo' | 'City' | 'External'

export interface GeoPoint {
  latitude: number
  longitude: number
}

export interface EventSummary {
  id: string
  name: string
  description: string
  startAt: string
  endAt?: string | null
  venueName: string
  category: EventCategory
  location: GeoPoint
  participantsCount: number
  source: EventSource
}

export interface EventDetails extends EventSummary {
  externalId?: string | null
  crewAvailable: boolean
  availableTransportModes: TransportMode[]
}

export interface AttendanceUpsertRequest {
  userId: string
  transportMode: TransportMode
}

export interface AttendanceResponse {
  eventId: string
  userId: string
  transportMode: TransportMode
  participantsCount: number
  isNew: boolean
  updatedAt: string
}

export interface GroupMembershipRequest {
  userId: string
}

export interface MeetingPoint extends GeoPoint {
  name: string
}

export interface GroupSummary {
  id: string
  eventId: string
  name: string
  description: string
  currentMembers: number
  maxMembers: number
  tags: string[]
  meetingPoint: MeetingPoint
  joinedByCurrentUser: boolean
}

export type PlannerSource = 'Demo' | 'RoadRouting' | 'OpenTripPlanner'
export type RouteStepType = 'Walk' | 'Transit' | 'Bike' | 'Car' | 'Wait'

export interface RouteStep {
  type: RouteStepType
  instruction: string
  durationMinutes: number
  line?: string | null
}

export interface JourneyOption {
  durationMinutes: number
  distanceMeters?: number | null
  departureAt: string
  arrivalAt: string
  geometry?: RouteGeometry | null
  steps: RouteStep[]
}

export interface RouteGeometry {
  type: 'LineString'
  coordinates: Array<[number, number]>
}

export interface RouteResponse {
  eventId: string
  userId: string
  plannerSource: PlannerSource
  outbound: JourneyOption
  returns: JourneyOption[]
  returnGap: boolean
}
