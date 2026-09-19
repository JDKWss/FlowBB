import type { EventSummary, TransportMode } from '../../types/contracts'

type EventCategory = EventSummary['category']

const categoryLabels: Record<EventCategory, string> = {
  Culture: 'Culture',
  Sport: 'Sport',
  Education: 'Education',
  Community: 'Community',
  Other: 'Other',
}

export const transportLabels: Record<TransportMode, string> = {
  Walking: 'Walking',
  PublicTransport: 'Public transport',
  Bike: 'Bike',
  Car: 'Car',
  Unknown: 'Other',
}

export function getCategoryLabel(category: EventCategory): string {
  return categoryLabels[category] ?? category
}

export function formatEventDate(value: string): string {
  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat('en-GB', {
    weekday: 'short',
    timeZone: 'Europe/Warsaw',
    day: 'numeric',
    month: 'short',
  }).format(date)
}

export function formatEventDateLong(value: string): string {
  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat('en-GB', {
    weekday: 'long',
    timeZone: 'Europe/Warsaw',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(date)
}

export function formatEventTime(value: string): string {
  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    timeZone: 'Europe/Warsaw',
    minute: '2-digit',
  }).format(date)
}

export function formatEventTimeRange(startAt: string, endAt?: string | null): string {
  const start = formatEventTime(startAt)
  return endAt ? `${start}–${formatEventTime(endAt)}` : start
}
