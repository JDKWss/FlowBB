import { mkdir, rename, rm, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const DEFAULT_VALHALLA_BASE_URL = 'https://valhalla1.openstreetmap.de'
const MIN_GEOMETRY_POINTS = 20
const MAX_SNAP_DISTANCE_KM = 0.15

const goldenEvent = {
  eventId: '11111111-1111-1111-1111-111111111111',
  name: 'Koncert na Rynku',
  origin: {
    latitude: 49.81272,
    longitude: 19.03384,
    label: 'Your location',
  },
  destination: {
    latitude: 49.82245,
    longitude: 19.04431,
    label: 'Rynek w Bielsku-Białej',
  },
}

const routeProfiles = [
  { mode: 'Walking', costing: 'pedestrian', fileName: 'route-walking.json' },
  { mode: 'Bike', costing: 'bicycle', fileName: 'route-bike.json' },
  { mode: 'Car', costing: 'auto', fileName: 'route-car.json' },
]

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const outputDirectory = join(scriptDirectory, '../src/mocks/demo')
const valhallaBaseUrl = (
  process.env.VALHALLA_BASE_URL ?? DEFAULT_VALHALLA_BASE_URL
).replace(/\/$/, '')

function decodePolyline6(encodedShape) {
  const coordinates = []
  let latitude = 0
  let longitude = 0
  let index = 0

  const decodeValue = () => {
    let result = 0
    let shift = 0
    let byte

    do {
      if (index >= encodedShape.length) {
        throw new Error('Encoded route geometry ended unexpectedly')
      }

      byte = encodedShape.charCodeAt(index) - 63
      index += 1
      result |= (byte & 0x1f) << shift
      shift += 5
    } while (byte >= 0x20)

    return result & 1 ? ~(result >> 1) : result >> 1
  }

  while (index < encodedShape.length) {
    latitude += decodeValue()
    longitude += decodeValue()
    coordinates.push([
      Number((longitude / 1_000_000).toFixed(6)),
      Number((latitude / 1_000_000).toFixed(6)),
    ])
  }

  return coordinates
}

function distanceKm(first, second) {
  const earthRadiusKm = 6371
  const toRadians = (value) => (value * Math.PI) / 180
  const latitudeDelta = toRadians(second[1] - first[1])
  const longitudeDelta = toRadians(second[0] - first[0])
  const firstLatitude = toRadians(first[1])
  const secondLatitude = toRadians(second[1])
  const haversine =
    Math.sin(latitudeDelta / 2) ** 2 +
    Math.cos(firstLatitude) *
      Math.cos(secondLatitude) *
      Math.sin(longitudeDelta / 2) ** 2

  return earthRadiusKm * 2 * Math.atan2(Math.sqrt(haversine), Math.sqrt(1 - haversine))
}

function normalizeSteps(maneuvers, mode) {
  if (!Array.isArray(maneuvers) || maneuvers.length === 0) {
    throw new Error(`${mode}: Valhalla returned no maneuvers`)
  }

  return maneuvers.map((maneuver, index) => {
    if (
      typeof maneuver?.instruction !== 'string' ||
      typeof maneuver?.length !== 'number' ||
      typeof maneuver?.time !== 'number'
    ) {
      throw new Error(`${mode}: maneuver ${index} is incomplete`)
    }

    return {
      instruction: maneuver.instruction,
      distanceMeters: Math.round(maneuver.length * 1000),
      durationSeconds: Math.round(maneuver.time),
    }
  })
}

function normalizeRoute(response, mode) {
  const trip = response?.trip
  const leg = trip?.legs?.[0]
  const summary = trip?.summary ?? leg?.summary

  if (trip?.status !== 0 || typeof leg?.shape !== 'string') {
    throw new Error(`${mode}: Valhalla returned no usable route`)
  }
  if (typeof summary?.length !== 'number' || typeof summary?.time !== 'number') {
    throw new Error(`${mode}: Valhalla route summary is missing`)
  }

  const coordinates = decodePolyline6(leg.shape)
  if (coordinates.length < MIN_GEOMETRY_POINTS) {
    throw new Error(
      `${mode}: geometry has only ${coordinates.length} points; expected at least ${MIN_GEOMETRY_POINTS}`,
    )
  }

  const expectedOrigin = [goldenEvent.origin.longitude, goldenEvent.origin.latitude]
  const expectedDestination = [
    goldenEvent.destination.longitude,
    goldenEvent.destination.latitude,
  ]
  const originSnapDistance = distanceKm(coordinates[0], expectedOrigin)
  const destinationSnapDistance = distanceKm(
    coordinates[coordinates.length - 1],
    expectedDestination,
  )

  if (originSnapDistance > MAX_SNAP_DISTANCE_KM) {
    throw new Error(`${mode}: route starts ${originSnapDistance.toFixed(3)} km from the origin`)
  }
  if (destinationSnapDistance > MAX_SNAP_DISTANCE_KM) {
    throw new Error(
      `${mode}: route ends ${destinationSnapDistance.toFixed(3)} km from the event`,
    )
  }

  return {
    eventId: goldenEvent.eventId,
    mode,
    origin: goldenEvent.origin,
    destination: goldenEvent.destination,
    distanceKm: Number(summary.length.toFixed(2)),
    durationMinutes: Math.max(1, Math.round(summary.time / 60)),
    geometry: {
      type: 'LineString',
      coordinates,
    },
    steps: normalizeSteps(leg.maneuvers, mode),
  }
}

async function requestRoute(profile) {
  const response = await fetch(`${valhallaBaseUrl}/route`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Client-Id': 'flowbb-hackbb-demo-route-generator',
    },
    body: JSON.stringify({
      locations: [
        { lat: goldenEvent.origin.latitude, lon: goldenEvent.origin.longitude },
        {
          lat: goldenEvent.destination.latitude,
          lon: goldenEvent.destination.longitude,
        },
      ],
      costing: profile.costing,
      units: 'kilometers',
      directions_options: { units: 'kilometers', language: 'en-US' },
    }),
    signal: AbortSignal.timeout(30_000),
  })

  const responseBody = await response.text()
  if (!response.ok) {
    throw new Error(
      `${profile.mode}: Valhalla HTTP ${response.status}: ${responseBody.slice(0, 300)}`,
    )
  }

  try {
    return normalizeRoute(JSON.parse(responseBody), profile.mode)
  } catch (error) {
    if (error instanceof SyntaxError) {
      throw new Error(`${profile.mode}: Valhalla returned invalid JSON`, { cause: error })
    }
    throw error
  }
}

