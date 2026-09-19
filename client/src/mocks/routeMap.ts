import type { EventDetails, TransportMode } from '../types/contracts'

export type MapRouteMode = Extract<TransportMode, 'Walking' | 'Bike' | 'Car'>

export type MapPoint = {
  latitude: number
  longitude: number
}

export type MockRouteMap = {
  mode: MapRouteMode
  origin: MapPoint & { label: string }
  destination: MapPoint & { label: string }
  path: MapPoint[]
  distanceKm: number
  durationMinutes: number
}

const origin = {
  latitude: 49.81272,
  longitude: 19.03384,
  label: 'Your location',
} as const

const routeProfiles: Record<
  MapRouteMode,
  { offsets: Array<[number, number]>; speedKmh: number }
> = {
  Walking: {
    offsets: [
      [0.18, -0.0002],
      [0.38, 0.00035],
      [0.61, -0.00015],
      [0.82, 0.00025],
    ],
    speedKmh: 4.7,
  },
  Bike: {
    offsets: [
      [0.16, 0.00055],
      [0.37, 0.0008],
      [0.63, 0.00035],
      [0.84, 0.0006],
    ],
    speedKmh: 15,
  },
  Car: {
    offsets: [
      [0.15, -0.00075],
      [0.34, -0.00105],
      [0.59, -0.0007],
      [0.8, -0.00045],
    ],
    speedKmh: 25,
  },
}

function interpolatePath(
  destination: MapPoint,
  offsets: Array<[number, number]>,
): MapPoint[] {
  const latitudeDelta = destination.latitude - origin.latitude
  const longitudeDelta = destination.longitude - origin.longitude

  return [
    origin,
    ...offsets.map(([fraction, longitudeOffset], index) => ({
      latitude:
        origin.latitude +
        latitudeDelta * fraction +
        (index % 2 === 0 ? 0.00018 : -0.00014),
      longitude:
        origin.longitude + longitudeDelta * fraction + longitudeOffset,
    })),
    destination,
  ]
}

function segmentDistanceKm(start: MapPoint, end: MapPoint) {
  const earthRadiusKm = 6371
  const toRadians = (value: number) => (value * Math.PI) / 180
  const latitudeDelta = toRadians(end.latitude - start.latitude)
  const longitudeDelta = toRadians(end.longitude - start.longitude)
  const startLatitude = toRadians(start.latitude)
  const endLatitude = toRadians(end.latitude)
  const haversine =
    Math.sin(latitudeDelta / 2) ** 2 +
    Math.cos(startLatitude) *
      Math.cos(endLatitude) *
      Math.sin(longitudeDelta / 2) ** 2

  return earthRadiusKm * 2 * Math.atan2(Math.sqrt(haversine), Math.sqrt(1 - haversine))
}

function pathDistanceKm(path: MapPoint[]) {
  return path.slice(1).reduce(
    (distance, point, index) =>
      distance + segmentDistanceKm(path[index], point),
    0,
  )
}

export function getMockRouteMap(
  mode: MapRouteMode,
  event: EventDetails,
): MockRouteMap {
  const destination = {
    ...event.location,
    label: event.venueName,
  }
  const profile = routeProfiles[mode]
  const path = interpolatePath(destination, profile.offsets)
  const distanceKm = pathDistanceKm(path)

  return {
    mode,
    origin,
    destination,
    path,
    distanceKm: Number(distanceKm.toFixed(1)),
    durationMinutes: Math.max(3, Math.round((distanceKm / profile.speedKmh) * 60)),
  }
}
