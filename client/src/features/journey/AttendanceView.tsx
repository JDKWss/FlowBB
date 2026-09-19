import {
  Bike,
  BusFront,
  CarFront,
  Check,
  ChevronLeft,
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
    <section className="mx-auto flex min-h-dvh w-full max-w-md flex-col bg-slate-950 px-5 pb-6 pt-4 text-white">
      <header className="mb-8 flex items-center gap-3">
        <button
          type="button"
          onClick={onBack}
          className="grid size-11 shrink-0 place-items-center rounded-full border border-white/10 bg-white/5 text-slate-200 transition hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-300"
          aria-label="Back to event details"
        >
          <ChevronLeft aria-hidden="true" size={22} />
        </button>
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-cyan-300">
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
              onClick={() => onSelectMode(choice.mode)}
              className={`relative min-h-32 rounded-3xl border p-4 text-left transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-300 disabled:cursor-not-allowed disabled:opacity-35 ${
                isSelected
                  ? 'border-cyan-300 bg-cyan-300 text-slate-950 shadow-[0_14px_40px_-20px_rgba(103,232,249,0.8)]'
                  : 'border-white/10 bg-slate-900 text-white hover:border-white/25 hover:bg-slate-800'
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
                <span className="absolute right-3 top-3 grid size-6 place-items-center rounded-full bg-slate-950 text-cyan-300">
                  <Check aria-hidden="true" size={14} strokeWidth={3} />
                </span>
              )}
            </motion.button>
          )
        })}
      </fieldset>

      <div className="mt-auto pt-8">
        {confirmation && (
          <div
            className="mb-4 flex items-start gap-3 rounded-2xl border border-emerald-400/25 bg-emerald-400/10 p-4"
            role="status"
          >
            <span className="grid size-9 shrink-0 place-items-center rounded-full bg-emerald-300 text-emerald-950">
              <Check aria-hidden="true" size={18} strokeWidth={3} />
            </span>
            <div>
              <p className="font-bold text-emerald-100">You&apos;re going!</p>
              <p className="mt-1 text-sm leading-5 text-emerald-100/70">
                {confirmation.participantsCount} people are joining this event.
                {selectedChoice ? ` Route mode: ${selectedChoice.label}.` : ''}
              </p>
            </div>
          </div>
        )}

        {confirmation ? (
          <button
            type="button"
            onClick={onContinue}
            className="flex min-h-14 w-full items-center justify-center rounded-2xl bg-cyan-300 px-5 text-base font-extrabold text-slate-950 shadow-[0_16px_45px_-18px_rgba(103,232,249,0.9)] transition hover:bg-cyan-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-200 focus-visible:ring-offset-2 focus-visible:ring-offset-slate-950"
          >
            See my route
          </button>
        ) : (
          <button
            type="button"
            onClick={onSubmit}
            disabled={isSubmitting || !availableModes.has(selectedMode)}
            className="flex min-h-14 w-full items-center justify-center gap-2 rounded-2xl bg-cyan-300 px-5 text-base font-extrabold text-slate-950 shadow-[0_16px_45px_-18px_rgba(103,232,249,0.9)] transition hover:bg-cyan-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-200 focus-visible:ring-offset-2 focus-visible:ring-offset-slate-950 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isSubmitting ? (
              <>
                <LoaderCircle className="animate-spin" aria-hidden="true" size={20} />
                Saving…
              </>
            ) : (
              "I'm going"
            )}
          </button>
        )}
      </div>
    </section>
  )
}
