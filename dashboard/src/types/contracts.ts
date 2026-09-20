import { z } from 'zod'

const id = z.string().regex(
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
)
const timestamp = z.iso.datetime({ offset: true })
const count = z.number().int().nonnegative()

export const eventCategorySchema = z.enum([
  'Culture',
  'Sport',
  'Education',
  'Community',
  'Other',
])

export const geoPointSchema = z.strictObject({
  latitude: z.number().finite().min(-90).max(90),
  longitude: z.number().finite().min(-180).max(180),
})

export const eventSummarySchema = z.strictObject({
  id,
  name: z.string().min(1).max(160),
  description: z.string().max(1000),
  startAt: timestamp,
  endAt: timestamp.nullable().optional(),
  venueName: z.string().min(1).max(160),
  category: eventCategorySchema,
  location: geoPointSchema,
  participantsCount: count,
  source: z.enum(['Demo', 'City', 'External']),
})

export const eventDetailsSchema = eventSummarySchema.extend({
  externalId: z.string().nullable().optional(),
  crewAvailable: z.boolean(),
  availableTransportModes: z.array(
    z.enum(['Walking', 'PublicTransport', 'Bike', 'Car', 'Unknown']),
  ),
})

export const modalSplitSchema = z.strictObject({
  publicTransport: count,
  walking: count,
  bike: count,
  car: count,
  unknown: count,
})

export const pulseSummarySchema = z.strictObject({
  generatedAt: timestamp,
  eventsCount: count,
  participantsCount: count,
  modalSplit: modalSplitSchema,
  participantsWithoutReturn: count,
})

export const pulseAlertSchema = z.strictObject({
  code: z.enum(['ReturnGap', 'HighDemand', 'LowCoverage']),
  severity: z.enum(['Info', 'Warning', 'Critical']),
  message: z.string().max(300),
})

export const eventPulseSchema = z.strictObject({
  eventId: id,
  eventName: z.string(),
  generatedAt: timestamp,
  participantsCount: count,
  modalSplit: modalSplitSchema,
  participantsWithoutReturn: count,
  alerts: z.array(pulseAlertSchema),
})

const positionSchema = z.tuple([
  z.number().finite().min(-180).max(180),
  z.number().finite().min(-90).max(90),
])

export const hexagonFeatureCollectionSchema = z.strictObject({
  type: z.literal('FeatureCollection'),
  features: z.array(z.strictObject({
    type: z.literal('Feature'),
    id: z.string(),
    geometry: z.strictObject({
      type: z.literal('Polygon'),
      coordinates: z.array(z.array(positionSchema).min(4)).min(1),
    }),
    properties: z.strictObject({
      participants: z.number().int().min(10),
      publicTransport: count,
      walking: count,
      bike: count,
      car: count,
    }),
  })),
})

export const createEventRequestSchema = z.strictObject({
  name: z.string().trim().min(1, 'Name is required.').max(160),
  description: z.string().trim().max(1000),
  startAt: timestamp,
  endAt: timestamp.nullable().optional(),
  venueName: z.string().trim().min(1, 'Venue is required.').max(160),
  category: eventCategorySchema,
  location: geoPointSchema,
}).refine(
  value => !value.endAt || new Date(value.endAt) >= new Date(value.startAt),
  { message: 'End time cannot be earlier than start time.', path: ['endAt'] },
)

export const pulseUpdatedSchema = z.strictObject({
  eventId: id,
  participantsCount: count,
  modalSplit: modalSplitSchema,
  participantsWithoutReturn: count,
  changedAt: timestamp,
})

export type EventCategory = z.infer<typeof eventCategorySchema>
export type GeoPoint = z.infer<typeof geoPointSchema>
export type EventSummary = z.infer<typeof eventSummarySchema>
export type EventDetails = z.infer<typeof eventDetailsSchema>
export type PulseSummary = z.infer<typeof pulseSummarySchema>
export type EventPulse = z.infer<typeof eventPulseSchema>
export type ModalSplit = z.infer<typeof modalSplitSchema>
export type HexagonFeatureCollection = z.infer<typeof hexagonFeatureCollectionSchema>
export type CreateEventRequest = z.infer<typeof createEventRequestSchema>
export type PulseUpdated = z.infer<typeof pulseUpdatedSchema>
