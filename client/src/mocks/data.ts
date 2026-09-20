import type {
  AirQualityResponse,
  EventDetails,
  EventSummary,
  GroupSummary,
  RouteResponse,
} from '../types/contracts'

export const DEMO_USER_ID = 'dddddddd-dddd-dddd-dddd-dddddddddddd'
export const PRIMARY_EVENT_ID = '11111111-1111-1111-1111-111111111111'

export const airQuality: AirQualityResponse = {
  eventId: PRIMARY_EVENT_ID,
  station: {
    name: 'Bielsko-Biała, ul. Kossak-Szczuckiej',
    distanceMeters: 1576,
  },
  measuredAt: '2026-09-20T10:00:00+02:00',
  qualityLevel: 'Good',
  status: 'Fallback',
  source: 'Demo',
  pm10: null,
  pm25: null,
  no2: { value: 5.3, unit: 'µg/m³' },
  o3: { value: 80.8, unit: 'µg/m³' },
  alert: null,
}

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

// Trasa Koncertu na Rynku to dokladny wynik planera z rozkladu MZK (sobota 2026-09-19, linia 7), a wspolrzedne
// przystankow pochodza z data/gtfs/mzk/parsed/stops.json. Dojscie pieszo jest szacunkiem, godziny autobusu
// pochodza z rozkladu. Nocny Bieg zostaje na planerze demo: z pieciu pobranych linii Bloni nie obsluguje wieczorem zadna.
const concertOutbound = {
  durationMinutes: 23,
  departureAt: '2026-09-19T18:15:00+02:00',
  arrivalAt: '2026-09-19T18:38:00+02:00',
  steps: [
    {
      type: 'Walk' as const,
      instruction: 'Idz 11 min do przystanku Karpacka Osiedle Karpackie (szacunek, linia prosta).',
      durationMinutes: 11,
    },
    {
      type: 'Wait' as const,
      instruction: 'Poczekaj 2 min na linie 7 (rozklad MZK).',
      durationMinutes: 2,
      line: '7',
    },
    {
      type: 'Transit' as const,
      instruction: 'Linia 7, rozklad MZK: 18:28 Karpacka Osiedle Karpackie -> 18:35 Hotel Prezydent.',
      durationMinutes: 7,
      line: '7',
    },
    {
      type: 'Walk' as const,
      instruction: 'Idz 3 min na miejsce wydarzenia (szacunek, linia prosta).',
      durationMinutes: 3,
    },
  ],
  stops: [
    { name: "Karpacka Osiedle Karpackie", latitude: 49.806768, longitude: 19.0337205 },
    { name: "Hotel Prezydent", latitude: 49.823493, longitude: 19.0449202 },
  ],
}

export const routes: Record<string, RouteResponse> = {
  [PRIMARY_EVENT_ID]: {
    eventId: PRIMARY_EVENT_ID,
    userId: DEMO_USER_ID,
    plannerSource: 'MzkTimetable',
    outbound: concertOutbound,
    returns: [
      {
        durationMinutes: 56,
        departureAt: '2026-09-19T21:40:00+02:00',
        arrivalAt: '2026-09-19T22:36:00+02:00',
        steps: [
          {
            type: 'Walk',
            instruction: 'Idz 6 min do przystanku Plac Żwirki i Wigury (szacunek, linia prosta).',
            durationMinutes: 6,
          },
          {
            type: 'Wait',
            instruction: 'Poczekaj 27 min na linie 7 (rozklad MZK).',
            durationMinutes: 27,
            line: '7',
          },
          {
            type: 'Transit',
            instruction: 'Linia 7, rozklad MZK: 22:13 Plac Żwirki i Wigury -> 22:23 Browarna.',
            durationMinutes: 10,
            line: '7',
          },
          {
            type: 'Walk',
            instruction: 'Idz 13 min do punktu startu (szacunek, linia prosta).',
            durationMinutes: 13,
          },
        ],
        stops: [
          { name: "Plac Żwirki i Wigury", latitude: 49.8192094, longitude: 19.0436443 },
          { name: "Browarna", latitude: 49.8193787, longitude: 19.0314566 },
        ],
      },
      {
        durationMinutes: 64,
        departureAt: '2026-09-19T21:40:00+02:00',
        arrivalAt: '2026-09-19T22:44:00+02:00',
        steps: [
          {
            type: 'Walk',
            instruction: 'Idz 9 min do przystanku Sobieskiego Wyspiańskiego (szacunek, linia prosta).',
            durationMinutes: 9,
          },
          {
            type: 'Wait',
            instruction: 'Poczekaj 32 min na linie 7 (rozklad MZK).',
            durationMinutes: 32,
            line: '7',
          },
          {
            type: 'Transit',
            instruction: 'Linia 7, rozklad MZK: 22:21 Sobieskiego Wyspiańskiego -> 22:33 Karpacka Osiedle Karpackie.',
            durationMinutes: 12,
            line: '7',
          },
          {
            type: 'Walk',
            instruction: 'Idz 11 min do punktu startu (szacunek, linia prosta).',
            durationMinutes: 11,
          },
        ],
        stops: [
          { name: "Sobieskiego Wyspiańskiego", latitude: 49.8203709, longitude: 19.0374888 },
          { name: "Karpacka Osiedle Karpackie", latitude: 49.806768, longitude: 19.0337205 },
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
