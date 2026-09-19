# Plan pracy MVP FlowBB

Uzupelnia `AGENTS.md` (zasady) i [ADR 001](adr/001-runtime-persistence.md) (baza runtime). Kontrakt danych: [NEO4J_CONTRACT.md](NEO4J_CONTRACT.md).

## 1. Odpowiedzialnosci

| Osoba | Odpowiedzialnosc | Nie robi |
|---|---|---|
| Core Backend Owner (Kuba) | integracja backendu, `Program.cs`, SignalR, Attendance Application/API, PULSE API, `DemoRoutePlanner`, Docker Compose calej aplikacji, kontrakty, przeglad zmian | Events, schemat Neo4j, frontend |
| Backend Events | domena, Application i endpointy Events, implementacja `IEventLookup`, testy kontraktowe Events | Attendance, Neo4j schema |
| Data/Neo4j Owner | usluga Neo4j do Docker Compose, schemat, constraints, seed, Cypher, implementacje repozytoriow `Infrastructure/Neo4j`, konsultacje konfiguracji Neo4j | logika routingu, API, agregacja PULSE |
| Frontend | `/client`, `/dashboard`, klient REST i SignalR | zmiany kontraktu |

Rola integracyjna nalezy do Core Backend Ownera: integracja backendu, `Program.cs`, SignalR, Attendance Application/API, PULSE API, `DemoRoutePlanner`, CORS, health check i Docker Compose na poziomie calej aplikacji. Data/Neo4j Owner przygotowuje usluge Neo4j do Compose i konsultuje jej konfiguracje, a Core Backend Owner wlacza ja do calego Compose. Crew (domena na `feature/crew-domain`) pozostaje u Core Backend Ownera, do czasu wskazania innego wlasciciela.

## 2. Granice modulow

| Modul | Warstwy | Zalezy od |
|---|---|---|
| Events | Domain, Application (`Events/*`), Api (`Endpoints/Events`), Infrastructure/Neo4j (odczyt) | schemat Neo4j |
| Attendance | Application, Api, SignalR (`Hubs`) | `IEventLookup` z Events, adapter Neo4j |
| Crew | Domain (`Crews`), Application (`Crews/*`), Api | schemat Neo4j (`Crew`) |
| PULSE | Application (`Pulse/*`), Api, `Hubs` | odczyty Attendance z Neo4j |
| Routing | Application (`Abstractions/Routing`), Infrastructure (`Routing`) | brak zaleznosci od bazy |

