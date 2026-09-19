# Kontrakt danych Neo4j (integracyjny)

Dokument opisuje wezly, relacje i pola, ktorych oczekuje backend. To kontrakt integracyjny, a nie implementacja. Nie zmienia `contracts/openapi.yaml`. Decyzja o bazie: [ADR 001](adr/001-runtime-persistence.md).

Stan: **propozycja docelowa**. Kolumna „Stan w repo” opisuje, co jest na `develop`, a kolumna „Zmiana” wskazuje wlasciciela.

Wlasciciele: **Data/Neo4j** (schemat, constraints, seed, Cypher, adaptery), **Core Backend** (Kuba, zatwierdza kontrakt i odczyty PULSE), **Backend Events** (definiuje ksztalt Event po stronie domeny).

## Zasady ogolne

- Nazwy wlasciwosci: PascalCase, jak w istniejacym kodzie i seedzie.
- Identyfikatory Guid sa przechowywane jako string w formacie kanonicznym (male litery, z myslnikami). Adapter Neo4j konwertuje `Guid` <-> string. Warstwy Application i API widza tylko `Guid`.
- Czas: `datetime` z offsetem, zapisywany jako UTC.
- Wspolrzedne sa `Float` w stopniach (EPSG:4326). Wspolrzedne uzytkownika sa danymi wewnetrznymi.
- Dane demonstracyjne sa syntetyczne i oznaczone w seedzie (`DemoData: true`).
- Licznikow nie przechowuje sie. Liczba uczestnikow to `count` relacji `IS_GOING_TO`.

## Wezly

### `User`

| Pole | Typ | Wymagane | Stan w repo | Uwagi |
|---|---|---|---|---|
| `UserId` | string (Guid) | tak | jest | unikalne |
| `Name` | string | tak | jest | |
| `HomeLatitude` | float | tak | brak | wewnetrzne, demonstracyjne, `[-90, 90]` |
| `HomeLongitude` | float | tak | brak | wewnetrzne, demonstracyjne, `[-180, 180]` |
| `DemoData` | bool | tak | brak | `true` dla seedu |
| `Email`, `PasswordHash` | string | nie | sa | pozostalosc po wczesniejszym modelu. Logowanie jest poza zakresem MVP; pola nie moga byc uzywane do uwierzytelniania ani zwracane przez API. |

Zmiana: dodanie `HomeLatitude`, `HomeLongitude`, `DemoData` i seed gestych punktow w 3-4 obszarach. Wlasciciel: Data/Neo4j.

### `Event`

| Pole | Typ | Wymagane | Stan w repo | Uwagi |
|---|---|---|---|---|
| `EventId` | string (Guid) | tak | jest | unikalne; w domenie `Id` |
| `Name` | string | tak | jako `Title` | zmiana nazwy pola |
| `Description` | string | tak | jest | moze byc pusty |
| `StartAt` | datetime | tak | jako `DateTime` | zmiana nazwy pola |
| `EndAt` | datetime | nie | brak | opcjonalne |
| `Category` | string | tak | brak | wartosci `EventCategory` z OpenAPI: `Culture`, `Sport`, `Education`, `Community`, `Other` |
| `Source` | string | tak | brak | wartosci `EventSource` z OpenAPI: `Demo`, `City`, `External` |
| `EventUrl` | string | nie | jest | |

Powiazanie z miejscem: relacja `(Event)-[:HOSTED_AT]->(Venue)` (jest). Kazde wydarzenie ma dokladnie jedno miejsce.

Zmiana: dostosowanie schematu i seedu do powyzszych pol. Wlasciciel: Data/Neo4j, ksztalt domeny potwierdza Backend Events.

### `Venue`

| Pole | Typ | Wymagane | Stan w repo |
|---|---|---|---|
| `VenueId` | string | tak | jest (obecnie tekstowe slugi, np. `venue-rynek-bb`) |
| `Name` | string | tak | jest |
| `Address` | string | tak | jest |
| `Latitude` | float | tak | jest |
| `Longitude` | float | tak | jest |

Uwaga: jesli kontrakt API bedzie wymagal `Guid` dla miejsca, `VenueId` trzeba ujednolicic. Decyzja: Core Backend.

### `Crew` (propozycja, niezamrozona)

> **Propozycja wymagajaca zatwierdzenia** przez wlasciciela Crew oraz Data/Neo4j. Nie jest zamrozonym kontraktem: nazwy wezla, pol i relacji moga sie zmienic. Do czasu zatwierdzenia nie implementuj jej w adapterach ani seedzie.

Mikrogrupa zgodna z `GroupSummary` z OpenAPI. Nie istnieje jeszcze w Neo4j.

| Pole | Typ | Uwagi |
|---|---|---|
| `CrewId` | string (Guid) | unikalne |
| `Name` | string | 1-100 znakow |
| `Description` | string | do 500 znakow |
| `MaxMembers` | int | 2-12 |
| `Tags` | lista string | do 10, unikalne, do 40 znakow |
| `MeetingPointName` | string | do 120 znakow |
| `MeetingPointLatitude`, `MeetingPointLongitude` | float | |
| `DemoData` | bool | |

