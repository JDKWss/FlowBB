# Kontrakt danych Neo4j (integracyjny)

Dokument opisuje wezly, relacje i pola, ktorych oczekuje backend. To kontrakt integracyjny, a nie implementacja. Nie zmienia `contracts/openapi.yaml`. Decyzja o bazie: [ADR 001](adr/001-runtime-persistence.md).

Stan: **zaakceptowany model danych**. Kolumna „Stan w repo” opisuje stan
implementacji na `develop` z 2026-09-20; niezgodnosc w tej kolumnie oznacza
zadanie implementacyjne, a nie zmiane zaakceptowanego kontraktu.

Wlasciciele: **Data/Neo4j** (schemat, constraints, seed, Cypher, adaptery), **Core Backend** (Kuba, zatwierdza kontrakt i odczyty PULSE), **Backend Events** (definiuje ksztalt Event po stronie domeny).

## Zasady ogolne

- Nazwy wlasciwosci: PascalCase, jak w istniejacym kodzie i seedzie.
- Identyfikatory Guid sa przechowywane jako string w formacie kanonicznym (male litery, z myslnikami). Adapter Neo4j konwertuje `Guid` <-> string. Warstwy Application i API widza tylko `Guid`.
- Czas: `datetime` z offsetem, zapisywany jako UTC.
- Wspolrzedne sa `Float` w stopniach (EPSG:4326). Wspolrzedne uzytkownika sa danymi wewnetrznymi.
- Dane demonstracyjne w pliku seed sa syntetyczne; model nie zawiera pola `DemoData`.
- Licznikow nie przechowuje sie. Liczba uczestnikow to `count` relacji `IS_GOING_TO`.

## Wezly

### `User`

| Pole | Typ | Wymagane | Stan w repo | Uwagi |
|---|---|---|---|---|
| `UserId` | string (Guid) | tak | jest | unikalne |
| `Name` | string | tak | jest | |
| `HomeLatitude` | float | tak | jest w schemacie, seedzie `flowbb-queries.cypher` i adapterach | wewnetrzny domyslny punkt rozpoczecia podrozy, `[-90, 90]`. Nazwa docelowa (ADR 001, AGENTS.md sekcja 8). Adaptery czytaja tez zamiennik `DefaultOriginLatitude` (patrz [NEO4J_ADAPTER_RECONCILIATION.md](NEO4J_ADAPTER_RECONCILIATION.md)) |
| `HomeLongitude` | float | tak | jak wyzej | wewnetrzny domyslny punkt rozpoczecia podrozy, `[-180, 180]`. Zamiennik: `DefaultOriginLongitude` |
| `Email`, `PasswordHash` | string | nie | sa | pozostalosc po wczesniejszym modelu. Logowanie jest poza zakresem MVP; pola nie moga byc uzywane do uwierzytelniania ani zwracane przez API. |

Dokladny punkt startowy jest danymi wewnetrznymi i nie moze byc zwracany przez publiczne API.

### `Event`

| Pole | Typ | Wymagane | Stan w repo | Uwagi |
|---|---|---|---|---|
| `EventId` | string (Guid) | tak | jest | unikalne; w domenie `Id` |
| `Name` | string | tak | jest | |
| `Description` | string | tak | jest | moze byc pusty |
| `StartAt` | datetime | tak | jest | |
| `EndAt` | datetime | nie | jest | opcjonalne |
| `EventUrl` | string | tak | jest | link do strony zrodlowej wydarzenia |
| `Category` | string | tak | jest w seedzie | nazwa wartosci `EventCategory` z domeny: `Culture`, `Sport`, `Education`, `Community`, `Other` |
| `Source` | string | tak | jest w seedzie | nazwa wartosci `EventSource` z domeny: `Demo`, `City`, `External` |

Powiazanie z miejscem: relacja `(Event)-[:HOSTED_AT]->(Venue)` (jest). Kazde wydarzenie ma dokladnie jedno miejsce.

`Category` i `Source` to pola wezla `Event` (1:1 z enumami domeny `Event`), a nie wezly `Tag`. Wezly `Tag` i relacja `(Event)-[:HAS_TAG]->(Tag)` pozostaja w seedzie, ale sa poza P0 i adapter Events ich nie czyta. `VenueName` i `Location` wydarzenia adapter odczytuje z wezla `Venue` przez `HOSTED_AT`.

### `Venue`

| Pole | Typ | Wymagane | Stan w repo |
|---|---|---|---|
| `VenueId` | string | tak | jest (obecnie tekstowe slugi, np. `venue-rynek-bb`) |
| `Name` | string | tak | jest |
| `Address` | string | tak | jest |
| `Latitude` | float | tak | jest |
| `Longitude` | float | tak | jest |

`VenueId` pozostaje tekstowym slugiem, np. `venue-rynek-bb`. Adapter Events zwraca dane miejsca wymagane przez domenę: nazwę i współrzędne.

### `Crew`

Mikrogrupa zgodna z `GroupSummary` z OpenAPI.

| Pole | Typ | Uwagi |
|---|---|---|
| `CrewId` | string (Guid) | unikalne |
| `Name` | string | 1-100 znakow |
| `Description` | string | do 500 znakow |
| `MaxMembers` | int | 2-12 |
| `Tags` | lista string | do 10, unikalne, do 40 znakow |
| `MeetingPointName` | string | do 120 znakow |
| `MeetingPointLatitude`, `MeetingPointLongitude` | float | |

Relacje: `(Crew)-[:FOR_EVENT]->(Event)` oraz `(User)-[:MEMBER_OF]->(Crew)`.

