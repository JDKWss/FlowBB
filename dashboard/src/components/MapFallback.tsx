import { Component, type ErrorInfo, type ReactNode } from 'react'
import { MapPinned, TriangleAlert } from 'lucide-react'

export function MapUnavailable({ detail = 'Map tiles or WebGL are unavailable.' }) {
  return (
    <div className="map-fallback" role="status">
      <span className="icon-tile icon-tile--muted">
        <MapPinned aria-hidden="true" size={22} />
      </span>
      <div>
        <strong>Map unavailable</strong>
        <p>{detail} The rest of the dashboard remains available.</p>
      </div>
      <TriangleAlert aria-hidden="true" size={18} />
    </div>
  )
}

type State = { failed: boolean }

export class MapErrorBoundary extends Component<
  { children: ReactNode; fallback?: ReactNode },
  State
> {
  state: State = { failed: false }

  static getDerivedStateFromError(): State {
    return { failed: true }
  }

  componentDidCatch(_error: Error, _info: ErrorInfo) {
    // The surrounding workflow intentionally remains usable without WebGL.
  }

  render() {
    if (this.state.failed) return this.props.fallback ?? <MapUnavailable />
    return this.props.children
  }
}
