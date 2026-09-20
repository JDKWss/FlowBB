# Porty persystencji - co musi dostarczyc adapter Neo4j

Backend jest napisany w calosci przeciwko interfejsom. Zadnego z nich nie implementuje dzis
kod produkcyjny - istnieja tylko fake'i w testach. To jest lista zadan dla Data/Neo4j Ownera
i jednoczesnie spis tego, co blokuje uruchomienie scenariusza demo.

## Podsumowanie

| Port | Plik | Uzywa go | Issue | Implementacja |
|---|---|---|---|---|
| `IAttendanceRepository` | `Application/Abstractions/Persistence/IAttendanceRepository.cs` | Attendance | #17 | brak |
| `IPulseDataReader` | `Application/Abstractions/Persistence/IPulseDataReader.cs` | PULSE | #15 | brak |
| `IEventRepository` | `Application/Abstractions/Persistence/IEventRepository.cs` (branch) | Events | #16 | brak |
| `IEventLookup` | `Application/Abstractions/Persistence/IEventLookup.cs` (branch) | Attendance, Crew, Routing | #2 | `EventLookup` nad `IEventRepository` (branch) |
| `ICrewRepository` | `Application/Abstractions/Persistence/ICrewRepository.cs` (branch) | Crew | #18 | brak |
| `IAttendanceOriginLookup` | `Application/Abstractions/Persistence/IAttendanceOriginLookup.cs` (branch) | Routing | - | brak |
| `IPulseNotifier` | `Application/Abstractions/Realtime/IPulseNotifier.cs` | Attendance | #19 | `SignalRPulseNotifier` (gotowe) |
| `IRoutePlanner` | `Application/Abstractions/Routing/IRoutePlanner.cs` (branch) | Routing | #4 | `DemoRoutePlanner` (gotowe, branch) |

Dwa ostatnie sa jedynymi portami, ktore maja dzialajaca implementacje.

## `IAttendanceRepository` - najtrudniejszy

```csharp
Task<AttendanceUpsertPersistenceResult?> UpsertAsync(AttendanceIntent attendance, CancellationToken ct = default);
Task<AttendanceDeletePersistenceResult>  DeleteAsync(Guid eventId, Guid userId, CancellationToken ct = default);
```

Adapter odpowiada za wiecej niz zapis. Musi w **jednej transakcji**:

1. sprawdzic, ze uzytkownik i wydarzenie istnieja (`null` z `UpsertAsync` oznacza "nie ma" i daje 404);
2. wykonac `MERGE` relacji `(User)-[:IS_GOING_TO]->(Event)` ze snapshotem `TransportMode`,
   `OriginLatitude`, `OriginLongitude`, `UpdatedAt`;
3. ustalic `IsNew` - czy relacja powstala teraz, czy zostala nadpisana;
4. policzyc `ParticipantsCount` jako `count` relacji (licznika sie nie przechowuje);
5. zwrocic `ModalSplit` po zmianie.

Punkty 4 i 5 musza pochodzic **z tej samej transakcji co zapis**, bo ich wynik trafia
prosto do komunikatu SignalR. Odczyt po zatwierdzeniu, osobnym zapytaniem, daje wyscig:
dwie rownolegle deklaracje moga wyslac ten sam licznik.

`DeleteAsync` jest idempotentne: brak relacji to `WasDeleted = false` i **nie jest bledem**.
Wywolujacy endpoint zwraca wtedy 204 i celowo nie publikuje `PulseUpdated`.

Otwarta pozycja kontraktu danych: obecny [NEO4J_CONTRACT.md](../NEO4J_CONTRACT.md) opisuje
`IS_GOING_TO` bez snapshotu transportu, a kod i `contracts/openapi.yaml` go wymagaja
(bez niego modal split i mapa PULSE nie maja skad wziac danych). Do rozstrzygniecia
przez Core Backend i Data/Neo4j.

## `IPulseDataReader`

```csharp
Task<IReadOnlyList<PulsePoint>>    GetPointsAsync(Guid eventId, CancellationToken ct = default);
Task<PulseEventInfo?>              GetEventAsync(Guid eventId, CancellationToken ct = default);
Task<IReadOnlyList<PulseEventInfo>> GetEventsAsync(CancellationToken ct = default);
```

