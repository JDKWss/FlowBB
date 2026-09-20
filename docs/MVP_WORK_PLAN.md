# Plan pracy MVP FlowBB

Uzupelnia `AGENTS.md` (zasady) i [ADR 001](adr/001-runtime-persistence.md) (baza runtime). Kontrakt danych: [NEO4J_CONTRACT.md](NEO4J_CONTRACT.md).

> **Status implementacji: 2026-09-20 (`develop`).** Ten dokument zachowuje
> role, kolejnosc prac i bramki MVP. Tabele statusowe ponizej odrozniaja kod
> istniejacy w repozytorium od modulow zarejestrowanych w uruchamianym API.

## 1. Odpowiedzialnosci

| Osoba | Odpowiedzialnosc | Nie robi |
|---|---|---|
| Core Backend Owner (Kuba) | integracja backendu, `Program.cs`, SignalR, Attendance Application/API, PULSE API, `IRoutePlanner` i `DemoRoutePlanner`, integracja proponowanej prywatnej uslugi routingu, Docker Compose calej aplikacji, kontrakty, przeglad zmian | Events, schemat Neo4j, frontend |
| Backend Events | domena, Application i endpointy Events, implementacja `IEventLookup`, testy kontraktowe Events | Attendance, Neo4j schema |
| Data/Neo4j Owner | usluga Neo4j do Docker Compose, schemat, constraints, seed, Cypher, implementacje repozytoriow `Infrastructure/Neo4j`, konsultacje konfiguracji Neo4j | logika routingu, API, agregacja PULSE |
| Frontend | `/client`, `/dashboard`, klient REST i SignalR | zmiany kontraktu |

Rola integracyjna nalezy do Core Backend Ownera: integracja backendu, `Program.cs`, SignalR, Attendance Application/API, PULSE API, `DemoRoutePlanner`, CORS, health check i Docker Compose na poziomie calej aplikacji. Data/Neo4j Owner przygotowuje usluge Neo4j do Compose i konsultuje jej konfiguracje, a Core Backend Owner wlacza ja do calego Compose. Crew (domena na `feature/crew-domain`) pozostaje u Core Backend Ownera, do czasu wskazania innego wlasciciela.

## 2. Granice modulow i stan implementacji

| Modul | Docelowe warstwy | Stan na `develop` 2026-09-20 | Podpiecie w `Program.cs` |
|---|---|---|---|
| Events | Domain, Application (`Events/*`), Api (`Endpoints/Events`), Infrastructure/Neo4j (odczyt) | domena, `IEventLookup`, endpointy i adapter Neo4j sa zaimplementowane | tak |
| Attendance | Application, Api, SignalR (`Hubs`) | encja, handlery, endpointy, testy i adapter `IAttendanceRepository` dla Neo4j sa zaimplementowane | tak |
| Crew | Domain (`Crews`), Application (`Crews/*`), Api, Infrastructure/Neo4j | domena, handlery, endpointy (`GET groups`, `POST/DELETE members`) i adapter Neo4j sa zaimplementowane | tak |
| PULSE | Application (`Pulse/*`), Api, `Hubs` | handlery agregacji, endpointy, testy i adapter `IPulseDataReader` dla Neo4j sa zaimplementowane | tak |
| Routing | Domain (`Routing`), Application (`Abstractions/Routing`, `Routing`), Infrastructure (`Routing`), Api (`Endpoints/Routing`); prywatna usluga Python/FastAPI | `IRoutePlanner`, `DemoRoutePlanner`, handler i endpoint sa podlaczone; FastAPI, generator grafow, Compose i wewnetrzny klient HTTP sa zaimplementowane jako spike. Publiczne wlaczenie realnego planera blokuje brak prawdziwego `PlannerSource`, dystansu i geometrii w zaakceptowanym kontrakcie | tak, nadal `DemoRoutePlanner` |
| SignalR | Api (`Hubs`) | hub i notifier istnieja; `/hubs/pulse` jest mapowany | tak |

Uruchamiany host mapuje `/health`, `/health/ready`, Events, Attendance, Crew, PULSE, Routing,
`/hubs/pulse` oraz dokumentacje API w srodowisku Development. Start calego stosu i smoke test:
[`DEMO_RUNBOOK.md`](DEMO_RUNBOOK.md), sekcja 10.

