# Modul Events

Lista i szczegoly wydarzen - punkt wejscia calego scenariusza. Wlasciciel: Backend Events.
Issues #2, #9, #11.

**Status: kod istnieje wylacznie na lokalnych branchach `feature/events-domain`,
`feature/events-application`, `feature/events-api`. Nie ma go na `origin/develop`.**
Brak adaptera Neo4j (#16).

## Endpointy

| Metoda | Sciezka | Odpowiedzi |
|---|---|---|
| `GET` | `/api/events?from=&to=` | 200 lista `EventSummary`, 400 przy zlym zakresie |
| `GET` | `/api/events/{eventId}` | 200 `EventDetails`, 400, 404 |

`from` i `to` sa opcjonalne, w ISO 8601, wlacznie, i dotycza `StartAt`.
Brak wartosci to brak ograniczenia; `from > to` to 400.

Zachowanie 400 jest **zatwierdzone i dopisane do kontraktu**: `getEventById` ma w
`contracts/openapi.yaml` odpowiedzi 200, 400, 404 i 500, wraz z przykladem `ProblemDetails`.

Rozroznienie jest celowe: `eventId` jest przyjmowany jako `string`, a nie `Guid`, wlasnie po to,
by "to nie jest UUID" (400) odroznic od "UUID poprawny, ale nie ma takiego wydarzenia" (404).
Routing binding na `{eventId:guid}` dalby 404 w obu przypadkach.

## Model domenowy

`FlowBB.Domain/Events/Event.cs` - walidacja w konstruktorze:

| Pole | Regula |
|---|---|
| `Id` | niepusty `Guid` |
| `Name` | wymagane, do 160 znakow |
| `Description` | moze byc puste, do 1000 znakow |
| `StartAt`, `EndAt` | `EndAt` nie moze byc wczesniejsze niz `StartAt`; `EndAt` opcjonalne |
| `VenueName` | wymagane, do 160 znakow |
| `Category` | `Culture`, `Sport`, `Education`, `Community`, `Other` |
| `Source` | `Demo`, `City`, `External` |
| `Location` | `GeoPoint` - szerokosc `[-90, 90]`, dlugosc `[-180, 180]`, wartosci skonczone |

`Source` istnieje po to, zeby UI mogl oznaczyc dane jako `DEMO DATA / SYMULACJA` zgodnie z `AGENTS.md`.

**Uwaga dla Data/Neo4j:** [NEO4J_CONTRACT.md](../NEO4J_CONTRACT.md) mowi, ze `Category` i `Source`
nie wystepuja w grafie, a kategorie sa wezlami `Tag`. Adapter musi te dwie reprezentacje pogodzic.

## Dwa modele Event w repozytorium

To jest najczestsze zrodlo nieporozumien w tym module.

| Typ | Rola |
|---|---|
| `FlowBB.Domain/Events/Event.cs` | nowy, wlasciwy model domenowy uzywany przez Events, Crew i Routing |
| `FlowBB.Domain/Models/Event.cs` | stary rekord (`EventId`, `Title`, `EventUrl`, `DateTime`) uzywany przez `Neo4jFlowBbGraphRepository` |

Branch `feature/events-domain` oznaczyl stary typ komentarzem `Superseded by ...` i zostawil go
tylko do czasu, az adapter Neo4j przejdzie na nowy model. **Nowy kod nie powinien uzywac
`Models/Event`.** Docelowo znika razem z zadaniem #16.

## Warstwa Application

| Element | Rola |
|---|---|
| `GetEventsQuery` | opcjonalny zakres czasu + `HasValidRange()` |
| `GetEventsHandler` | waliduje zakres, deleguje do `IEventRepository.ListAsync` |
| `GetEventHandler` | `FindAsync`, mapuje na `EventDetails`, `null` gdy brak |
| `EventWithParticipants` | wydarzenie + `ParticipantsCount` (wynik odczytu, nie pole modelu) |
| `EventDetails` | `EventWithParticipants` + `CrewAvailable` + lista srodkow transportu |
| `EventLookup` | implementuje `IEventLookup` nad `IEventRepository` |

**Dwie wartosci tymczasowe w `EventDetails`:**
- `CrewAvailable` jest zawsze `false` - Crew nie ma jeszcze zrodla w grafie;
- `AvailableTransportModes` to stala lista `[Walking, PublicTransport, Bike, Car]`.

Obie sa w jednym miejscu (`EventDetails.From`), wiec po wdrozeniu Crew poprawia sie je jedna zmiana.
`Unknown` swiadomie nie jest na liscie - to nie jest wybor uzytkownika.

## Strefa czasowa

`EventResponseMapping` konwertuje `StartAt` i `EndAt` na `Europe/Warsaw` przez
`TimeZoneInfo.FindSystemTimeZoneById`. Wewnatrz backendu wszystko pozostaje w UTC.

**Zweryfikowane w kontenerze:** obraz bazowy `mcr.microsoft.com/dotnet/aspnet:10.0` zawiera
tzdata (`/usr/share/zoneinfo/Europe/Warsaw`), a konwersja daje poprawny czas letni (+02:00)
i zimowy (+01:00). Nie trzeba doinstalowywac `tzdata` w `backend/Dockerfile`.

Strefa jest rozwiazywana raz, w statycznym polu `DemoTimeZone`, a nie przy kazdym mapowaniu.
Drobny dlug: to samo pole jest zdublowane w `EventsResponses.cs` i `RoutingResponses.cs` -
warto je kiedys scalic w jedno miejsce.

## Pliki

```text
Domain/Events/{Event,EventCategory,EventSource}.cs
Domain/Common/GeoPoint.cs
Application/Events/{EventDetails,EventWithParticipants,EventLookup}.cs
Application/Events/GetEvents/{GetEventsQuery,GetEventsHandler}.cs
Application/Events/GetEvent/GetEventHandler.cs
Application/Abstractions/Persistence/{IEventRepository,IEventLookup}.cs
Api/Endpoints/Events/{EventsEndpoints,EventsResponses}.cs
```