`GetPointsAsync` zwraca **jeden punkt na deklaracje**, bez `userId`. To jedyne miejsce,
w ktorym wspolrzedne uzytkownikow wchodza do backendu; dalej sa juz tylko agregowane.

Uwaga wydajnosciowa: `GetPulseSummaryHandler` wola `GetEventsAsync`, a potem `GetPointsAsync`
w petli po kazdym wydarzeniu (N+1). Dla demo to wystarcza, ale jesli adapter moze zwrocic
punkty wszystkich wydarzen jednym zapytaniem, warto o tym powiedziec Core Backendowi -
zmiana portu jest wtedy tania.

## `IEventRepository` i `IEventLookup`

```csharp
Task<IReadOnlyList<EventWithParticipants>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
Task<EventWithParticipants?>               FindAsync(Guid eventId, CancellationToken ct = default);
```

Sortowanie rosnaco po `StartAt`, granice `from`/`to` wlacznie i tylko wzgledem `StartAt`.
`ParticipantsCount` znowu jest `count` relacji `IS_GOING_TO`, nie polem wezla.

`IEventLookup` **nie jest osobnym zadaniem dla Neo4j** - `EventLookup` implementuje go nad
`IEventRepository`. Wystarczy jeden adapter. Nie tworzymy produkcyjnego `DemoEventLookup`.

Rozbieznosc do zamkniecia: domenowy `Events/Event` ma `Category` i `Source`, a
[NEO4J_CONTRACT.md](../NEO4J_CONTRACT.md) mowi, ze tych pol w grafie nie ma (kategorie sa
wezlami `Tag`). Adapter musi albo mapowac tagi na `EventCategory`, albo schemat zyskuje te pola.

## `ICrewRepository`

```csharp
Task<IReadOnlyList<CrewSummary>> ListByEventAsync(Guid eventId, Guid? userId, CancellationToken ct = default);
Task<CrewJoinResult>             TryJoinAsync(Guid crewId, Guid userId, DateTimeOffset joinedAt, CancellationToken ct = default);
Task                             LeaveAsync(Guid crewId, Guid userId, CancellationToken ct = default);
```

`Crew.Join` w domenie **nie jest bezpieczne wspolbieznie** i nie jest uzywane w sciezce zapisu.
Cala logika dolaczenia idzie przez `TryJoinAsync`, ktore musi atomowo rozstrzygnac szesc przypadkow:
`Joined`, `AlreadyMember`, `Full`, `InAnotherCrew`, `CrewNotFound`, `UserNotFound`.

Reguly: jedna grupa na wydarzenie (ale rownolegle czlonkostwa w grupach roznych wydarzen sa
dozwolone), a ponowne dolaczenie nie zmienia ani licznika, ani `joinedAt` na relacji `MEMBER_OF`.

## `IAttendanceOriginLookup`

```csharp
Task<AttendanceOrigin?> FindAsync(Guid eventId, Guid userId, CancellationToken ct = default);
```

Odczytuje snapshot z relacji `IS_GOING_TO` (punkt startu + tryb transportu) na potrzeby routingu.
`null` oznacza, ze uzytkownik nie zadeklarowal udzialu - endpoint trasy zwraca wtedy 404.

To ten sam snapshot, ktory zapisuje `IAttendanceRepository.UpsertAsync`. Jesli snapshot nie
trafi do grafu, routing nie ma skad wziac punktu startu.

## Reguly wspolne dla kazdego adaptera

- Identyfikatory na granicy Application i API sa typu `Guid`. Adapter trzyma je w grafie jako
  string w formacie kanonicznym i **sam odpowiada za konwersje**.
- Wspolrzedne uzytkownika nie opuszczaja backendu. Zaden port nie zwraca ich razem z `userId`
  poza `IAttendanceOriginLookup`, ktorego wynik trafia wylacznie do planera trasy.
- Nie tworzymy generycznego `Repository<TEntity>`. Kazdy port jest waski i nazwany przypadkiem uzycia.
- Idempotencja i zachowanie constraintow musza byc sprawdzone **na prawdziwej instancji Neo4j**,
  nie na atrapie. Sluzy do tego atrybut `[Neo4jFact]` - patrz [TESTS.md](TESTS.md).
