import type {
  EventDetails,
  EventSummary,
  GroupSummary,
  RouteResponse,
} from '../types/contracts'

export const DEMO_USER_ID = 'dddddddd-dddd-dddd-dddd-dddddddddddd'
export const PRIMARY_EVENT_ID = '11111111-1111-1111-1111-111111111111'

export const eventDetails: EventDetails[] = [
  {
    id: PRIMARY_EVENT_ID,
    name: 'Koncert na Rynku',
    description:
      'Wieczorny koncert w centrum Bielska-Bialej.',
    startAt: '2026-09-19T19:00:00+02:00',
    endAt: '2026-09-19T21:30:00+02:00',
    venueName: 'Rynek w Bielsku-Bialej',
    category: 'Culture',
    location: { latitude: 49.82245, longitude: 19.04431 },
    participantsCount: 82,
    source: 'Demo',
    externalId: null,
    crewAvailable: true,
    availableTransportModes: ['Walking', 'PublicTransport', 'Bike', 'Car'],
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    name: 'Nocny Bieg na Błoniach',
    description:
      'A recreational run after dark. The late finish demonstrates the return-gap alert.',
    startAt: '2026-09-20T21:00:00+02:00',
    endAt: '2026-09-20T23:15:00+02:00',
    venueName: 'Błonia Bielskie',
    category: 'Sport',
    location: { latitude: 49.79381, longitude: 19.04955 },
    participantsCount: 46,
    source: 'Demo',
    externalId: null,
    crewAvailable: true,
    availableTransportModes: ['Walking', 'PublicTransport', 'Bike', 'Car'],
  },
  {
    id: '44444444-4444-4444-4444-444444444444',
    name: 'Warsztaty miejskiego ogrodnictwa',
    description:
      'A practical session about green balconies, water retention, and neighbourhood gardens.',
    startAt: '2026-09-21T10:30:00+02:00',
    endAt: '2026-09-21T12:30:00+02:00',
    venueName: 'Książnica Beskidzka',
    category: 'Education',
    location: { latitude: 49.82055, longitude: 19.04863 },
    participantsCount: 28,
    source: 'Demo',
    externalId: null,
    crewAvailable: false,
    availableTransportModes: ['Walking', 'PublicTransport', 'Bike'],
  },
  {
    id: '55555555-5555-5555-5555-555555555555',
    name: 'Sąsiedzki piknik nad Białą',
    description:
      'A family picnic, local initiatives, and a relaxed afternoon by the river.',
    startAt: '2026-09-21T14:00:00+02:00',
    endAt: '2026-09-21T18:00:00+02:00',
    venueName: 'Bulwary Straceńskie',
    category: 'Community',
    location: { latitude: 49.81324, longitude: 19.05071 },
    participantsCount: 64,
    source: 'Demo',
    externalId: null,
    crewAvailable: true,
    availableTransportModes: ['Walking', 'PublicTransport', 'Bike', 'Car'],
  },
]

export const events: EventSummary[] = eventDetails.map(
  ({ crewAvailable: _crewAvailable, availableTransportModes: _modes, externalId: _externalId, ...event }) =>
    event,
)

const sharedOutbound = {
  durationMinutes: 37,
  departureAt: '2026-09-19T18:12:00+02:00',
  arrivalAt: '2026-09-19T18:49:00+02:00',
  steps: [
    {
      type: 'Walk' as const,
      instruction: 'Idz 6 minut do przystanku.',
      durationMinutes: 6,
    },
    {
      type: 'Transit' as const,
      instruction: 'Wsiadz do autobusu linii 7.',
      durationMinutes: 19,
      line: '7',
    },
    {
      type: 'Walk' as const,
      instruction: 'Idz 4 minuty na Rynek.',
      durationMinutes: 4,
    },
  ],
}

