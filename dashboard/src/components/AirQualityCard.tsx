import {
  IconAlertTriangle,
  IconCircleCheck,
  IconClock,
  IconCloudFog,
  IconDatabase,
  IconInfoCircle,
  IconMapPin,
  IconMist,
  IconRefresh,
  IconWind,
} from '@tabler/icons-react'
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

const levelHelpers: Record<AirQuality['qualityLevel'], string> = {
  VeryGood: 'Excellent air conditions.',
  Good: 'Fresh air for outdoor activity.',
  Moderate: 'Air is acceptable for most people.',
  Sufficient: 'Conditions are fair, but not ideal.',
  Bad: 'Poor air quality. Limit longer outdoor activity.',
  VeryBad: 'Very poor air quality. Avoid outdoor exertion.',
  Unknown: 'Air quality data is currently unavailable.',
}

const levelVisuals = {
  VeryGood: { icon: IconWind, tone: 'good' },
  Good: { icon: IconCircleCheck, tone: 'good' },
  Moderate: { icon: IconMist, tone: 'moderate' },
  Sufficient: { icon: IconCloudFog, tone: 'moderate' },
  Bad: { icon: IconAlertTriangle, tone: 'bad' },
  VeryBad: { icon: IconAlertTriangle, tone: 'very-bad' },
  Unknown: { icon: IconInfoCircle, tone: 'unknown' },
} satisfies Record<AirQuality['qualityLevel'], {
  icon: typeof IconWind
  tone: string
}>

const statusLabels: Record<AirQuality['status'], string> = {
  Fresh: 'Current measurement',
  Stale: 'Older measurement',
  Fallback: 'Fallback measurement',
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
        <IconRefresh aria-hidden="true" className="spin" size={19} />
        <span>Checking air quality</span>
      </article>
    )
  }

  if (error || !data) {
    return (
      <article className="card air-quality-card air-quality-state" role="status">
        <IconAlertTriangle aria-hidden="true" size={19} />
        <div>
          <strong>Air quality unavailable</strong>
          <p>{error ?? 'No measurement is available for this event.'}</p>
          <button type="button" className="button button--secondary" onClick={onRetry}>Try again</button>
        </div>
      </article>
    )
  }

  const source = data.source === 'Gios'
    ? 'GIOŚ / EKOINFONET'
    : 'FlowBB fallback snapshot'
  const visual = levelVisuals[data.qualityLevel]
  const QualityIcon = visual.icon

  return (
    <article className={`card air-quality-card air-quality-card--${visual.tone}`}>
      <header className="air-quality-heading">
        <h2>Air quality</h2>
      </header>

      <section className="air-quality-summary">
        <span className="air-quality-icon">
          <QualityIcon aria-hidden="true" size={34} stroke={1.7} />
        </span>
        <div>
          <strong>{levelLabels[data.qualityLevel]}</strong>
          <p>{levelHelpers[data.qualityLevel]}</p>
        </div>
      </section>

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
          <IconAlertTriangle aria-hidden="true" size={16} />
          {data.alert.message}
        </p>
      ) : null}

      <footer>
        <span><IconMapPin aria-hidden="true" size={14} />{data.station.name}</span>
        <span><IconClock aria-hidden="true" size={14} />{statusLabels[data.status]} · {measuredAt(data.measuredAt)}</span>
        <span><IconDatabase aria-hidden="true" size={14} />{source}</span>
      </footer>
    </article>
  )
}
