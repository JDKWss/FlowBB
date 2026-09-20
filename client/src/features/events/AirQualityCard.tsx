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

const levelHelpers: Record<AirQualityLevel, string> = {
  VeryGood: 'Excellent air conditions.',
  Good: 'Fresh air for outdoor activity.',
  Moderate: 'Air is acceptable for most people.',
  Sufficient: 'Conditions are fair, but not ideal.',
  Bad: 'Poor air quality. Limit longer outdoor activity.',
  VeryBad: 'Very poor air quality. Avoid outdoor exertion.',
  Unknown: 'Air quality data is currently unavailable.',
}

const levelVisuals = {
  VeryGood: {
    icon: IconWind,
    accent: 'text-emerald-200',
    iconClass: 'bg-emerald-300/12 text-emerald-200',
    gradient: 'rgb(110 231 183 / .12)',
  },
  Good: {
    icon: IconCircleCheck,
    accent: 'text-emerald-200',
    iconClass: 'bg-emerald-300/12 text-emerald-200',
    gradient: 'rgb(110 231 183 / .12)',
  },
  Moderate: {
    icon: IconMist,
    accent: 'text-amber-200',
    iconClass: 'bg-amber-300/12 text-amber-200',
    gradient: 'rgb(252 211 77 / .12)',
  },
  Sufficient: {
    icon: IconCloudFog,
    accent: 'text-amber-200',
    iconClass: 'bg-amber-300/12 text-amber-200',
    gradient: 'rgb(252 211 77 / .12)',
  },
  Bad: {
    icon: IconAlertTriangle,
    accent: 'text-orange-200',
    iconClass: 'bg-orange-400/12 text-orange-200',
    gradient: 'rgb(251 146 60 / .13)',
  },
  VeryBad: {
    icon: IconAlertTriangle,
    accent: 'text-red-200',
    iconClass: 'bg-red-400/12 text-red-200',
    gradient: 'rgb(248 113 113 / .14)',
  },
  Unknown: {
    icon: IconInfoCircle,
    accent: 'text-zinc-300',
    iconClass: 'bg-white/[0.06] text-zinc-300',
    gradient: 'rgb(163 163 163 / .1)',
  },
} satisfies Record<AirQualityLevel, {
  icon: typeof IconWind
  accent: string
  iconClass: string
  gradient: string
}>

const statusLabels: Record<AirQualityStatus, string> = {
  Fresh: 'Current measurement',
  Stale: 'Older measurement',
  Fallback: 'Fallback measurement',
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
          <IconRefresh aria-hidden="true" className="size-5 animate-spin text-primary" />
          Checking air quality…
        </p>
      </Card>
    )
  }

  if (error || !airQuality) {
    return (
      <Card className="p-5">
        <div className="flex items-start gap-3">
          <IconAlertTriangle aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-amber-300" />
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
    ? 'GIOŚ / EKOINFONET'
    : 'FlowBB fallback snapshot'
  const visual = levelVisuals[airQuality.qualityLevel]
  const QualityIcon = visual.icon

  return (
    <Card
      className="p-5"
      style={{
        background: `radial-gradient(circle at 92% 4%, ${visual.gradient}, transparent 52%), var(--card)`,
      }}
    >
      <h2 className="text-base font-semibold text-white">Air quality</h2>

      <div className="mt-[15px] flex items-center gap-[13px]">
        <span className={`grid size-[54px] shrink-0 place-items-center rounded-2xl ${visual.iconClass}`}>
          <QualityIcon aria-hidden="true" className="size-8" stroke={1.7} />
        </span>
        <div className="min-w-0">
          <p className={`text-base font-semibold ${visual.accent}`}>
            {levelLabels[airQuality.qualityLevel]}
          </p>
          <p className="mt-1 text-xs leading-[1.45] text-zinc-300">
            {levelHelpers[airQuality.qualityLevel]}
          </p>
        </div>
      </div>

      <dl className="mt-4 grid grid-cols-2 gap-[7px]">
        {([
          ['PM10', airQuality.pm10],
          ['PM2.5', airQuality.pm25],
          ['NO₂', airQuality.no2],
          ['O₃', airQuality.o3],
        ] as const).map(([label, measurement]) => (
          <div key={label} className="min-w-0 rounded-xl bg-white/[0.04] p-2.5">
            <dt className="text-[.65rem] text-zinc-500">{label}</dt>
            <dd className="mt-1 truncate text-xs font-semibold text-zinc-100" title={formatMeasurement(measurement)}>
              {formatMeasurement(measurement)}
            </dd>
          </div>
        ))}
      </dl>

      {airQuality.alert ? (
        <p className="mt-4 flex gap-2 rounded-2xl bg-amber-400/10 p-3 text-sm leading-6 text-amber-100">
          <IconAlertTriangle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          {airQuality.alert.message}
        </p>
      ) : null}

      <div className="mt-[13px] grid gap-[3px] text-[.65rem] leading-[1.4] text-zinc-500">
        <p className="flex items-start gap-2">
          <IconMapPin aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          <span>{airQuality.station.name}</span>
        </p>
        <p className="flex items-center gap-2">
          <IconClock aria-hidden="true" className="size-4 shrink-0" />
          <span>{statusLabels[airQuality.status]} · {formatMeasuredAt(airQuality.measuredAt)}</span>
        </p>
        <p className="flex items-center gap-2">
          <IconDatabase aria-hidden="true" className="size-4 shrink-0" />
          <span>{source}</span>
        </p>
      </div>
    </Card>
  )
}
