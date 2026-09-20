import { useCallback, useEffect, useRef, useState } from 'react'
import { Crosshair, MapPin } from 'lucide-react'
import Map, {
  Marker,
  type MapLayerMouseEvent,
  type MapRef,
} from 'react-map-gl/maplibre'
import type { GeoPoint } from '../types/contracts'
import { MapErrorBoundary, MapUnavailable } from './MapFallback'

const OPEN_FREE_MAP_STYLE = 'https://tiles.openfreemap.org/styles/liberty'
const BIELSKO_BIALA = { latitude: 49.8224, longitude: 19.0443 }

export function EventLocationMap({
  value,
  onChange,
}: {
  value: GeoPoint | null
  onChange: (point: GeoPoint) => void
}) {
  const mapRef = useRef<MapRef>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const loadedRef = useRef(false)
  const [loaded, setLoaded] = useState(false)
  const [mapError, setMapError] = useState(false)

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

  const selectPoint = (event: MapLayerMouseEvent) => {
    onChange({
      longitude: event.lngLat.lng,
      latitude: event.lngLat.lat,
    })
  }

  if (mapError) {
    return <MapUnavailable detail="Location selection needs a working map." />
  }

  return (
    <MapErrorBoundary>
      <div
        ref={containerRef}
        className="event-location-map"
        data-testid="event-location-map"
      >
        {!loaded && <div className="map-loading">Loading Bielsko-Biała…</div>}
        <Map
          ref={mapRef}
          initialViewState={{ ...BIELSKO_BIALA, zoom: 13.4 }}
          mapStyle={OPEN_FREE_MAP_STYLE}
          dragRotate={false}
          touchPitch={false}
          maxPitch={0}
          attributionControl={{ compact: true }}
          cursor="crosshair"
          onClick={selectPoint}
          onLoad={() => {
            loadedRef.current = true
            setLoaded(true)
            synchronizeViewport()
          }}
          onError={event => {
            if (!loadedRef.current && !event.target.getStyle()?.layers?.length) {
              setMapError(true)
            }
          }}
          style={{ width: '100%', height: '100%' }}
        >
          {value && (
            <Marker
              longitude={value.longitude}
              latitude={value.latitude}
              anchor="bottom"
            >
              <span
                className="event-marker event-marker--selected"
                aria-label="Selected event location"
                data-testid="event-location-marker"
              >
                <MapPin aria-hidden="true" size={22} />
              </span>
            </Marker>
          )}
        </Map>

        <div className="map-overlay map-overlay--instruction">
          <Crosshair aria-hidden="true" size={16} />
          Click the map to place the event pin
        </div>
      </div>
    </MapErrorBoundary>
  )
}
