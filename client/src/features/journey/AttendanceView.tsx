import {
  Bike,
  BusFront,
  CarFront,
  Check,
  Footprints,
  LoaderCircle,
  type LucideIcon,
} from 'lucide-react'
import type {
  AttendanceResponse,
  EventDetails,
  TransportMode,
} from '../../types/contracts'
import { motion, useReducedMotion } from 'motion/react'
import { FlowBackButton } from '../../components/FlowBackButton'
import { Alert, AlertDescription, AlertTitle, Button } from '../../components/ui'

export interface AttendanceViewProps {
  event: EventDetails
  selectedMode: TransportMode
  onSelectMode: (mode: TransportMode) => void
  onSubmit: () => void
  onContinue: () => void
  onBack: () => void
  isSubmitting: boolean
  confirmation: AttendanceResponse | null
}

interface TransportChoice {
  mode: TransportMode
  label: string
  description: string
  icon: LucideIcon
}

const transportChoices: TransportChoice[] = [
  {
    mode: 'Walking',
    label: 'Walk',
    description: 'On foot',
    icon: Footprints,
  },
  {
    mode: 'PublicTransport',
    label: 'Public transport',
    description: 'Bus or train',
    icon: BusFront,
  },
  {
    mode: 'Bike',
    label: 'Bike',
    description: 'Cycle there',
    icon: Bike,
  },
  {
    mode: 'Car',
    label: 'Car',
    description: 'Drive or share',
    icon: CarFront,
  },
]

export function AttendanceView({
  event,
  selectedMode,
  onSelectMode,
  onSubmit,
  onContinue,
  onBack,
  isSubmitting,
  confirmation,
}: AttendanceViewProps) {
  const reducedMotion = useReducedMotion()
  const availableModes = new Set(event.availableTransportModes)
  const selectedChoice = transportChoices.find(
    (choice) => choice.mode === selectedMode,
  )

  return (
    <section className="flex min-h-full w-full flex-col bg-background px-5 pb-6 pt-4 text-white">
      <header className="mb-8 flex items-center gap-3">
        <FlowBackButton label="Back to event details" onClick={onBack} />
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-primary">
            Plan your trip
          </p>
          <p className="truncate text-sm text-slate-400">{event.name}</p>
        </div>
      </header>

      <div className="mb-7">
        <h1 className="text-3xl font-bold tracking-tight text-white">
          How will you get there?
        </h1>
        <p className="mt-2 text-sm leading-6 text-slate-400">
          Pick a transport mode so FlowBB can prepare your route.
        </p>
      </div>

      <fieldset className="grid grid-cols-2 gap-3">
        <legend className="sr-only">Transport mode</legend>
        {transportChoices.map((choice) => {
          const Icon = choice.icon
          const isSelected = choice.mode === selectedMode
          const isAvailable = availableModes.has(choice.mode)

          return (
            <motion.button
              whileTap={reducedMotion ? undefined : { scale: .97 }}
              key={choice.mode}
              type="button"
              disabled={!isAvailable || isSubmitting}
              aria-pressed={isSelected}
              aria-label={`Select ${choice.label}`}
              onClick={() => onSelectMode(choice.mode)}
              className={`relative min-h-32 rounded-3xl p-4 text-left transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:cursor-not-allowed disabled:opacity-35 ${
                isSelected
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-card text-white hover:bg-neutral-800'
              }`}
            >
              <span
                className={`mb-5 grid size-10 place-items-center rounded-2xl ${
                  isSelected ? 'bg-slate-950/10' : 'bg-white/5'
                }`}
              >
                <Icon aria-hidden="true" size={21} />
              </span>
              <span className="block text-sm font-bold">{choice.label}</span>
              <span
                className={`mt-0.5 block text-xs ${
                  isSelected ? 'text-slate-800' : 'text-slate-500'
                }`}
              >
                {isAvailable ? choice.description : 'Unavailable'}
              </span>
              {isSelected && (
                <span className="absolute right-3 top-3 grid size-6 place-items-center rounded-full bg-black text-primary">
                  <Check aria-hidden="true" size={14} strokeWidth={3} />
                </span>
              )}
            </motion.button>
          )
        })}
      </fieldset>

      <div className="mt-auto pt-8">
        {confirmation && (
          <Alert className="mb-4 bg-primary/10 text-primary" role="status">
            <Check aria-hidden="true" size={18} strokeWidth={3} />
            <AlertTitle>You&apos;re going!</AlertTitle>
            <AlertDescription className="text-primary/75">
                {confirmation.participantsCount} people are joining this event.
                {selectedChoice ? ` Route mode: ${selectedChoice.label}.` : ''}
            </AlertDescription>
          </Alert>
        )}

        {confirmation ? (
          <Button
            type="button"
            size="lg"
            onClick={onContinue}
            className="w-full"
          >
            See my route
          </Button>
        ) : (
          <Button
            type="button"
            size="lg"
            onClick={onSubmit}
            disabled={isSubmitting || !availableModes.has(selectedMode)}
            className="w-full"
          >
            {isSubmitting ? (
              <>
                <LoaderCircle className="animate-spin" aria-hidden="true" size={20} />
                Saving…
              </>
            ) : (
              "I'm going"
            )}
          </Button>
        )}
      </div>
    </section>
  )
}
