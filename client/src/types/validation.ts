import { z } from 'zod'

// Mirrors contracts/openapi.yaml, including the UUID-shaped IDs in its examples.
const id = z.string().regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i)
const timestamp = z.iso.datetime({ offset: true })
const count = z.number().int().nonnegative()
export const transportSchema = z.enum(['Walking', 'PublicTransport', 'Bike', 'Car', 'Unknown'])
const point = z.object({ latitude: z.number().min(-90).max(90), longitude: z.number().min(-180).max(180) })
export const eventSummarySchema = z.object({
  id, name: z.string().min(1).max(160), description: z.string().max(1000),
  startAt: timestamp, endAt: timestamp.nullable().optional(),
  venueName: z.string().min(1).max(160),
  category: z.enum(['Culture', 'Sport', 'Education', 'Community', 'Other']),
  location: point, participantsCount: count, source: z.enum(['Demo', 'City', 'External']),
})
export const eventDetailsSchema = eventSummarySchema.extend({
  externalId: z.string().nullable().optional(), crewAvailable: z.boolean(),
  availableTransportModes: z.array(transportSchema).refine(items => new Set(items).size === items.length),
})
const airQualityMeasurementSchema = z.strictObject({
  value: z.number().finite().nonnegative(),
  unit: z.literal('µg/m³'),
})
export const airQualitySchema = z.strictObject({
  eventId: id,
  station: z.strictObject({
    name: z.string().min(1).max(200),
    distanceMeters: z.number().finite().nonnegative(),
  }),
  measuredAt: timestamp,
  qualityLevel: z.enum(['VeryGood', 'Good', 'Moderate', 'Sufficient', 'Bad', 'VeryBad', 'Unknown']),
  status: z.enum(['Fresh', 'Stale', 'Fallback']),
  source: z.enum(['Gios', 'Demo']),
  pm10: airQualityMeasurementSchema.nullable(),
  pm25: airQualityMeasurementSchema.nullable(),
  no2: airQualityMeasurementSchema.nullable(),
  o3: airQualityMeasurementSchema.nullable(),
  alert: z.strictObject({
    severity: z.enum(['Info', 'Warning']),
    message: z.string().min(1).max(300),
  }).nullable(),
})
export const attendanceRequestSchema = z.strictObject({ userId: id, transportMode: transportSchema })
export const attendanceSchema = z.strictObject({
  eventId: id, userId: id, transportMode: transportSchema,
  participantsCount: count, isNew: z.boolean(), updatedAt: timestamp,
})
export const groupSchema = z.strictObject({
  id, eventId: id, name: z.string().min(1).max(100), description: z.string().max(500),
  currentMembers: count, maxMembers: z.number().int().min(2).max(12),
  tags: z.array(z.string().max(40)).max(10).refine(items => new Set(items).size === items.length),
  meetingPoint: point.extend({ name: z.string().min(1).max(120) }),
  joinedByCurrentUser: z.boolean(),
})
const journeySchema = z.strictObject({
  durationMinutes: count, departureAt: timestamp, arrivalAt: timestamp,
  distanceMeters: z.number().finite().nonnegative().nullable().optional(),
  geometry: z.strictObject({
    type: z.literal('LineString'),
    coordinates: z.array(z.tuple([
      z.number().finite().min(-180).max(180),
      z.number().finite().min(-90).max(90),
    ])).min(2),
  }).nullable().optional(),
  steps: z.array(z.strictObject({
    type: z.enum(['Walk', 'Transit', 'Bike', 'Car', 'Wait']),
    instruction: z.string().max(300), durationMinutes: count,
    line: z.string().max(40).nullable().optional(),
  })).min(1),
  stops: z.array(z.strictObject({
    name: z.string().min(1).max(120),
    latitude: z.number().finite().min(-90).max(90),
    longitude: z.number().finite().min(-180).max(180),
  })).min(2).nullable().optional(),
})
export const routeSchema = z.strictObject({
  eventId: id, userId: id, plannerSource: z.enum(['Demo', 'RoadRouting', 'OpenTripPlanner', 'MzkTimetable']),
  outbound: journeySchema, returns: z.array(journeySchema), returnGap: z.boolean(),
})
