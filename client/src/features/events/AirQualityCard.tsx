import { RefreshCw, TriangleAlert, Wind } from 'lucide-react'
import { Button, Card } from '../../components/ui'
import type {
  AirQualityLevel,
  AirQualityMeasurement,
  AirQualityResponse,
  AirQualityStatus,
} from '../../types/contracts'

const levelLabels: Record<AirQualityLevel, string> = {
  VeryGood: 'Very good',
  Good: 'Good',
  Moderate: 'Moderate',
  Sufficient: 'Sufficient',
  Bad: 'Bad',
  VeryBad: 'Very bad',
  Unknown: 'Unknown',
}

const statusLabels: Record<AirQualityStatus, string> = {
  Fresh: 'Current measurement',
  Stale: 'Older measurement',
  Fallback: 'Demo fallback',
}

const formatMeasurement = (measurement: AirQualityMeasurement | null) =>
  measurement ? `${measurement.value.toLocaleString('en-GB')} ${measurement.unit}` : '—'

const formatMeasuredAt = (value: string) =>
  new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Europe/Warsaw',
  }).format(new Date(value))

export function AirQualityCard({
  airQuality,
  isLoading,
  error,
  onRetry,
}: {
  airQuality?: AirQualityResponse
  isLoading: boolean
  error: string | null
  onRetry: () => void
}) {
  if (isLoading) {
    return (
      <Card className="p-5" aria-busy="true">
        <p className="flex items-center gap-3 text-sm text-zinc-300">
          <RefreshCw aria-hidden="true" className="size-5 animate-spin text-primary" />
          Checking air quality…
        </p>
      </Card>
    )
  }

  if (error || !airQuality) {
    return (
      <Card className="p-5">
        <div className="flex items-start gap-3">
          <TriangleAlert aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-amber-300" />
          <div>
            <p className="font-semibold text-white">Air quality unavailable</p>
            <p className="mt-1 text-sm leading-6 text-zinc-400">
              {error ?? 'No measurement is available for this event.'}
            </p>
            <Button type="button" variant="secondary" size="sm" className="mt-3" onClick={onRetry}>
              Try again
            </Button>
          </div>
        </div>
      </Card>
    )
  }

  const source = airQuality.source === 'Gios'
    ? 'Źródło danych: GIOŚ - EKOINFONET'
    : 'Źródło: dane demonstracyjne FlowBB'

  return (
    <Card className="p-5">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <span className="grid size-11 shrink-0 place-items-center rounded-2xl bg-primary text-primary-foreground">
            <Wind aria-hidden="true" className="size-5" />
          </span>
          <div>
            <h2 className="font-semibold text-white">Air quality</h2>
            <p className="mt-1 text-sm text-zinc-400">{airQuality.station.name}</p>
          </div>
        </div>
        <span className="rounded-full bg-white/[0.07] px-3 py-1 text-xs text-zinc-200">
          {levelLabels[airQuality.qualityLevel]}
        </span>
      </div>

      <dl className="mt-5 grid grid-cols-2 gap-2">
        {([
          ['PM10', airQuality.pm10],
          ['PM2.5', airQuality.pm25],
          ['NO₂', airQuality.no2],
          ['O₃', airQuality.o3],
        ] as const).map(([label, measurement]) => (
          <div key={label} className="rounded-2xl bg-white/[0.04] p-3">
            <dt className="text-xs text-zinc-500">{label}</dt>
            <dd className="mt-1 text-sm font-semibold text-zinc-100">
              {formatMeasurement(measurement)}
            </dd>
          </div>
        ))}
      </dl>

      {airQuality.alert ? (
        <p className="mt-4 flex gap-2 rounded-2xl bg-amber-400/10 p-3 text-sm leading-6 text-amber-100">
          <TriangleAlert aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          {airQuality.alert.message}
        </p>
      ) : null}

      <div className="mt-4 text-xs leading-5 text-zinc-500">
        <p>{statusLabels[airQuality.status]} · {formatMeasuredAt(airQuality.measuredAt)}</p>
        <p>{source}</p>
      </div>
    </Card>
  )
}
