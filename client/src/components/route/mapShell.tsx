import { Component, type ErrorInfo, type ReactNode } from 'react'
import { TriangleAlert } from 'lucide-react'
import 'maplibre-gl/dist/maplibre-gl.css'
import { Alert, AlertDescription, AlertTitle } from '../ui'

// Wspolna powloka map trasy: ten sam styl podkladu i ta sama granica bledu dla RouteMap i TransitStopsMap.
// Bez zaleznosci od RouteMap, zeby nie tworzyc importu cyklicznego.
export const OPEN_FREE_MAP_STYLE = 'https://tiles.openfreemap.org/styles/liberty'

type MapFallbackProps = {
  children: ReactNode
  fallback: ReactNode
}

type MapFallbackState = {
  failed: boolean
}

export class MapErrorBoundary extends Component<MapFallbackProps, MapFallbackState> {
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

export function MapUnavailable() {
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