Zasady: agent pracujacy nad Attendance nie implementuje Events (uzywa `IEventLookup`, w testach fake'a). Nie tworzymy produkcyjnego `DemoEventLookup`.

## 3. Zaleznosci miedzy zadaniami

```text
Dokumentacja (ta zmiana) --> Crew Domain (merge)
                       \--> Schemat Neo4j (pola Events + Attendance)
                                 |
                                 +--> Events --> IEventLookup --> Attendance --> SignalR/PULSE --> Frontend dashboard (count + 1)
Routing (DemoRoutePlanner) ---------------------------------------> Frontend /client (trasa)
```

## 4. Kolejnosc integracji i bramki

| # | Bramka | Kryterium akceptacji |
|---|---|---|
| 1 | Dokumentacja i architektura sa spojne, `develop` jest zielony | brak sprzecznych odniesien do bazy w `AGENTS.md`, `START_HERE.md`, `.agents`; `dotnet build` i `dotnet test` przechodza |
| 2 | Crew Domain jest scalone po przejsciu testow | `feature/crew-domain` w `develop`, testy jednostkowe zielone |
| 3 | Testy kontraktowe Events trafiaja na branch wlasciciela Events, a nie osobno na `develop`, jesli sa czerwone | `develop` nie zawiera czerwonych testow; `test/events-contract` scala Backend Events razem z implementacja |
| 4 | Schemat Neo4j ma pola wymagane przez Events i Attendance | zgodnosc z `NEO4J_CONTRACT.md`: pola Event, `IS_GOING_TO` ze snapshotem, `Home*` uzytkownika. Schemat Crew dopiero po zatwierdzeniu propozycji przez wlasciciela Crew i Data/Neo4j |
| 5 | Events dziala i udostepnia stabilny kontrakt dla Attendance | `GET /api/events` i `/{id}` zgodne z OpenAPI; `IEventLookup` dostepny |
| 6 | Attendance jest idempotentne i integruje sie z SignalR | drugi identyczny POST nie zwieksza licznika; DELETE jest idempotentny; `PulseUpdated` po zatwierdzeniu transakcji; test idempotencji na prawdziwej instancji Neo4j |
| 7 | Frontend obsluguje dashboard oraz aktualizacje `count + 1` | klik "Ide" w `/client` zmienia licznik w `/dashboard` bez odswiezania |
| 8 | Routing MVP korzysta z deterministycznego planera demonstracyjnego | `DemoRoutePlanner` dziala bez internetu; brak zaleznosci od danych MZK |
| 9 | PULSE spelnia regule prywatnosci `count >= 10` | test: komorka 9-osobowa ukryta, 10-osobowa zwrocona; brak `userId` i dokladnych wspolrzednych w odpowiedziach |

Bramki 1-3 sa warunkiem startu bramek 4-9. Bramki 5 i 4 mozna prowadzic rownolegle po uzgodnieniu pol Event.

## 5. Zasady rownoleglej pracy Claude i Codex

- Jedna osoba = jeden branch = jeden worktree. Worktree tworzy sie z `origin/develop` obok repozytorium, np. `D:\HACKATON\FlowBB-<zadanie>`.
- Nigdy dwoch piszacych agentow w tym samym katalogu.
- Agent edytuje tylko pliki swojego obszaru. Zmiany w `contracts/`, `Program.cs` i nowych zaleznosciach wymagaja zgody Core Backend.
- Codex moze pisac testy kontraktowe lub review, Claude implementacje; kazdy na osobnym branchu. Testy na innym branchu (np. Events) nie sa zmieniane przez agenta Attendance.
- Zadanie zaczyna sie od sprawdzenia, czy `origin/develop` zawiera zaleznosci (np. Events przed Attendance). Jesli nie, agent zglasza blocker zamiast implementowac cudzy modul.
- Commit lokalny wykonuje agent tylko na wyrazne polecenie; `push`, merge i rebase robi czlowiek.
- Diff czyta czlowiek przed commitem i merge'em.

## 6. Problemy znalezione w kodzie (zadania dla wlascicieli)

Tej zmiany nie robi sie w ramach dokumentacji. Kazda pozycja wymaga osobnego zadania. Priorytety: **MVP** = potrzebne do scenariusza demo, **Porzadki** = maly task po potwierdzeniu, ze nic nie zalezy od starego kodu, **Po MVP** = nie blokuje MVP.

| # | Problem | Priorytet | Wlasciciel |
|---|---|---|---|
| 1 | `FlowBB.Infrastructure.csproj` nadal zawiera `Npgsql.EntityFrameworkCore.PostgreSQL`, `...NetTopologySuite` i `Microsoft.EntityFrameworkCore.*`. Osobny maly task porzadkowy po potwierdzeniu, ze kod runtime ich nie uzywa | Porzadki | Data/Neo4j (zgoda Core Backend Owner) |
| 2 | `IFlowBbGraphRepository` i modele w `FlowBB.Domain/Repositories` i `Models`: interfejs powinien docelowo trafic do `Application/Abstractions/Persistence`. Decyzja po MVP albo przy pierwszej implementacji repozytorium; nie blokuje MVP | Po MVP | Core Backend Owner + Data/Neo4j |
| 3 | `Event` ma `Title` i `DateTime` zamiast `Name` i `StartAt` oraz brak `EndAt`, `Category`, `Source` | MVP | Data/Neo4j + Backend Events |
| 4 | `SetUserGoingToEventAsync` tworzy relacje bez `TransportMode`, wspolrzednych i `UpdatedAt`; brak usuwania i odczytu dla Attendance po stronie Application | MVP | Data/Neo4j |
| 5 | Brak `HomeLatitude` i `HomeLongitude` w `User`. `Email` i `PasswordHash` pozostaja bez zmian: logowanie jest poza zakresem, a pola nie beda uzywane | MVP (Home*) | Data/Neo4j |
| 6 | Identyfikatory w kodzie Neo4j sa `string`, a kontrakt API uzywa `Guid`; konwersja w adapterze | MVP | Data/Neo4j |
| 7 | `Venue.VenueId` to tekstowe slugi, a nie Guid | MVP (decyzja) | Core Backend Owner |
| 8 | Brak seedu z gestymi punktami startowymi uzytkownikow (mapa heksagonalna bylaby pusta przy progu `count >= 10`) | MVP | Data/Neo4j |
| 9 | `PLAN_EVENTS_LOAD.md` opisuje seeder PostGIS/EF Core; wymaga przepisania pod Neo4j przez Backend Events i Data/Neo4j | MVP | Backend Events + Data/Neo4j |
| 10 | Dane MZK nie maja trips, kolejnosci przystankow, powiazania kursow i wspolrzednych; do czasu ich uzupelnienia routing to `DemoRoutePlanner` | Po MVP | Core Backend Owner |
| 11 | Repozytorium nie ma `.env.example`, Docker Compose ani konfiguracji Neo4j dla API (w kodzie: `NEO4J_*` ze zmiennych srodowiskowych). Zadanie Core Backend Ownera wykonywane wspolnie z Data/Neo4j (usluga Neo4j) | MVP | Core Backend Owner + Data/Neo4j |
| 12 | Lokalny `develop` moze zostawac w tyle za `origin/develop`; przed pracami wykonuj `git fetch` | Porzadki | wszyscy |
| 13 | `FlowBB.Domain.Tests` nie ma jeszcze zadnych testow. To luka, nie blad; zostanie uzupelniona razem z pierwsza logika domenowa (np. Crew) | MVP | wlasciciel danej logiki domenowej |

## 7. Weryfikacja ukonczenia

- `dotnet build backend/FlowBB.sln` i `dotnet test backend/FlowBB.sln` przechodza na `develop`.
- Walking skeleton: `/client` -> API -> Neo4j -> SignalR -> `/dashboard` (`+1`).
- Test prywatnosci PULSE (`count >= 10`).
- Analiza Sonar dla zmienionego kodu bez nowych Critical/Blocker/Major.