export const routes: Record<string, RouteResponse> = {
  [PRIMARY_EVENT_ID]: {
    eventId: PRIMARY_EVENT_ID,
    userId: DEMO_USER_ID,
    plannerSource: 'Demo',
    outbound: sharedOutbound,
    returns: [
      {
        durationMinutes: 34,
        departureAt: '2026-09-19T21:44:00+02:00',
        arrivalAt: '2026-09-19T22:18:00+02:00',
        steps: [
          {
            type: 'Transit',
            instruction: 'Autobus linii 7.',
            durationMinutes: 26,
            line: '7',
          },
        ],
      },
    ],
    returnGap: false,
  },
  '33333333-3333-3333-3333-333333333333': {
    eventId: '33333333-3333-3333-3333-333333333333',
    userId: DEMO_USER_ID,
    plannerSource: 'Demo',
    outbound: {
      durationMinutes: 24,
      departureAt: '2026-09-20T20:20:00+02:00',
      arrivalAt: '2026-09-20T20:44:00+02:00',
      steps: [
        {
          type: 'Transit',
          instruction: 'Take bus 8 toward Błonia.',
          durationMinutes: 17,
          line: '8',
        },
        {
          type: 'Walk',
          instruction: 'Walk from the stop to the starting area.',
          durationMinutes: 7,
        },
      ],
    },
    returns: [],
    returnGap: true,
  },
  '44444444-4444-4444-4444-444444444444': {
    eventId: '44444444-4444-4444-4444-444444444444',
    userId: DEMO_USER_ID,
    plannerSource: 'Demo',
    outbound: {
      durationMinutes: 18,
      departureAt: '2026-09-21T10:02:00+02:00',
      arrivalAt: '2026-09-21T10:20:00+02:00',
      steps: [
        {
          type: 'Walk',
          instruction: 'Walk through the city centre to Książnica Beskidzka.',
          durationMinutes: 18,
        },
      ],
    },
    returns: [
      {
        durationMinutes: 17,
        departureAt: '2026-09-21T12:42:00+02:00',
        arrivalAt: '2026-09-21T12:59:00+02:00',
        steps: [
          {
            type: 'Walk',
            instruction: 'Walk back through the city centre.',
            durationMinutes: 17,
          },
        ],
      },
    ],
    returnGap: false,
  },
  '55555555-5555-5555-5555-555555555555': {
    eventId: '55555555-5555-5555-5555-555555555555',
    userId: DEMO_USER_ID,
    plannerSource: 'Demo',
    outbound: {
      durationMinutes: 21,
      departureAt: '2026-09-21T13:24:00+02:00',
      arrivalAt: '2026-09-21T13:45:00+02:00',
      steps: [
        {
          type: 'Bike',
          instruction: 'Follow the riverside cycle route to Straconka.',
          durationMinutes: 21,
        },
      ],
    },
    returns: [
      {
        durationMinutes: 23,
        departureAt: '2026-09-21T18:10:00+02:00',
        arrivalAt: '2026-09-21T18:33:00+02:00',
        steps: [
          {
            type: 'Bike',
            instruction: 'Return along the riverside cycle route.',
            durationMinutes: 23,
          },
        ],
      },
    ],
    returnGap: false,
  },
}

export const groups: GroupSummary[] = [
  {
    id: '22222222-2222-2222-2222-222222222222',
    eventId: PRIMARY_EVENT_ID,
    name: 'Nowi w Bielsku',
    description: 'Mikrogrupa dla osob, ktore chca poznac miasto.',
    currentMembers: 4,
    maxMembers: 6,
    tags: ['nowi-w-miescie', 'muzyka'],
    meetingPoint: {
      name: 'Plac Chrobrego',
      latitude: 49.82205,
      longitude: 19.04318,
    },
    joinedByCurrentUser: false,
  },
  {
    id: '66666666-6666-6666-6666-666666666666',
    eventId: PRIMARY_EVENT_ID,
    name: 'Sąsiedzi z Dolnego Przedmieścia',
    description: 'We meet nearby and walk to the concert together.',
    currentMembers: 6,
    maxMembers: 6,
    tags: ['neighbours', 'walking'],
    meetingPoint: {
      name: 'Plac Wojska Polskiego',
      latitude: 49.82413,
      longitude: 19.04561,
    },
    joinedByCurrentUser: false,
  },
]
