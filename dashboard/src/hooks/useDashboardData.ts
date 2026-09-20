import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { dashboardService } from '../services/dashboardService'
import type { CreateEventRequest } from '../types/contracts'

export const dashboardKeys = {
  events: ['events'] as const,
  airQuality: (eventId: string) => ['events', eventId, 'air-quality'] as const,
  pulseSummary: ['pulse', 'summary'] as const,
  eventPulse: (eventId: string) => ['pulse', 'event', eventId] as const,
  hexagons: (eventId: string) => ['pulse', 'hexagons', eventId] as const,
}

export function useEvents() {
  return useQuery({
    queryKey: dashboardKeys.events,
    queryFn: dashboardService.getEvents,
  })
}

export function usePulseSummary() {
  return useQuery({
    queryKey: dashboardKeys.pulseSummary,
    queryFn: dashboardService.getPulseSummary,
  })
}

export function useAirQuality(eventId: string | null) {
  return useQuery({
    queryKey: dashboardKeys.airQuality(eventId ?? 'none'),
    queryFn: () => dashboardService.getAirQuality(eventId!),
    enabled: Boolean(eventId),
    retry: 1,
  })
}

export function useEventPulse(eventId: string | null) {
  return useQuery({
    queryKey: dashboardKeys.eventPulse(eventId ?? 'none'),
    queryFn: () => dashboardService.getEventPulse(eventId!),
    enabled: Boolean(eventId),
  })
}

export function usePulseHexagons(eventId: string | null) {
  return useQuery({
    queryKey: dashboardKeys.hexagons(eventId ?? 'none'),
    queryFn: () => dashboardService.getPulseHexagons(eventId!),
    enabled: Boolean(eventId),
  })
}

export function useCreateEvent() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: CreateEventRequest) => dashboardService.createEvent(request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: dashboardKeys.events })
      await queryClient.invalidateQueries({ queryKey: dashboardKeys.pulseSummary })
    },
  })
}
