import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { BusFront, Footprints, MapPin } from 'lucide-react'
// Celowo bez Source i Layer: nie znamy przebiegu trasy autobusu, wiec nie rysujemy linii miedzy przystankami.
// Brak tych importow jest strukturalna gwarancja, ze mapa pokazuje wylacznie markery.
import Map, { Marker, type MapRef } from 'react-map-gl/maplibre'
import type { EventDetails, RouteStop } from '../../types/contracts'
import { Skeleton } from '../ui'
import { MapErrorBoundary, MapUnavailable, OPEN_FREE_MAP_STYLE } from './mapShell'

interface TransitStopsMapProps {
  event: EventDetails
  stops: RouteStop[]
}

// Dwa przystanki i miejsce wydarzenia moga lezec niemal w tym samym punkcie; bbox o zerowej rozciaglosci
// psuje fitBounds, wiec w takim przypadku centrujemy widok na sztywno.
const DEGENERATE_BOUNDS_DEGREES = 0.0005
// Dolny margines jest wiekszy, bo rozwiniete pole atrybucji mapy przykrywa ok. 45 px u dolu i chowalo marker wsiadania.
const FIT_PADDING = { top: 56, right: 38, bottom: 72, left: 38 }

const markerIcons = { board: BusFront, alight: Footprints, event: MapPin } as const
const toneClasses = {
  primary: 'bg-primary text-primary-foreground',
  neutral: 'bg-white text-neutral-950',
} as const

// Przystanek wysiadania lezy zwykle kilkaset metrow od wydarzenia, wiec ich etykiety nachodzily na siebie.
// Etykieta wydarzenia idzie pod pinezke, a etykiety przystankow nad nia.
function MarkerLabel({ name, testId, icon, tone, labelBelow = false }: {
  name: string
  testId: string
  icon: keyof typeof markerIcons
  tone: keyof typeof toneClasses
  labelBelow?: boolean
}) {
  const Icon = markerIcons[icon]
  const toneClass = toneClasses[tone]

  return (
    <span data-testid={testId} className={`flex items-center gap-1 ${labelBelow ? 'flex-col-reverse' : 'flex-col'}`}>
      <span className="max-w-32 truncate rounded-full bg-neutral-950/90 px-2 py-0.5 text-[10px] font-semibold text-white shadow-lg">
        {name}
      </span>
      <span className={`grid size-7 place-items-center rounded-full shadow-lg ${toneClass}`}>
        <Icon aria-hidden="true" className="size-3.5" />
      </span>
    </span>
  )
}

function InteractiveMap({ event, stops }: TransitStopsMapProps) {
  const mapRef = useRef<MapRef | null>(null)
  const containerRef = useRef<HTMLDivElement | null>(null)
  const loadedRef = useRef(false)
  const [loaded, setLoaded] = useState(false)
  const [mapError, setMapError] = useState(false)

  const boarding = stops[0]
  const alighting = stops[stops.length - 1]
  const points = useMemo(
    () => [...stops, { latitude: event.location.latitude, longitude: event.location.longitude }],
    [stops, event.location.latitude, event.location.longitude],
  )

  const fitStops = useCallback((map: MapRef) => {
    const west = Math.min(...points.map(point => point.longitude))
    const east = Math.max(...points.map(point => point.longitude))
    const south = Math.min(...points.map(point => point.latitude))
    const north = Math.max(...points.map(point => point.latitude))

    if (east - west < DEGENERATE_BOUNDS_DEGREES && north - south < DEGENERATE_BOUNDS_DEGREES) {
      map.jumpTo({ center: [west, south], zoom: 15 })
      return
    }

    map.fitBounds([[west, south], [east, north]], { padding: FIT_PADDING, duration: 0, maxZoom: 15 })
  }, [points])

  const synchronizeViewport = useCallback(() => {
    const container = containerRef.current
    const map = mapRef.current
    if (!container || !map) return

    const { width, height } = container.getBoundingClientRect()
    if (width <= 0 || height <= 0) return

    map.resize()
    if (loadedRef.current) fitStops(map)
  }, [fitStops])

  useEffect(() => {
    const container = containerRef.current
    if (!container) return undefined

    const observer = new ResizeObserver(synchronizeViewport)
    observer.observe(container)
    synchronizeViewport()
    return () => observer.disconnect()
  }, [synchronizeViewport])

  if (mapError) return <MapUnavailable />

  return (
    <div
      ref={containerRef}
      data-testid="transit-stops-map"
      aria-label={`Map of the bus stops for ${event.name}`}
      className="flowbb-route-map relative h-[240px] overflow-hidden rounded-3xl bg-neutral-900"
    >
      {!loaded && <Skeleton data-testid="transit-stops-map-loading" className="absolute inset-0 z-20 rounded-3xl" />}
      <Map
        ref={mapRef}
        initialViewState={{
          longitude: event.location.longitude,
          latitude: event.location.latitude,
          zoom: 13,
          bearing: 0,
          pitch: 0,
        }}
        mapStyle={OPEN_FREE_MAP_STYLE}
        onLoad={() => {
          loadedRef.current = true
          setLoaded(true)
          synchronizeViewport()
        }}
        onError={(error) => {
          const style = error.target.getStyle()
          if (!loadedRef.current && !style?.layers?.length) setMapError(true)
        }}
        dragRotate={false}
        touchPitch={false}
        maxPitch={0}
        attributionControl={{ compact: true }}
        style={{ width: '100%', height: '100%' }}
      >
        <Marker longitude={boarding.longitude} latitude={boarding.latitude} anchor="bottom">
          <MarkerLabel name={boarding.name} testId="transit-stop-board" icon="board" tone="primary" />
        </Marker>
        <Marker longitude={alighting.longitude} latitude={alighting.latitude} anchor="bottom">
          <MarkerLabel name={alighting.name} testId="transit-stop-alight" icon="alight" tone="primary" />
        </Marker>
        <Marker longitude={event.location.longitude} latitude={event.location.latitude} anchor="top">
          <MarkerLabel name={event.venueName} testId="transit-map-event" icon="event" tone="neutral" labelBelow />
        </Marker>
      </Map>
    </div>
  )
}

/**
 * Mapa przystankow odcinka komunikacji miejskiej: przystanek wsiadania, wysiadania i miejsce wydarzenia.
 * Nie przyjmuje punktu startu uzytkownika, wiec nie ma jak go pokazac (AGENTS.md, prywatnosc): to dana wewnetrzna backendu.
 */
export function TransitStopsMap({ event, stops }: TransitStopsMapProps) {
  if (stops.length < 2) return null

  const boarding = stops[0]
  const alighting = stops[stops.length - 1]

  return (
    <section data-testid="transit-stops-section" className="mb-5" aria-labelledby="transit-stops-title">
      <MapErrorBoundary fallback={<MapUnavailable />}>
        <InteractiveMap event={event} stops={stops} />
      </MapErrorBoundary>
      <div className="px-1 pt-4">
        <h2 id="transit-stops-title" className="text-sm font-bold text-white">Your bus stops</h2>
        <p className="mt-1 text-xs text-slate-400">
          Board at <span className="font-semibold text-slate-200">{boarding.name}</span>, get off at{' '}
          <span className="font-semibold text-slate-200">{alighting.name}</span>.
        </p>
      </div>
    </section>
  )
}
