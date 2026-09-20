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

export type AirQualityLevel =
  | 'VeryGood'
  | 'Good'
  | 'Moderate'
  | 'Sufficient'
  | 'Bad'
  | 'VeryBad'
  | 'Unknown'

export type AirQualityStatus = 'Fresh' | 'Stale' | 'Fallback'
export type AirQualitySource = 'Gios' | 'Demo'

export interface AirQualityMeasurement {
  value: number
  unit: 'µg/m³'
}

export interface AirQualityResponse {
  eventId: string
  station: {
    name: string
    distanceMeters: number
  }
  measuredAt: string
  qualityLevel: AirQualityLevel
  status: AirQualityStatus
  source: AirQualitySource
  pm10: AirQualityMeasurement | null
  pm25: AirQualityMeasurement | null
  no2: AirQualityMeasurement | null
  o3: AirQualityMeasurement | null
  alert: {
    severity: 'Info' | 'Warning'
    message: string
  } | null
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

export type PlannerSource = 'Demo' | 'RoadRouting' | 'OpenTripPlanner' | 'MzkTimetable'
export type RouteStepType = 'Walk' | 'Transit' | 'Bike' | 'Car' | 'Wait'

export interface RouteStep {
  type: RouteStepType
  instruction: string
  durationMinutes: number
  line?: string | null
}

/**
 * Przystanek komunikacji miejskiej. Wspolrzedne pochodza z publicznej infrastruktury przystankowej,
 * a nie z punktu startu uzytkownika, ktory nigdy nie opuszcza backendu.
 */
export interface RouteStop {
  name: string
  latitude: number
  longitude: number
}

export interface JourneyOption {
  durationMinutes: number
  distanceMeters?: number | null
  departureAt: string
  arrivalAt: string
  geometry?: RouteGeometry | null
  steps: RouteStep[]
  /** Pierwszy element to przystanek wsiadania, ostatni wysiadania. Null poza komunikacja miejska. */
  stops?: RouteStop[] | null
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