Proponowane relacje: `(Crew)-[:FOR_EVENT]->(Event)` oraz `(User)-[:MEMBER_OF]->(Crew)`. Domena Crew jest na branchu `feature/crew-domain`. Zatwierdzaja: wlasciciel Crew (do czasu wskazania innego: Core Backend Owner) i Data/Neo4j Owner.

## Relacje

### `(User)-[:IS_GOING_TO]->(Event)` - Attendance

| Wlasciwosc | Typ | Uwagi |
|---|---|---|
| `TransportMode` | string | `Walking`, `PublicTransport`, `Bike`, `Car`, `Unknown` (enum `TransportMode` z OpenAPI) |
| `OriginLatitude` | float | snapshot z `HomeLatitude` w chwili deklaracji; wewnetrzne |
| `OriginLongitude` | float | snapshot z `HomeLongitude`; wewnetrzne |
| `UpdatedAt` | datetime | UTC |

Stan w repo: relacja istnieje, ale bez wlasciwosci. Wymagane: dodanie wlasciwosci przy `MERGE`.

Reguly:

- Para `(User, Event)` ma co najwyzej jedna relacje: zapis przez `MERGE` na relacji, potem `SET` wlasciwosci. Ponowienie aktualizuje `TransportMode` i `UpdatedAt`, nie zwieksza licznika.
- Odpowiedz `isNew` wynika z tego, czy `MERGE` utworzyl relacje.
- `DELETE Attendance` usuwa relacje; brak relacji nie jest bledem.
- Wspolrzedne nie sa zwracane przez publiczne API ani logowane.

### Pozostale relacje

| Relacja | Stan | Uwagi |
|---|---|---|
| `(Event)-[:HOSTED_AT]->(Venue)` | jest | wymagana dla P0 |
| `(Crew)-[:FOR_EVENT]->(Event)`, `(User)-[:MEMBER_OF]->(Crew)` | propozycja, do zatwierdzenia | P0 (Crew), po zatwierdzeniu |
| `IS_INTERESTED_IN`, `FRIENDS_WITH`, `FOLLOWS`, `LIKES_TAG`, `HAS_TAG`, `MANAGES` | sa w kodzie i seedzie | poza P0; nie rozwijac przed zamknieciem scenariusza demo |

## Constraints i indeksy

| Constraint | Stan |
|---|---|
| `User.UserId`, `Event.EventId`, `Venue.VenueId` - unikalne | jest |
| `Crew.CrewId` - unikalne | propozycja, po zatwierdzeniu Crew |
| Indeks na `Event.StartAt` | opcjonalnie |

Unikalnosc relacji `IS_GOING_TO` zapewnia `MERGE`. Zachowanie przy rownoleglych zadaniach dla tej samej pary trzeba potwierdzic testem na prawdziwej instancji Neo4j; jesli `MERGE` nie wystarcza, dodaje sie blokade lub constraint na relacji (o ile dostepny w uzywanej edycji).

## Odczyty potrzebne PULSE (wewnetrzne)

Dla Core Backend adapter Neo4j udostepnia (kontrakt Application, nie API):

1. Liczba uczestnikow wydarzenia i modal split: `count` relacji `IS_GOING_TO` pogrupowane po `TransportMode`.
2. Lista `(OriginLatitude, OriginLongitude, TransportMode)` dla wydarzenia. Zwracana tylko backendowi, bez `UserId`.

Zapis Attendance i odczyt danych do `PulseUpdated` odbywaja sie w jednej transakcji. Agregacja do heksagonow i filtr `count >= 10` dzieje sie w C#.

## Zmiany do wykonania (podsumowanie)

| # | Zmiana | Wlasciciel |
|---|---|---|
| 1 | `User`: `HomeLatitude`, `HomeLongitude`, `DemoData`; seed gestych punktow | Data/Neo4j |
| 2 | `Event`: `Name`, `StartAt`, `EndAt`, `Category`, `Source` | Data/Neo4j + Backend Events |
| 3 | `IS_GOING_TO`: wlasciwosci snapshotu | Data/Neo4j (kontrakt: Core Backend) |
| 4 | `Crew`, `FOR_EVENT`, `MEMBER_OF`, constraint: propozycja, po zatwierdzeniu | Data/Neo4j + wlasciciel Crew |
| 5 | Interfejsy repozytoriow z Domain do `Application/Abstractions`: decyzja po MVP albo przy pierwszej implementacji repozytorium, nie blokuje MVP | Core Backend Owner + Data/Neo4j |
| 6 | Usuniecie pakietow EF Core/Npgsql z Infrastructure: osobny maly task porzadkowy po potwierdzeniu, ze kod runtime ich nie uzywa | Data/Neo4j (zgoda Core Backend Owner) |
