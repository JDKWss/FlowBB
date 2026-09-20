import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import { MapPin, TriangleAlert } from 'lucide-react'
import Map, {
  Layer,
  Marker,
  Popup,
  Source,
  type LayerProps,
  type MapRef,
  type MapLayerMouseEvent,
} from 'react-map-gl/maplibre'
import type {
  EventSummary,
  HexagonFeatureCollection,
} from '../types/contracts'
import { MapErrorBoundary, MapUnavailable } from './MapFallback'

const OPEN_FREE_MAP_STYLE = 'https://tiles.openfreemap.org/styles/liberty'
const HEX_FILL_ID = 'flowbb-demand-fill'

const hexFillLayer: LayerProps = {
  id: HEX_FILL_ID,
  type: 'fill',
  paint: {
    'fill-color': [
      'interpolate',
      ['linear'],
      ['get', 'participants'],
      10,
      '#215845',
      40,
      '#47b98b',
      85,
      '#a7f3d0',
    ],
    'fill-opacity': 0.66,
  },
}

const hexLineLayer: LayerProps = {
  id: 'flowbb-demand-outline',
  type: 'line',
  paint: {
    'line-color': '#d1fae5',
    'line-opacity': 0.75,
    'line-width': 1.5,
  },
}

type Aggregate = {
  longitude: number
  latitude: number
  participants: number
  publicTransport: number
  walking: number
  bike: number
  car: number
}

export function PulseMap({
  event,
  hexagons,
  isLoading,
  error,
}: {
  event: EventSummary
  hexagons?: HexagonFeatureCollection
  isLoading: boolean
  error: string | null
}) {
  const mapRef = useRef<MapRef>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const loadedRef = useRef(false)
  const [loaded, setLoaded] = useState(false)
  const [mapError, setMapError] = useState(false)
  const [aggregate, setAggregate] = useState<Aggregate | null>(null)
  const [showEvent, setShowEvent] = useState(false)

  const synchronizeViewport = useCallback(() => {
    const map = mapRef.current
    const container = containerRef.current
    if (!map || !container) return
    const { width, height } = container.getBoundingClientRect()
    if (width <= 0 || height <= 0) return
    map.resize()
  }, [])

  useEffect(() => {
    const container = containerRef.current
    if (!container) return
    const observer = new ResizeObserver(synchronizeViewport)
    observer.observe(container)
    synchronizeViewport()
    return () => observer.disconnect()
  }, [synchronizeViewport])

  useEffect(() => {
    mapRef.current?.flyTo({
      center: [event.location.longitude, event.location.latitude],
      zoom: 13.2,
      duration: 450,
    })
  }, [event])

  const featureCollection = useMemo(
    () => hexagons ?? { type: 'FeatureCollection' as const, features: [] },
    [hexagons],
  )

  const inspectAggregate = (mapEvent: MapLayerMouseEvent) => {
    const feature = mapEvent.features?.[0]
    if (!feature) {
      setAggregate(null)
      return
    }
    const properties = feature.properties as Record<string, unknown>
    setAggregate({
      longitude: mapEvent.lngLat.lng,
      latitude: mapEvent.lngLat.lat,
      participants: Number(properties.participants ?? 0),
      publicTransport: Number(properties.publicTransport ?? 0),
      walking: Number(properties.walking ?? 0),
      bike: Number(properties.bike ?? 0),
      car: Number(properties.car ?? 0),
    })
  }

  if (mapError) return <MapUnavailable />

  return (
    <MapErrorBoundary>
      <div ref={containerRef} className="pulse-map" data-testid="pulse-map">
        {!loaded && <div className="map-loading">Loading demand map…</div>}
        <Map
          ref={mapRef}
          initialViewState={{
            longitude: event.location.longitude,
            latitude: event.location.latitude,
            zoom: 13.2,
          }}
          mapStyle={OPEN_FREE_MAP_STYLE}
          dragRotate={false}
          touchPitch={false}
          maxPitch={0}
          attributionControl={{ compact: true }}
          interactiveLayerIds={[HEX_FILL_ID]}
          onClick={inspectAggregate}
          onLoad={() => {
            loadedRef.current = true
            setLoaded(true)
            synchronizeViewport()
          }}
          onError={(mapEvent) => {
            if (!loadedRef.current && !mapEvent.target.getStyle()?.layers?.length) {
              setMapError(true)
            }
          }}
          style={{ width: '100%', height: '100%' }}
        >
          <Source id="flowbb-demand" type="geojson" data={featureCollection}>
            <Layer {...hexFillLayer} />
            <Layer {...hexLineLayer} />
          </Source>

          <Marker
            longitude={event.location.longitude}
            latitude={event.location.latitude}
            anchor="bottom"
          >
            <button
              type="button"
              className="event-marker"
              aria-label={`Event location: ${event.name}`}
              onClick={markerEvent => {
                markerEvent.stopPropagation()
                setShowEvent(true)
              }}
            >
              <MapPin aria-hidden="true" size={22} />
            </button>
          </Marker>

          {showEvent && (
            <Popup
              className="flowbb-map-popup"
              longitude={event.location.longitude}
              latitude={event.location.latitude}
              anchor="bottom"
              offset={38}
              closeOnClick={false}
              onClose={() => setShowEvent(false)}
            >
              <strong>{event.name}</strong>
              <small>{event.venueName}</small>
              <small>{event.participantsCount} participants</small>
            </Popup>
          )}

          {aggregate && (
            <Popup
              className="flowbb-map-popup"
              longitude={aggregate.longitude}
              latitude={aggregate.latitude}
              closeOnClick={false}
              onClose={() => setAggregate(null)}
            >
              <strong>{aggregate.participants} participants</strong>
              <small>Public transport {aggregate.publicTransport}</small>
              <small>Walking {aggregate.walking} · Bike {aggregate.bike}</small>
              <small>Car {aggregate.car}</small>
            </Popup>
          )}
        </Map>

        {isLoading && <div className="map-overlay map-overlay--status">Refreshing cells…</div>}
        {error && (
          <div className="map-overlay map-overlay--error">
            <TriangleAlert aria-hidden="true" size={15} />
            Demand layer unavailable
          </div>
        )}
        {!isLoading && !error && featureCollection.features.length === 0 && (
          <div className="map-overlay map-overlay--status">
            No cells meet the privacy threshold.
          </div>
        )}
      </div>
    </MapErrorBoundary>
  )
}
