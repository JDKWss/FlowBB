# Modul Attendance

Deklaracja "Ide" - serce scenariusza demo. Wlasciciel: Core Backend (Kuba). Issues #10, #14, #19.
**Status: kod na `develop`, nie podpiety w `Program.cs`, brak adaptera Neo4j (#17).**

## Endpointy

| Metoda | Sciezka | Odpowiedzi |
|---|---|---|
| `POST` | `/api/events/{eventId}/attendance` | 200 `AttendanceResponse`, 400, 404 |
| `DELETE` | `/api/events/{eventId}/attendance/{userId}` | 204 zawsze (gdy id sa poprawne), 400 |

Zadanie: `{ "userId": "...", "transportMode": "PublicTransport" }`.

## Przeplyw POST

```text
AttendanceEndpoints.UpsertAsync
  -> walidacja: eventId, userId niepuste, transportMode zdefiniowany   (inaczej 400)
  -> UpsertAttendanceHandler
       -> new AttendanceIntent(eventId, userId, mode, TimeProvider.GetUtcNow())
       -> IAttendanceRepository.UpsertAsync(...)        <-- ADAPTER NEO4J, BRAK
       -> null? => 404
  -> IPulseNotifier.PublishAsync(PulseUpdate)           <-- dopiero tutaj, po zapisie
  -> 200 AttendanceResponse
```

Trzy rzeczy, ktore latwo zepsuc przy zmianach w tym module:

**Kolejnosc.** Publikacja `PulseUpdated` nastepuje **po** zapisie. Odwrocenie kolejnosci oznacza,
ze dashboard pokaze licznik, ktory nigdy nie trafil do bazy.

**DELETE nie publikuje, gdy nic nie usunieto.** `if (result.WasDeleted)` jest celowe: ponowne
DELETE jest no-opem, a nie zdarzeniem. Bez tego dashboard migalby przy kazdym powtorzonym zadaniu.

**Licznik i modal split pochodza z repozytorium, nie z handlera.** `UpsertAttendanceResult` tylko
przepisuje `ParticipantsCount` i `ModalSplit` zwrocone przez adapter. Dlatego adapter musi je
policzyc w tej samej transakcji, co zapis (patrz [PERSISTENCE_PORTS.md](PERSISTENCE_PORTS.md)).

## Idempotencja

Wymagana przez `contracts/openapi.yaml`: powtorzony POST dla tej samej pary `userId`-`eventId`
aktualizuje tryb transportu, ale **nie zwieksza licznika**. Realizuje ja `MERGE` po stronie
adaptera, nie kod C#. Flaga `isNew` w odpowiedzi mowi klientowi, czy to byla pierwsza deklaracja.

Konsekwencja dla demo: drugie klikniecie "Ide" ma zostawic licznik bez zmian, a zmiana srodka
transportu ma przesunac tylko modal split.

## Model domenowy

`FlowBB.Domain/Attendance/AttendanceIntent.cs` - rekord z walidacja w konstruktorze:
niepuste `EventId` i `UserId`, `TransportMode` musi byc zdefiniowana wartoscia enuma,
`UpdatedAt` zawsze normalizowany do UTC.

`TransportMode`: `Walking`, `PublicTransport`, `Bike`, `Car`, `Unknown`.
`Unknown` istnieje dla danych zastanych - oznacza brak lub niepoprawny tryb.
Routing celowo **odmawia** planowania dla `Unknown` zamiast zgadywac.

## SignalR

Hub `PulseHub` pod `/hubs/pulse` jest pusty - klienci tylko odbieraja.
`SignalRPulseNotifier` wysyla `PulseUpdated` do `Clients.All` (bez grup per wydarzenie -
przy skali demo to wystarcza, dashboard filtruje po `eventId`).

Komunikat zawiera wylacznie agregaty: `eventId`, `participantsCount`, `modalSplit`,
`participantsWithoutReturn`, `changedAt`. Nigdy `userId` ani wspolrzednych.

`participantsWithoutReturn` to dzis stala `0` (`GetEventPulseHandler.ParticipantsWithoutReturnInMvp`).
Alert luki powrotowej nie ma jeszcze zrodla danych - to jawne zalozenie MVP, nie blad.

## Pliki

```text
Domain/Attendance/AttendanceIntent.cs
Domain/Common/TransportMode.cs
Application/Attendance/UpsertAttendance/{Command,Handler,Result}.cs
Application/Attendance/DeleteAttendance/{Command,Handler,Result}.cs
Application/Abstractions/Persistence/IAttendanceRepository.cs
Application/Abstractions/Realtime/IPulseNotifier.cs
Api/Endpoints/Events/AttendanceEndpoints.cs
Api/Endpoints/Events/AttendanceContracts.cs
Api/Hubs/{PulseHub,PulseHubExtensions,SignalRPulseNotifier}.cs
```
