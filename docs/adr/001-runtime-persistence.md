# ADR 001: Baza runtime aplikacji FlowBB

Status: przyjety (MVP hackathonowe)
Data: 2026-09-19
Wlasciciel decyzji: Core Backend (Kuba)

## Context

Poczatkowe instrukcje (`AGENTS.md`, `START_HERE.md`, `PLAN_EVENTS_LOAD.md`) zakladaly PostgreSQL + PostGIS z EF Core, migracjami i `ST_HexagonGrid`. Rownolegle w repozytorium powstaly:

- implementacja Neo4j: `Neo4j.Driver`, `Neo4jFlowBbGraphRepository`, model wezlow i relacji, schemat oraz seed w `database/flowbb-queries.cypher`;
- odseparowany PoC pipeline'u rozkladow MZK, ktory laduje odjazdy do PostGIS (`data/gtfs/mzk/`).

W efekcie dokumentacja opisywala dwie rozne bazy jako glowna. Agenci (Claude Code, Codex) i czlonkowie zespolu dostawali sprzeczne instrukcje, a `AGENTS.md` jednoczesnie wskazywal PostgreSQL jako baze i Neo4j jako element poza zakresem.

Dane FlowBB maja charakter grafowy (uzytkownik -> wydarzenie -> miejsce, uzytkownik -> grupa), a zakres MVP jest maly: kilkadziesiat wydarzen i syntetyczni uzytkownicy.

## Decision

1. **Neo4j jest jedyna baza runtime aplikacji w MVP.** Przechowuje Events, Attendance, Crew oraz dane zrodlowe do agregacji PULSE.
2. **Attendance** to relacja `(User)-[:IS_GOING_TO]->(Event)` ze snapshotem `TransportMode`, `OriginLatitude`, `OriginLongitude`, `UpdatedAt`. Uzytkownik ma wewnetrzne, demonstracyjne `DefaultOriginLatitude` i `DefaultOriginLongitude`. Wspolrzedne nie sa czescia publicznego API.
3. **PULSE** jest agregowany w backendzie C# na podstawie wspolrzednych pobranych wewnetrznie z Neo4j. Frontend dostaje tylko zagregowane komorki. Nie zwracamy `userId`, dokladnych wspolrzednych ani komorek z `count < 10`.
4. **PostgreSQL/PostGIS pozostaje wylacznie odseparowanym PoC** w `data/gtfs/mzk/`. Nie jest baza aplikacji, nie przechowuje Events ani Attendance i nie jest zaleznoscia backendu.
5. **Routing MVP** korzysta z deterministycznego `DemoRoutePlanner`. Obecne dane MZK to odjazdy z przystankow, bez pelnych trips, kolejnosci przystankow, kompletnego powiazania kursow i wszystkich wspolrzednych, wiec nie wystarczaja do planowania podrozy.
6. Na granicy Application/API identyfikatory sa typem `Guid`. Adapter Neo4j moze je przechowywac jako string i odpowiada za konwersje.

## Consequences

Pozytywne:

- jedna baza i jedno zrodlo prawdy dla danych aplikacji, bez synchronizacji;
- prostszy model dla relacji uzytkownik-wydarzenie-grupa;
- istniejacy kod i seed Neo4j sa wykorzystane, a nie porzucone;
- jasna instrukcja dla agentow.

Negatywne i ryzyka:

- agregacja przestrzenna (siatka heksagonalna, `count >= 10`) musi byc zaimplementowana w C#, bez gotowego `ST_HexagonGrid`;
- Attendance wymaga transakcji obejmujacej zapis relacji i odczyt danych do `PulseUpdated`, a wiadomosc musi byc publikowana dopiero po zatwierdzeniu;
- idempotencja opiera sie na `MERGE` i constraints wezlow. Zachowanie przy rownoleglych zadaniach trzeba zweryfikowac testem na prawdziwej instancji Neo4j;
- pakiety EF Core i Npgsql nadal sa w `FlowBB.Infrastructure.csproj` i wymagaja usuniecia (zadanie Data/Neo4j);
- czesc dokumentacji historycznej (`PLAN_EVENTS_LOAD.md`, dokumentacja pipeline'u MZK) opisuje PostgreSQL. Jest oznaczona jako historyczna lub PoC.

## Rejected alternatives

| Alternatywa | Powod odrzucenia |
|---|---|
| PostgreSQL + PostGIS jako glowna baza (pierwotny plan) | Kod runtime powstal juz w Neo4j; przepisanie kosztowaloby czas potrzebny na P0. |
| Dwie bazy runtime (Neo4j dla grafu, PostGIS dla PULSE) | Wymaga synchronizacji i podwaja miejsca bledow; zagraza scenariuszowi P0. |
| PostGIS tylko do agregacji, zasilany z Neo4j | Jak wyzej; dodatkowa infrastruktura bez korzysci dla demo. |
| Neo4j Spatial lub Cypher `point()` do siatki heksagonalnej | Zaleznosc od funkcji bazy trudna do testowania jednostkowo; agregacja w C# jest testowalna i niezalezna od bazy. |

## Granice: Neo4j i PostGIS

| Obszar | Neo4j | PostGIS PoC (`data/gtfs/mzk/`) |
|---|---|---|
| Events, Attendance, Crew | tak | nie |
| Dane uzytkownikow demo | tak | nie |
| Dane zrodlowe PULSE | tak | nie |
| Odjazdy i przystanki MZK | nie (na razie) | tak, PoC |
| Uruchamiany przez backend | tak | nie (osobny `docker compose` w katalogu PoC) |
| Synchronizacja miedzy nimi | brak | brak |

Ewentualne uzycie danych MZK w aplikacji wymaga osobnej decyzji: dane trzeba wtedy dostarczyc do backendu wprost (np. jako plik JSON), a nie przez druga baze.

## Brak synchronizacji

Nie istnieje i nie bedzie budowany zaden mechanizm synchronizacji Neo4j z PostgreSQL. Jesli w przyszlosci potrzebne beda dane MZK w aplikacji, wymaga to nowego ADR.
