import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { DEMO_USER_ID } from '../mocks/data'
import { clientService } from '../services/clientService'
import type {
  AttendanceResponse,
  EventDetails,
  EventSummary,
  GroupSummary,
  TransportMode,
} from '../types/contracts'

const queryKeys = {
  events: ['events'] as const,
  event: (eventId: string) => ['events', eventId] as const,
  airQuality: (eventId: string) => ['events', eventId, 'air-quality'] as const,
  route: (eventId: string) => ['events', eventId, 'route', DEMO_USER_ID] as const,
  groups: (eventId: string) => ['events', eventId, 'groups', DEMO_USER_ID] as const,
}

export const useEvents = () =>
  useQuery({
    queryKey: queryKeys.events,
    queryFn: () => clientService.getEvents(),
  })

export const useEvent = (eventId: string | null) =>
  useQuery({
    queryKey: queryKeys.event(eventId ?? 'none'),
    queryFn: () => clientService.getEvent(eventId!),
    enabled: Boolean(eventId),
  })

export const useAirQuality = (eventId: string | null, enabled: boolean) =>
  useQuery({
    queryKey: queryKeys.airQuality(eventId ?? 'none'),
    queryFn: () => clientService.getAirQuality(eventId!),
    enabled: Boolean(eventId) && enabled,
    retry: 1,
  })

export const useRoute = (eventId: string | null, enabled: boolean) =>
  useQuery({
    queryKey: queryKeys.route(eventId ?? 'none'),
    queryFn: () => clientService.getRoute(eventId!, DEMO_USER_ID),
    enabled: Boolean(eventId) && enabled,
  })

export const useGroups = (eventId: string | null, enabled: boolean) =>
  useQuery({
    queryKey: queryKeys.groups(eventId ?? 'none'),
    queryFn: () => clientService.getGroups(eventId!, DEMO_USER_ID),
    enabled: Boolean(eventId) && enabled,
  })

interface SaveAttendanceInput {
  eventId: string
  transportMode: TransportMode
}

export const useSaveAttendance = () => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ eventId, transportMode }: SaveAttendanceInput) =>
      clientService.saveAttendance(eventId, {
        userId: DEMO_USER_ID,
        transportMode,
      }),
    onSuccess: (attendance: AttendanceResponse) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.route(attendance.eventId) })
      queryClient.setQueryData<EventDetails>(
        queryKeys.event(attendance.eventId),
        (event) =>
          event
            ? { ...event, participantsCount: attendance.participantsCount }
            : event,
      )
      queryClient.setQueryData<EventSummary[]>(queryKeys.events, (items) =>
        items?.map((event) =>
          event.id === attendance.eventId
            ? { ...event, participantsCount: attendance.participantsCount }
            : event,
        ),
      )
    },
  })
}

export const useToggleGroup = (eventId: string) => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (group: GroupSummary) => {
      if (group.joinedByCurrentUser) {
        await clientService.leaveGroup(group.id, DEMO_USER_ID)
        return {
          ...group,
          currentMembers: Math.max(0, group.currentMembers - 1),
          joinedByCurrentUser: false,
        }
      }

      return clientService.joinGroup(group.id, { userId: DEMO_USER_ID })
    },
    onSuccess: (updatedGroup) => {
      queryClient.setQueryData<GroupSummary[]>(
        queryKeys.groups(eventId),
        (items) =>
          items?.map((group) =>
            group.id === updatedGroup.id ? updatedGroup : group,
          ),
      )
    },
  })
}
