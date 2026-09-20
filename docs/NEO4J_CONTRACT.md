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
| `HomeLatitude` | float | tak | jest w schemacie i seedzie (`database/`); `Domain/Models/User.cs` i stary adapter grafu nadal uzywaja `DefaultOriginLatitude` | wewnetrzny domyslny punkt rozpoczecia podrozy, `[-90, 90]` |
| `HomeLongitude` | float | tak | jest w schemacie i seedzie (`database/`); `Domain/Models/User.cs` i stary adapter grafu nadal uzywaja `DefaultOriginLongitude` | wewnetrzny domyslny punkt rozpoczecia podrozy, `[-180, 180]` |
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

Uwaga: jesli kontrakt API bedzie wymagal `Guid` dla miejsca, `VenueId` trzeba ujednolicic. Decyzja: Core Backend.

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

| Pole relacji | Typ | Wymagane | Stan w repo |
|---|---|---|---|
| `TransportMode` | string | tak | jest w seedzie; zapis przez adapter: issue #17 |
| `OriginLatitude` | float | tak | jest w seedzie; zapis przez adapter: issue #17 |
| `OriginLongitude` | float | tak | jest w seedzie; zapis przez adapter: issue #17 |
| `UpdatedAt` | datetime z offsetem | tak | jest w seedzie; zapis przez adapter: issue #17 |

Reguly:

- Para `(User, Event)` ma co najwyzej jedna relacje: zapis przez `MERGE` na relacji nie zwieksza licznika przy ponowieniu.
- Ponowny zapis aktualizuje snapshot, ale nie zwieksza liczby uczestnikow.
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

Unikalnosc relacji `IS_GOING_TO` zapewnia `MERGE`. Zachowanie przy rownoleglych zadaniach dla tej samej pary trzeba potwierdzic testem na prawdziwej instancji Neo4j; jesli `MERGE` nie wystarcza, dodaje sie blokade lub constraint na relacji (o ile dostepny w uzywanej edycji).

## Odczyty potrzebne PULSE

Adapter `IPulseDataReader` odczytuje wewnetrznie tryb transportu i punkt
startu ze snapshotu `IS_GOING_TO`. Agregacja licznika, modal split i siatki
heksagonalnej odbywa sie w C#. Publiczne odpowiedzi nie zawieraja `userId`,
surowych punktow ani komorek z `count < 10`.

## Zmiany do wykonania (podsumowanie)

| # | Zmiana | Wlasciciel |
|---|---|---|
| 1 | Rozbudowa seedu po otrzymaniu realnych danych ze scrapera | Data/Neo4j |
| 2 | Idempotentny importer realnych wydarzen | Data/Neo4j + Backend Events |
| 3 | Interfejsy repozytoriow z Domain do `Application/Abstractions`: decyzja po MVP albo przy pierwszej implementacji repozytorium, nie blokuje MVP | Core Backend Owner + Data/Neo4j |
| 4 | Usuniecie pakietow EF Core/Npgsql z Infrastructure: osobny maly task porzadkowy po potwierdzeniu, ze kod runtime ich nie uzywa | Data/Neo4j (zgoda Core Backend Owner) |
| 5 | Ujednolicenie `DefaultOrigin*` do zaakceptowanych `Home*` oraz zapis snapshotu `IS_GOING_TO`. Schemat i seed zrobione (issue #6); pozostaje `Domain/Models/User.cs` i stary adapter grafu | Data/Neo4j + Core Backend Owner |
| 6 | Implementacja adapterow `IAttendanceRepository` i `IPulseDataReader` dla Neo4j | Data/Neo4j |