function validateDistinctRoutes(routes) {
  const geometries = routes.map((route) => JSON.stringify(route.geometry.coordinates))
  if (new Set(geometries).size !== routes.length) {
    throw new Error('Valhalla returned identical geometry for two or more transport modes')
  }
}

async function saveFixtures(routes) {
  const temporaryDirectory = join(outputDirectory, `.generated-${process.pid}`)
  const files = [
    ['golden-event.json', goldenEvent],
    ...routes.map((route, index) => [routeProfiles[index].fileName, route]),
  ]

  await mkdir(temporaryDirectory, { recursive: true })
  try {
    await Promise.all(
      files.map(([fileName, contents]) =>
        writeFile(
          join(temporaryDirectory, fileName),
          `${JSON.stringify(contents, null, 2)}\n`,
          'utf8',
        ),
      ),
    )

    for (const [fileName] of files) {
      await rename(
        join(temporaryDirectory, fileName),
        join(outputDirectory, fileName),
      )
    }
  } finally {
    await rm(temporaryDirectory, { recursive: true, force: true })
  }
}

async function main() {
  console.log(`Generating golden demo routes with ${valhallaBaseUrl}`)
  const routes = []
  for (const profile of routeProfiles) {
    const route = await requestRoute(profile)
    routes.push(route)
    console.log(
      `${route.mode}: ${route.distanceKm.toFixed(2)} km, ${route.durationMinutes} min, ${route.geometry.coordinates.length} points`,
    )
  }

  validateDistinctRoutes(routes)
  await saveFixtures(routes)
  console.log(`Saved ${routes.length} route fixtures to ${outputDirectory}`)
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error)
  process.exitCode = 1
})
