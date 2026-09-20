import { useEffect, useRef, useState } from 'react'
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { apiBaseUrl } from '../services/dashboardService'
import {
  pulseUpdatedSchema,
  type EventPulse,
  type EventSummary,
  type PulseUpdated,
} from '../types/contracts'
import { dashboardKeys } from './useDashboardData'

export type LiveStatus = 'Live' | 'Reconnecting' | 'Offline'

export function usePulseConnection(selectedEventId: string | null) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<LiveStatus>('Offline')
  const [lastUpdate, setLastUpdate] = useState<PulseUpdated | null>(null)
  const selectedEventIdRef = useRef(selectedEventId)

  useEffect(() => {
    selectedEventIdRef.current = selectedEventId
  }, [selectedEventId])

  useEffect(() => {
    let disposed = false
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/pulse`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    const reconcile = (message: unknown) => {
      const parsed = pulseUpdatedSchema.safeParse(message)
      if (!parsed.success) return

      const update = parsed.data
      setLastUpdate(update)
      queryClient.setQueryData<EventPulse>(
        dashboardKeys.eventPulse(update.eventId),
        current => current
          ? {
              ...current,
              participantsCount: update.participantsCount,
              modalSplit: update.modalSplit,
              participantsWithoutReturn: update.participantsWithoutReturn,
              generatedAt: update.changedAt,
            }
          : current,
      )
      queryClient.setQueryData<EventSummary[]>(
        dashboardKeys.events,
        current => current?.map(event => event.id === update.eventId
          ? { ...event, participantsCount: update.participantsCount }
          : event),
      )

      void queryClient.invalidateQueries({
        queryKey: dashboardKeys.eventPulse(update.eventId),
      })
      void queryClient.invalidateQueries({ queryKey: dashboardKeys.pulseSummary })
      void queryClient.invalidateQueries({ queryKey: dashboardKeys.events })
      if (selectedEventIdRef.current === update.eventId) {
        void queryClient.invalidateQueries({
          queryKey: dashboardKeys.hexagons(update.eventId),
        })
      }
    }

    connection.on('PulseUpdated', reconcile)
    connection.onreconnecting(() => {
      if (!disposed) setStatus('Reconnecting')
    })
    connection.onreconnected(() => {
      if (!disposed) setStatus('Live')
      const activeEventId = selectedEventIdRef.current
      if (activeEventId) {
        void queryClient.invalidateQueries({
          queryKey: dashboardKeys.eventPulse(activeEventId),
        })
        void queryClient.invalidateQueries({
          queryKey: dashboardKeys.hexagons(activeEventId),
        })
      }
      void queryClient.invalidateQueries({ queryKey: dashboardKeys.pulseSummary })
    })
    connection.onclose(() => {
      if (!disposed) setStatus('Offline')
    })

    void connection.start()
      .then(() => {
        if (!disposed) setStatus('Live')
      })
      .catch(() => {
        if (!disposed) setStatus('Offline')
      })

    return () => {
      disposed = true
      connection.off('PulseUpdated', reconcile)
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop()
      }
    }
  }, [queryClient])

  return { status, lastUpdate }
}
