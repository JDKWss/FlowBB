import {
  Component,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ErrorInfo,
  type ReactNode,
} from 'react'
import {
  Bike,
  CarFront,
  Flag,
  Footprints,
  MapPin,
  Maximize2,
  Minimize2,
  Navigation,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react'
import Map, {
  Layer,
  Marker,
  Popup,
  Source,
  type LayerProps,
  type MapRef,
} from 'react-map-gl/maplibre'
import 'maplibre-gl/dist/maplibre-gl.css'
import type { EventDetails, JourneyOption, RouteGeometry, TransportMode } from '../../types/contracts'
import { Alert, AlertDescription, AlertTitle, Badge, Skeleton } from '../ui'

type MapRouteMode = Extract<TransportMode, 'Walking' | 'Bike' | 'Car'>

type RenderedRoadRoute = {
  mode: MapRouteMode
  geometry: RouteGeometry
  durationMinutes: number
  distanceMeters: number
  origin: { longitude: number; latitude: number }
  destination: { longitude: number; latitude: number }
}

const OPEN_FREE_MAP_STYLE = 'https://tiles.openfreemap.org/styles/liberty'

const routeCasingLayer: LayerProps = {
  id: 'flowbb-route-casing',
  type: 'line',
  layout: {
    'line-cap': 'round',
    'line-join': 'round',
  },
  paint: {
    'line-color': '#101010',
    'line-width': 8,
    'line-opacity': 0.82,
  },
}

const routeLineLayer: LayerProps = {
  id: 'flowbb-route-line',
  type: 'line',
  layout: {
    'line-cap': 'round',
    'line-join': 'round',
  },
  paint: {
    'line-color': '#6ee7b7',
    'line-width': 5,
  },
}

const modeDetails: Record<
  MapRouteMode,
  { label: string; description: string; icon: LucideIcon }
> = {
  Walking: {
    label: 'Walking',
    description: 'Your walking route',
    icon: Footprints,
  },
  Bike: {
    label: 'Bike',
    description: 'Your bike route',
    icon: Bike,
  },
  Car: {
    label: 'Car',
    description: 'Your driving route',
    icon: CarFront,
  },
}

type MapFallbackProps = {
  children: ReactNode
  fallback: ReactNode
}

type MapFallbackState = {
  failed: boolean
}

class MapErrorBoundary extends Component<MapFallbackProps, MapFallbackState> {
  state: MapFallbackState = { failed: false }

  static getDerivedStateFromError(): MapFallbackState {
    return { failed: true }
  }

  componentDidCatch(_error: Error, _info: ErrorInfo) {
    // Route details remain usable when WebGL is unavailable.
  }

  render() {
    return this.state.failed ? this.props.fallback : this.props.children
  }
}

function MapUnavailable() {
  return (
    <Alert
      data-testid="route-map-unavailable"
      className="h-[240px] content-center bg-neutral-900 text-neutral-200"
    >
      <TriangleAlert aria-hidden="true" />
      <AlertTitle>Map unavailable</AlertTitle>
      <AlertDescription>
        Route details are still available below. You can continue to Crew.
      </AlertDescription>
    </Alert>
  )
}

function InteractiveMap({ route, event }: { route: RenderedRoadRoute; event: EventDetails }) {
  const mapRef = useRef<MapRef>(null)
  const mapContainerRef = useRef<HTMLDivElement>(null)
  const loadedRef = useRef(false)
  const [loaded, setLoaded] = useState(false)
  const [mapError, setMapError] = useState(false)
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [openPopup, setOpenPopup] = useState<'start' | 'destination' | null>(null)

  const routeGeoJson = useMemo(
    () => ({
      type: 'Feature' as const,
      properties: {},
      geometry: route.geometry,
    }),
    [route.geometry],
  )

  const fitRoute = useCallback((map: MapRef) => {
    const longitudes = route.geometry.coordinates.map(([longitude]) => longitude)
    const latitudes = route.geometry.coordinates.map(([, latitude]) => latitude)

    map.fitBounds(
      [
        [Math.min(...longitudes), Math.min(...latitudes)],
        [Math.max(...longitudes), Math.max(...latitudes)],
      ],
      {
        padding: { top: 42, right: 38, bottom: 42, left: 38 },
        duration: 0,
        maxZoom: 15,
      },
    )
  }, [route.geometry.coordinates])

  const synchronizeViewport = useCallback(() => {
    const container = mapContainerRef.current
    const map = mapRef.current
    if (!container || !map) return

    const { width, height } = container.getBoundingClientRect()
    if (width <= 0 || height <= 0) return

    map.resize()
    if (loadedRef.current) fitRoute(map)
  }, [fitRoute])

  useEffect(() => {
    const container = mapContainerRef.current
    if (!container) return

    const observer = new ResizeObserver(synchronizeViewport)
    observer.observe(container)
    synchronizeViewport()
    return () => observer.disconnect()
  }, [synchronizeViewport])

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(document.fullscreenElement === mapContainerRef.current)
    }

    document.addEventListener('fullscreenchange', handleFullscreenChange)
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange)
  }, [])

  const toggleFullscreen = async () => {
    const mapContainer = mapContainerRef.current
    if (!mapContainer) return

    try {
      if (document.fullscreenElement === mapContainer) {
        await document.exitFullscreen()
      } else if (!document.fullscreenElement) {
        await mapContainer.requestFullscreen()
      }
    } catch {
      setIsFullscreen(false)
    }
  }

  if (mapError) return <MapUnavailable />

  return (
    <div
      ref={mapContainerRef}
      data-testid="route-map"
      aria-label={`${modeDetails[route.mode].label} route map to ${event.name}`}
      className="flowbb-route-map relative h-[240px] overflow-hidden rounded-3xl bg-neutral-900"
    >
      {!loaded && (
        <Skeleton
          data-testid="route-map-loading"
          className="absolute inset-0 z-20 rounded-3xl"
        />
      )}
      <Map
        ref={mapRef}
        initialViewState={{
          longitude: route.origin.longitude,
          latitude: route.origin.latitude,
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
        <Source id="flowbb-route" type="geojson" data={routeGeoJson}>
          <Layer {...routeCasingLayer} />
          <Layer {...routeLineLayer} />
        </Source>

        <Marker
          longitude={route.origin.longitude}
          latitude={route.origin.latitude}
          anchor="center"
        >
          <button
            type="button"
            data-testid="route-start-marker"
            aria-label="Route start: Your location"
            onClick={(event) => {
              event.stopPropagation()
              setOpenPopup('start')
            }}
            className="grid size-8 place-items-center rounded-full border-2 border-neutral-700 bg-white text-black shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          >
            <Navigation aria-hidden="true" className="size-4 fill-black" />
          </button>
        </Marker>

        <Marker
          longitude={route.destination.longitude}
          latitude={route.destination.latitude}
          anchor="bottom"
        >
          <button
            type="button"
            data-testid="route-destination-marker"
            aria-label={`Destination: ${event.name}`}
            onClick={(markerEvent) => {
              markerEvent.stopPropagation()
              setOpenPopup('destination')
            }}
            className="grid size-10 place-items-center rounded-full bg-primary text-primary-foreground shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white"
          >
            <MapPin aria-hidden="true" className="size-5 fill-current" />
          </button>
        </Marker>

        {openPopup === 'start' && (
          <Popup
            longitude={route.origin.longitude}
            latitude={route.origin.latitude}
            anchor="bottom"
            offset={22}
            closeOnClick={false}
            className="flowbb-map-popup"
            onClose={() => setOpenPopup(null)}
          >
            <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-primary">
              Your location
            </p>
            <p className="mt-1 text-sm font-semibold text-white">
              Start of your {route.mode.toLowerCase()} route
            </p>
          </Popup>
        )}

        {openPopup === 'destination' && (
          <Popup
            longitude={route.destination.longitude}
            latitude={route.destination.latitude}
            anchor="bottom"
            offset={30}
            closeOnClick={false}
            className="flowbb-map-popup"
            onClose={() => setOpenPopup(null)}
          >
            <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-primary">
              Event
            </p>
            <p className="mt-1 text-sm font-bold text-white">{event.name}</p>
            <p className="mt-0.5 text-xs text-neutral-400">{event.venueName}</p>
          </Popup>
        )}
      </Map>

      <button
        type="button"
        data-testid="route-fullscreen-toggle"
        aria-label={isFullscreen ? 'Exit full screen' : 'Expand route map'}
        aria-pressed={isFullscreen}
        onClick={() => void toggleFullscreen()}
        className="absolute right-3 top-3 z-30 grid size-10 place-items-center rounded-full border border-white/15 bg-neutral-950/90 text-white shadow-lg backdrop-blur-sm transition hover:bg-neutral-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      >
        {isFullscreen ? (
          <Minimize2 aria-hidden="true" className="size-5" />
        ) : (
          <Maximize2 aria-hidden="true" className="size-5" />
        )}
      </button>
    </div>
  )
}

export function RouteMap({
  mode,
  event,
  journey,
}: {
  mode: MapRouteMode
  event: EventDetails
  journey: JourneyOption
}) {
  const route = useMemo<RenderedRoadRoute | null>(() => {
    if (!journey.geometry || journey.distanceMeters == null) return null
    const first = journey.geometry.coordinates[0]
    const last = journey.geometry.coordinates.at(-1)
    if (!first || !last) return null

    return {
      mode,
      geometry: journey.geometry,
      durationMinutes: journey.durationMinutes,
      distanceMeters: journey.distanceMeters,
      origin: { longitude: first[0], latitude: first[1] },
      destination: { longitude: last[0], latitude: last[1] },
    }
  }, [journey, mode])
  const ModeIcon = modeDetails[mode].icon

  if (!route) return null

  return (
    <section data-testid="route-map-section" className="mb-5" aria-labelledby="road-route-title">
      <MapErrorBoundary fallback={<MapUnavailable />}>
        <InteractiveMap route={route} event={event} />
      </MapErrorBoundary>

      <div className="px-1 pt-4">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <span className="grid size-10 place-items-center rounded-2xl bg-white/[0.06] text-primary">
              <ModeIcon aria-hidden="true" className="size-5" />
            </span>
            <div>
              <Badge
                id="road-route-title"
                variant="secondary"
                className="h-auto bg-white/[0.06] px-2 py-0.5 text-[10px] font-bold uppercase tracking-[0.14em] text-neutral-300"
              >
                Road route
              </Badge>
              <p className="mt-1 text-base font-bold text-white">
                {modeDetails[mode].label}
              </p>
            </div>
          </div>
          <p className="pt-1 text-right text-sm font-semibold text-white">
            {route.durationMinutes} min
            <span className="block text-xs font-normal text-neutral-400">
              {(route.distanceMeters / 1000).toFixed(1)} km
            </span>
          </p>
        </div>

        <div className="mt-4 grid grid-cols-[auto_1fr_auto] items-center gap-3 text-xs">
          <span className="max-w-24 text-neutral-300">Your location</span>
          <span aria-hidden="true" className="h-px bg-neutral-700" />
          <span className="max-w-28 text-right font-medium text-white">
            {event.name}
          </span>
        </div>
        <p className="mt-2 flex items-center gap-1.5 text-xs text-neutral-500">
          <Flag aria-hidden="true" className="size-3.5" />
          {event.venueName}
        </p>
      </div>
    </section>
  )
}
