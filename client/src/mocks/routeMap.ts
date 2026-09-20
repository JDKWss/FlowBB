import type { EventDetails, TransportMode } from '../types/contracts'
import bikeRoute from './demo/route-bike.json'
import carRoute from './demo/route-car.json'
import walkingRoute from './demo/route-walking.json'

export const GOLDEN_DEMO_EVENT_ID = '11111111-1111-1111-1111-111111111111'

export type MapRouteMode = Extract<TransportMode, 'Walking' | 'Bike' | 'Car'>

export type MapPoint = {
  latitude: number
  longitude: number
}

export type DemoRouteStep = {
  instruction: string
  distanceMeters: number
  durationSeconds: number
}

export type DemoRouteFixture = {
  eventId: string
  mode: MapRouteMode
  origin: MapPoint & { label: string }
  destination: MapPoint & { label: string }
  distanceKm: number
  durationMinutes: number
  geometry: {
    type: 'LineString'
    coordinates: Array<[number, number]>
  }
  steps: DemoRouteStep[]
}

const demoRoutes: Record<MapRouteMode, DemoRouteFixture> = {
  Walking: walkingRoute as DemoRouteFixture,
  Bike: bikeRoute as DemoRouteFixture,
  Car: carRoute as DemoRouteFixture,
}

export function hasDemoRoute(eventId: string) {
  return eventId === GOLDEN_DEMO_EVENT_ID
}

export function getDemoRoute(
  mode: MapRouteMode,
  event: EventDetails,
): DemoRouteFixture {
  const route = demoRoutes[mode]
  if (event.id !== route.eventId) {
    throw new Error(`No ${mode} demo route fixture exists for event ${event.id}`)
  }

  return route
}
