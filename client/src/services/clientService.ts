import { eventDetails, events, groups, routes } from '../mocks/data'
import { attendanceRequestSchema, attendanceSchema, eventSummarySchema, eventDetailsSchema, groupSchema, routeSchema } from '../types/validation'
import type {
  AttendanceResponse,
  AttendanceUpsertRequest,
  EventDetails,
  EventSummary,
  GroupMembershipRequest,
  GroupSummary,
  RouteResponse,
  TransportMode,
} from '../types/contracts'

export interface ClientService {
  getEvents(): Promise<EventSummary[]>
  getEvent(eventId: string): Promise<EventDetails>
  saveAttendance(
    eventId: string,
    request: AttendanceUpsertRequest,
  ): Promise<AttendanceResponse>
  getRoute(eventId: string, userId: string): Promise<RouteResponse>
  getGroups(eventId: string, userId: string): Promise<GroupSummary[]>
  joinGroup(groupId: string, request: GroupMembershipRequest): Promise<GroupSummary>
  leaveGroup(groupId: string, userId: string): Promise<void>
}

const wait = (milliseconds = 220) =>
  new Promise<void>((resolve) => window.setTimeout(resolve, milliseconds))

const clone = <T,>(value: T): T => structuredClone(value)

class MockClientService implements ClientService {
  private readonly attendanceEvents = new Set<string>()
  private readonly transportByAttendance = new Map<string, TransportMode>()
  private readonly mutableEvents = clone(events)
  private readonly mutableDetails = clone(eventDetails)
  private readonly mutableGroups = clone(groups)

  async getEvents() {
    await wait()
    return eventSummarySchema.array().parse(clone(this.mutableEvents))
  }

  async getEvent(eventId: string) {
    await wait()
    const event = this.mutableDetails.find((item) => item.id === eventId)
    if (!event) throw new Error('Event not found.')
    return eventDetailsSchema.parse(clone(event))
  }

  async saveAttendance(eventId: string, request: AttendanceUpsertRequest) {
    await wait(360)
    attendanceRequestSchema.parse(request)
    const event = this.mutableDetails.find((item) => item.id === eventId)
    const summary = this.mutableEvents.find((item) => item.id === eventId)
    if (!event || !summary) throw new Error('Event not found.')
    if (!event.availableTransportModes.includes(request.transportMode)) throw new Error('Choose an available transport mode.')

    const attendanceKey = `${eventId}:${request.userId}`
    const isNew = !this.attendanceEvents.has(attendanceKey)
    this.transportByAttendance.set(attendanceKey, request.transportMode)
    if (isNew) {
      this.attendanceEvents.add(attendanceKey)
      event.participantsCount += 1
      summary.participantsCount += 1
    }

    return attendanceSchema.parse({
      eventId,
      userId: request.userId,
      transportMode: request.transportMode,
      participantsCount: event.participantsCount,
      isNew,
      updatedAt: new Date().toISOString(),
    })
  }

  async getRoute(eventId: string, userId: string) {
    await wait()
    const route = routes[eventId]
    if (!route) throw new Error('Route not found.')
    const result = clone({ ...route, userId })
    const mode = this.transportByAttendance.get(`${eventId}:${userId}`)
    if (mode === 'Walking' || mode === 'Bike' || mode === 'Car') {
      const type = mode === 'Walking' ? 'Walk' : mode
      const instruction = mode === 'Walking' ? 'Walk along the demonstration route.' : mode === 'Bike' ? 'Cycle along the demonstration route.' : 'Drive along the demonstration route.'
      for (const journey of [result.outbound, ...result.returns]) {
        journey.steps = [{ type, instruction, durationMinutes: journey.durationMinutes }]
      }
      // The late-night gap fixture models missing public transport, not other modes.
      if (result.returnGap) {
        const end = this.mutableDetails.find(event => event.id === eventId)?.endAt
        if (end) {
          const departure = new Date(end).getTime() + 10 * 60_000
          result.returns = [{ ...clone(result.outbound), departureAt: new Date(departure).toISOString(), arrivalAt: new Date(departure + result.outbound.durationMinutes * 60_000).toISOString() }]
          result.returnGap = false
        }
      }
    }
    return routeSchema.parse(result)
  }

  async getGroups(eventId: string, _userId: string) {
    await wait()
    return groupSchema.array().parse(clone(this.mutableGroups.filter((group) => group.eventId === eventId)))
  }

  async joinGroup(groupId: string, _request: GroupMembershipRequest) {
    await wait(300)
    const group = this.mutableGroups.find((item) => item.id === groupId)
    if (!group) throw new Error('Crew not found.')
    if (group.currentMembers >= group.maxMembers && !group.joinedByCurrentUser) {
      throw new Error('This crew is already full.')
    }
    if (!group.joinedByCurrentUser) {
      group.currentMembers += 1
      group.joinedByCurrentUser = true
    }
    return groupSchema.parse(clone(group))
  }

  async leaveGroup(groupId: string, _userId: string) {
    await wait(300)
    const group = this.mutableGroups.find((item) => item.id === groupId)
    if (!group) throw new Error('Crew not found.')
    if (group.joinedByCurrentUser) {
      group.currentMembers -= 1
      group.joinedByCurrentUser = false
    }
  }
}

export const clientService: ClientService = new MockClientService()
