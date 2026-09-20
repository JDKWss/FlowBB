import { RefreshCw, TriangleAlert, Wind } from 'lucide-react'
import type { AirQuality } from '../types/contracts'

const levelLabels: Record<AirQuality['qualityLevel'], string> = {
  VeryGood: 'Very good',
  Good: 'Good',
  Moderate: 'Moderate',
  Sufficient: 'Sufficient',
  Bad: 'Bad',
  VeryBad: 'Very bad',
  Unknown: 'Unknown',
}

const statusLabels: Record<AirQuality['status'], string> = {
  Fresh: 'Current measurement',
  Stale: 'Older measurement',
  Fallback: 'Demo fallback',
}

const measuredAt = (value: string) => new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'Europe/Warsaw',
}).format(new Date(value))

export function AirQualityCard({
  data,
  isLoading,
  error,
  onRetry,
}: {
  data?: AirQuality
  isLoading: boolean
  error: string | null
  onRetry: () => void
}) {
  if (isLoading) {
    return (
      <article className="card air-quality-card air-quality-state" aria-busy="true">
        <RefreshCw aria-hidden="true" className="spin" size={19} />
        <span>Checking air quality</span>
      </article>
    )
  }

  if (error || !data) {
    return (
      <article className="card air-quality-card air-quality-state" role="status">
        <TriangleAlert aria-hidden="true" size={19} />
        <div>
          <strong>Air quality unavailable</strong>
          <p>{error ?? 'No measurement is available for this event.'}</p>
          <button type="button" className="button button--secondary" onClick={onRetry}>Try again</button>
        </div>
      </article>
    )
  }

  const source = data.source === 'Gios'
    ? 'Źródło danych: GIOŚ - EKOINFONET'
    : 'Źródło: dane demonstracyjne FlowBB'

  return (
    <article className="card air-quality-card">
      <header className="air-quality-heading">
        <span className="icon-tile"><Wind aria-hidden="true" size={19} /></span>
        <div>
          <h2>Air quality</h2>
          <p>{data.station.name}</p>
        </div>
        <strong>{levelLabels[data.qualityLevel]}</strong>
      </header>

      <dl className="air-quality-values">
        {([
          ['PM10', data.pm10],
          ['PM2.5', data.pm25],
          ['NO₂', data.no2],
          ['O₃', data.o3],
        ] as const).map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value ? `${value.value.toLocaleString('en-GB')} ${value.unit}` : '—'}</dd>
          </div>
        ))}
      </dl>

      {data.alert ? (
        <p className="air-quality-alert">
          <TriangleAlert aria-hidden="true" size={16} />
          {data.alert.message}
        </p>
      ) : null}

      <footer>
        <span>{statusLabels[data.status]} · {measuredAt(data.measuredAt)}</span>
        <span>{source}</span>
      </footer>
    </article>
  )
}