Zasady: agent pracujacy nad Attendance nie implementuje Events (uzywa `IEventLookup`, w testach fake'a). Nie tworzymy produkcyjnego `DemoEventLookup`.

## 3. Zaleznosci miedzy zadaniami (plan integracji)

```text
Dokumentacja (ta zmiana) --> Crew Domain (merge)
                       \--> Schemat Neo4j (pola Events + Attendance)
                                 |
                                 +--> Events --> IEventLookup --> Attendance --> SignalR/PULSE --> Frontend dashboard (count + 1)
Routing (DemoRoutePlanner) ---------------------------------------> Frontend /client (trasa)
```

Docelowy realny routing drogowy zachowuje te sama granice Application:
`IRoutePlanner -> RoutingServiceRoutePlanner -> prywatny REST -> FastAPI ->
graf OSM`. Przegladarka nadal wywoluje tylko FlowBB.Api. PublicTransport nie
wchodzi do tej uslugi. Projekt i bramka spike'a sa w
[`ROUTING_SERVICE.md`](ROUTING_SERVICE.md) oraz proponowanym ADR 002.

## 4. Kolejnosc integracji i bramki

Ponizsza tabela definiuje kryteria bramek, a nie deklaruje ich ukonczenia.

| # | Bramka | Kryterium akceptacji |
|---|---|---|
| 1 | Dokumentacja i architektura sa spojne, `develop` jest zielony | brak sprzecznych odniesien do bazy w `AGENTS.md`, `START_HERE.md`, `.agents`; `dotnet build` i `dotnet test` przechodza |
| 2 | Crew Domain jest scalone po przejsciu testow | `feature/crew-domain` w `develop`, testy jednostkowe zielone |
| 3 | Testy kontraktowe Events trafiaja na branch wlasciciela Events, a nie osobno na `develop`, jesli sa czerwone | `develop` nie zawiera czerwonych testow; `test/events-contract` scala Backend Events razem z implementacja |
| 4 | Schemat Neo4j ma pola wymagane przez Events i Attendance | zgodnosc z `NEO4J_CONTRACT.md`: pola Event, `IS_GOING_TO` ze snapshotem, `DefaultOrigin*` uzytkownika. Schemat Crew dopiero po zatwierdzeniu propozycji przez wlasciciela Crew i Data/Neo4j |
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

## 6. Aktualne problemy znalezione w kodzie (zadania dla wlascicieli)

Tej zmiany nie robi sie w ramach dokumentacji. Kazda pozycja wymaga osobnego zadania. Priorytety: **MVP** = potrzebne do scenariusza demo, **Porzadki** = maly task po potwierdzeniu, ze nic nie zalezy od starego kodu, **Po MVP** = nie blokuje MVP.

| # | Problem | Priorytet | Wlasciciel |
|---|---|---|---|
| 1 | `FlowBB.Infrastructure.csproj` nadal zawiera `Npgsql.EntityFrameworkCore.PostgreSQL`, `...NetTopologySuite` i `Microsoft.EntityFrameworkCore.*`. Osobny maly task porzadkowy po potwierdzeniu, ze kod runtime ich nie uzywa | Porzadki | Data/Neo4j (zgoda Core Backend Owner) |
| 2 | `IFlowBbGraphRepository` i modele w `FlowBB.Domain/Repositories` i `Models`: interfejs powinien docelowo trafic do `Application/Abstractions/Persistence`. Decyzja po MVP albo przy pierwszej implementacji repozytorium; nie blokuje MVP | Po MVP | Core Backend Owner + Data/Neo4j |
| 3 | Stary model `FlowBB.Domain.Models.Event` pozostaje do posprzatania po MVP | Porzadki | Data/Neo4j + Backend Events |
| 4 | `PLAN_EVENTS_LOAD.md` zachowuje historyczny plan PostGIS/EF Core; aktualny importer wydarzen do Neo4j nie istnieje | MVP | Backend Events + Data/Neo4j |
| 5 | Dane MZK nie maja pelnych trips, kolejnosci przystankow, powiazania kursow i wszystkich wspolrzednych; nie sa grafem routingu | Po MVP | Core Backend Owner |
| 6 | Seed demo zapisuje wszystkich 82 uzytkownikow (w tym `aaaaaaaa-...` z klienta) na wydarzenie `1111...`: nikt nie moze dac `82 -> 83`. Uzytkownik `dddddddd-...` z dawnego runbooka istnieje tylko w `flowbb-queries.cypher`. Wymaga uzytkownika demo spoza wydarzenia i zgodnego `DEMO_USER_ID` w kliencie | MVP (#72) | Data/Neo4j + Frontend |
| 7 | Kontener `routing` jest `unhealthy` bez recznego `routing-prepare` (brak grafow). Nie blokuje API; do decyzji, czy Compose ma go pomijac w trybie demo | Porzadki | Core Backend Owner |

### Rozwiazane od utworzenia planu

- Crew: domena, Application, endpointy i adapter Neo4j sa zarejestrowane w API (#54).
- `/health` i `/health/ready` (Neo4j) zgodne z `HealthResponse` z OpenAPI (#55, #52).
- Compose przekazuje `NEO4J_SEED_ON_STARTUP` do `api`, a API czeka na zdrowy lokalny Neo4j (#57).
- Zweryfikowane: start od czystego srodowiska, seed, restart API i `infra/smoke-test.ps1` (13 PASS, 0 FAIL, 0 SKIP)
  na lokalnym Neo4j; wynik w `DEMO_RUNBOOK.md`, sekcja 10.

- `Event` uzywa `Name`, `StartAt`, opcjonalnego `EndAt` oraz kanonicznych
  `Category` i `Source`.
- `User` uzywa `DefaultOriginLatitude/Longitude`, a `IS_GOING_TO` przechowuje
  pelny snapshot wymagany przez Attendance, PULSE i Routing.
- Adaptery Events, Attendance i PULSE dla Neo4j sa zarejestrowane w API.
- Seed zawiera 82 syntetycznych uzytkownikow w gestych obszarach i daje
  widoczne komorki PULSE przy progu `count >= 10`.
- `VenueId` pozostaje zaakceptowanym tekstowym slugiem.
- Adapter ogolnego grafu konwertuje identyfikatory wezlow `Guid` do/z tekstu Neo4j.
- Repozytorium zawiera `.env.example`, `infra/docker-compose.yml` i
  `infra/smoke-test.ps1`.
- `FlowBB.Domain.Tests`, `FlowBB.Application.Tests` i
  `FlowBB.Api.IntegrationTests` zawieraja testy.

## 7. Weryfikacja ukonczenia

- `dotnet build backend/FlowBB.sln` i `dotnet test backend/FlowBB.sln` przechodza na `develop`.
- Walking skeleton: `/client` -> API -> Neo4j -> SignalR -> `/dashboard` (`+1`).
- Test prywatnosci PULSE (`count >= 10`).
- Analiza Sonar dla zmienionego kodu bez nowych Critical/Blocker/Major.