Relacja `MEMBER_OF` ma wlasciwosc `JoinedAt` (datetime z offsetem, UTC), ustawiana przy pierwszym dolaczeniu. Ponowne dolaczenie jej nie zmienia (port `ICrewRepository.TryJoinAsync`).

## Relacje

### `(User)-[:IS_GOING_TO]->(Event)` - Attendance

Relacja oznacza deklaracje udzialu i przechowuje snapshot wymagany przez
Attendance, PULSE i Routing. Samo planowanie trasy nalezy do `IRoutePlanner`;
nie zmienia to odpowiedzialnosci relacji za dane wejsciowe.

| Pole relacji | Typ | Wymagane | Stan w repo | Uwagi |
|---|---|---|---|---|
| `TransportMode` | string | tak | jest w obu seedach i w adapterze Attendance | dokladna nazwa enuma: `Walking`, `PublicTransport`, `Bike`, `Car`, `Unknown` |
| `OriginLatitude` | float | tak | jak wyzej | snapshot `User.HomeLatitude` (lub zamiennika `DefaultOriginLatitude`), `[-90, 90]` |
| `OriginLongitude` | float | tak | jak wyzej | snapshot `User.HomeLongitude` (lub zamiennika `DefaultOriginLongitude`), `[-180, 180]` |
| `UpdatedAt` | datetime z offsetem | tak | jak wyzej | czas ostatniego zapisu deklaracji, UTC |

Reguly:

- Para `(User, Event)` ma co najwyzej jedna relacje: zapis przez `MERGE` na relacji nie zwieksza licznika przy ponowieniu.
- Ponowny zapis aktualizuje snapshot i `UpdatedAt`, ale nie zwiększa liczby uczestników.
- `DELETE Attendance` usuwa relacje; brak relacji nie jest bledem.
- Wspolrzedne nie sa zwracane przez publiczne API ani logowane.

### Pozostale relacje

| Relacja | Stan | Uwagi |
|---|---|---|
| `(Event)-[:HOSTED_AT]->(Venue)` | jest | wymagana dla P0 |
| `(Crew)-[:FOR_EVENT]->(Event)`, `(User)-[:MEMBER_OF]->(Crew)` | jest | mikrogrupy powiazane z wydarzeniem |
| `IS_INTERESTED_IN`, `FRIENDS_WITH`, `FOLLOWS`, `LIKES_TAG`, `HAS_TAG`, `MANAGES` | sa w kodzie i seedzie | poza P0; nie rozwijac przed zamknieciem scenariusza demo |

## Constraints i indeksy

Definicja: `database/schema.cypher` (idempotentny, uruchomienie: [database/README.md](../database/README.md)).

| Constraint / indeks | Stan |
|---|---|
| `User.UserId`, `Event.EventId`, `Venue.VenueId`, `Crew.CrewId` - unikalne | jest |
| `Tag.TagId` - unikalne | jest (poza P0) |
| Indeks zakresu na `Event.StartAt` | jest |

Neo4j Community nie obsluguje constraintow istnienia (`IS NOT NULL`), wiec schemat ich nie zawiera. Kolumna „Wymagane” oznacza, ze pole musi zapisac adapter lub seed; baza tego nie wymusza, wymusza tylko unikalnosc identyfikatorow.

Unikalnosc relacji `IS_GOING_TO` zapewnia `MERGE`. Test na docelowej instancji Aura potwierdzil, ze 10 rownoleglych zapisow tej samej pary tworzy jedna relacje i nie zwieksza licznika wielokrotnie.

## Odczyty PULSE

Adapter `IPulseDataReader` odczytuje z Neo4j wyłącznie współrzędne i `TransportMode` snapshotów, bez identyfikatorów użytkowników. Backend C# wylicza z nich liczniki, modal split i komórki heksagonalne. Publiczne API nie zwraca surowych punktów i ukrywa komórki z `count < 10`.

PULSE nie przechowuje niezależnych liczników. Po zatwierdzeniu transakcji Attendance API publikuje przez SignalR zdarzenie `PulseUpdated` zawierające wyłącznie agregaty.

## Zmiany do wykonania (podsumowanie)

| # | Zmiana | Wlasciciel |
|---|---|---|
| 1 | Rozbudowa seedu po otrzymaniu realnych danych ze scrapera | Data/Neo4j |
| 2 | Idempotentny importer realnych wydarzen | Data/Neo4j + Backend Events |
| 3 | Interfejsy repozytoriow z Domain do `Application/Abstractions`: decyzja po MVP albo przy pierwszej implementacji repozytorium, nie blokuje MVP | Core Backend Owner + Data/Neo4j |
| 4 | Usuniecie pakietow EF Core/Npgsql z Infrastructure: osobny maly task porzadkowy po potwierdzeniu, ze kod runtime ich nie uzywa | Data/Neo4j (zgoda Core Backend Owner) |
| 5 | Ujednolicenie nazw wspolrzednych uzytkownika (`Home*` docelowo, `DefaultOrigin*` w seedzie demonstracyjnym). Do tego czasu adaptery czytaja obie nazwy; plan: [NEO4J_ADAPTER_RECONCILIATION.md](NEO4J_ADAPTER_RECONCILIATION.md) | Data/Neo4j + Core Backend Owner |
| 6 | Adaptery Neo4j (Events, Attendance, PULSE, Crew): zrobione, testy na prawdziwej instancji w `FlowBB.Infrastructure.Tests` | Data/Neo4j |
