# Plan pracy MVP FlowBB

Uzupelnia `AGENTS.md` (zasady) i [ADR 001](adr/001-runtime-persistence.md) (baza runtime). Kontrakt danych: [NEO4J_CONTRACT.md](NEO4J_CONTRACT.md). Backlog prowadzi [epic #23](https://github.com/JDKWss/FlowBB/issues/23), a decyzje o gotowosci MVP zbiera [bramka #93](https://github.com/JDKWss/FlowBB/issues/93).

> **Status implementacji: 2026-09-20, `origin/develop` `dffa9f8`.** Stan ponizej zostal zweryfikowany w kodzie, a nie odziedziczony z historycznych planow. Wyniki uruchomienia stosu sa w [DEMO_RUNBOOK.md](DEMO_RUNBOOK.md).

## 1. Odpowiedzialnosci i wylaczna wlasnosc

| Rola | Odpowiedzialnosc | Wylaczna wlasnosc |
|---|---|---|
| Frontend Owner | Client i Dashboard | `client/**`, `dashboard/**` |
| Backend 1 - Core/Integration (Kuba) | PULSE, Routing, SignalR, konfiguracja aplikacji i Compose | `Program.cs`, PULSE, Routing, SignalR, `infra/docker-compose.yml` |
| Backend 2 - Features/Quality | Events, Crew, OpenAPI, CI, testy black-box i dokumentacja | Events, Crew, `contracts/openapi.yaml`, `.github/workflows/**`, runbook |
| Data/Neo4j Owner | model grafu, adaptery, Cypher, seed i testy na prawdziwym Neo4j | `backend/src/FlowBB.Infrastructure/Neo4j/**`, `database/**`, `docs/NEO4J_CONTRACT.md` |

Kuba pozostaje liderem projektu oraz zatwierdza wspolne kontrakty i nowe zaleznosci. Backend 2 edytuje `contracts/openapi.yaml` dopiero po jego akceptacji. Obszary niewymienione jako wylaczne sa przydzielane w pojedynczych issues bez naruszania tej tabeli.

## 2. Stan implementacji

| Obszar | Stan na `origin/develop` | Dowod w repozytorium |
|---|---|---|
| Composition root | Events, Attendance, PULSE, Routing, Crew, SignalR i Neo4j sa zarejestrowane i zmapowane | `backend/src/FlowBB.Api/Program.cs` |
| Events | lista, szczegoly i tworzenie wydarzenia dzialaja przez porty i adapter Neo4j | `backend/src/FlowBB.Api/Endpoints/Events/`, `backend/src/FlowBB.Infrastructure/Neo4j/Neo4jEventRepository.cs` |
| Attendance | idempotentny upsert/delete, zapis snapshotu i publikacja PULSE sa zaimplementowane | `backend/src/FlowBB.Application/Attendance/`, `backend/src/FlowBB.Api/Endpoints/Events/AttendanceEndpoints.cs` |
| Crew | domena, handlery, endpointy i adapter Neo4j sa podlaczone; join/leave sa idempotentne | `backend/src/FlowBB.Api/Endpoints/Crews/`, `backend/src/FlowBB.Infrastructure/Neo4j/Neo4jCrewRepository.cs` |
| PULSE i SignalR | agregaty, prywatnosc `count >= 10`, hub i `PulseUpdated` sa podlaczone | `backend/src/FlowBB.Application/Pulse/`, `backend/src/FlowBB.Api/Hubs/` |
| Routing | `CompositeRoutePlanner` wybiera RoadRouting dla Walking/Bike/Car, Demo dla PublicTransport i kontrolowanego fallbacku | `backend/src/FlowBB.Infrastructure/Routing/CompositeRoutePlanner.cs` |
| Client | `HttpClientService` jest domyslny; mocki wlacza dopiero `VITE_USE_MOCKS=true` | `client/src/services/clientService.ts`, `client/.env.example` |
| Dashboard | operacyjny widok PULSE korzysta z REST, SignalR i GeoJSON; zawiera tez tworzenie wydarzenia | `dashboard/src/App.tsx`, `dashboard/src/hooks/usePulseConnection.ts`, `dashboard/src/components/PulseMap.tsx` |
| ReturnGap | pozostaje atrapa: `participantsWithoutReturn = 0`, alerty puste, a planery zwracaja `returnGap: false` | `backend/src/FlowBB.Application/Pulse/GetEventPulse/GetEventPulseHandler.cs`, `backend/src/FlowBB.Api/Endpoints/Pulse/PulseResponses.cs`, `backend/src/FlowBB.Infrastructure/Routing/` |
| Dawny stack | pakiety EF Core/Npgsql i stare `Domain/Models` zostaly usuniete (#58, #59) | `backend/src/FlowBB.Infrastructure/FlowBB.Infrastructure.csproj`, brak `backend/src/FlowBB.Domain/Models/` |

Host udostepnia `/health`, `/health/ready`, endpointy wszystkich modulow MVP, `/hubs/pulse` i dokumentacje API w srodowisku Development.

## 3. Zaleznosci i kierunek integracji

```text
OpenAPI (Backend 2, akceptacja Backend 1)
        |
        +--> Client / Dashboard (Frontend)
        +--> Events / Crew (Backend 2)
        +--> PULSE / Routing / SignalR / Program.cs (Backend 1)

Porty Application --> adaptery i model grafu (Data/Neo4j)
Client --> API --> Neo4j --> SignalR --> Dashboard
```

Zmiana wspolnego kontraktu musi byc scalona przed taskami, ktore od niej zaleza. Data implementuje porty persystencji bez przejmowania logiki Application; pozostale role nie modyfikuja Cypher ani seedu.

## 4. Bramki MVP

| Bramka | Stan | Kryterium zamkniecia |
|---|---|---|
| Cztery role i wlasnosc plikow | w trakcie (#83) | `AGENTS.md`, `START_HERE.md`, `.agents/agents.md` i ten plan sa spojne |
| Backend funkcjonalny | wykonane w kodzie | Events, Attendance, Crew, PULSE, Routing i SignalR sa podlaczone do Neo4j |
| Client na prawdziwym API | wykonane w kodzie (#89) | `VITE_USE_MOCKS=false`, pelny przeplyw mieszkanca bez fake'ow |
| Dashboard PULSE | wykonane w kodzie (#87) | REST, SignalR, KPI i mapa heksagonow dzialaja na seedzie demo |
| ReturnGap | otwarte (#84, #85, #86) | wspolna polityka PULSE/Routing, `EndAt` z Neo4j i wydarzenie demonstracyjne |
| Stabilne uruchomienie i CI | otwarte (#80, #88, #90, #91, #92) | zielone workflow, stabilne profile Compose, black-box i testy Neo4j bez pominiec |
| Proba prezentacji | otwarte (#57) | scenariusz wykonany dwa razy z timerem, zapisany backup i wyniki w runbooku |
| Akceptacja MVP | otwarte (#93) | wspolny przebieg Client -> API -> Neo4j -> SignalR -> Dashboard oraz wszystkie kryteria bramki |

PULSE nadal musi ukrywac komorki z `count < 10`, a publiczne odpowiedzi nie moga zawierac `userId` ani dokladnych punktow startowych. Te reguly nie sa odkładane przez otwarte bramki.

## 5. Zasady rownoleglej pracy

- Jedno issue = jeden branch = jeden PR; jedna osoba ma najwyzej jedno aktywne issue.
- Issue zawiera implementacje i wszystkie testy potrzebne do jego ukonczenia.
- Dwa rownolegle taski nie moga modyfikowac tych samych plikow.
- Jedna osoba lub agent pracuje w jednym worktree utworzonym z `origin/develop`.
- `Program.cs` edytuje tylko Backend 1; `contracts/openapi.yaml` tylko Backend 2 po akceptacji Backend 1; Neo4j, Cypher i seed tylko Data; frontend tylko Frontend Owner.
- Zaleznosc nieobecna na `origin/develop` jest blockerem; agent nie implementuje w jej miejsce cudzego modulu.
- Commit, push, merge i rebase wymagaja wyraznego polecenia czlowieka. Diff jest czytany przed integracja.

## 6. Aktualny backlog i ryzyka

Zrodlem pelnej listy i kolejnosci jest [epic #23](https://github.com/JDKWss/FlowBB/issues/23). Najwazniejsze otwarte elementy przed [bramka #93](https://github.com/JDKWss/FlowBB/issues/93):

- ReturnGap nie ma jeszcze logiki biznesowej; `participantsWithoutReturn` pozostaje 0, alerty sa puste, a `returnGap` jest false (#84-#86).
- CI wymaga zielonego formatowania oraz jobow client/dashboard/routing; testy prawdziwego Neo4j maja przechodzic bez pominiec (#80, #88, #92).
- Profile Compose maja zapewnic stabilny tryb demonstracyjny, a krytyczna sciezka wymaga testu black-box (#90, #91).
- Pelny scenariusz trzeba wykonac dwa razy z timerem i przygotowac material zapasowy (#57).
- Dane MZK nadal nie sa kompletnym grafem transportu publicznego; PublicTransport pozostaje deterministycznym Demo.

### Rozwiazane i niebedace juz blockerami

- Crew jest zarejestrowane w `Program.cs` i ma adapter Neo4j.
- Client domyslnie korzysta z `HttpClientService`; tryb mock jest jawnie opcjonalny.
- Dashboard przestal byc szkieletem: ma widok PULSE, mape GeoJSON i klienta SignalR (#87).
- Routing korzysta z `CompositeRoutePlanner`; niedostepnosc uslugi drogowej moze przejsc na jawny planner Demo.
- Nieuzywane pakiety EF Core/Npgsql usunieto (#58), a szerokie stare repozytorium grafu i `Domain/Models` usunieto (#59).
- Adaptery Events, Attendance, PULSE i Crew oraz readiness Neo4j sa podlaczone.

## 7. Weryfikacja ukonczenia

- [Bramka #93](https://github.com/JDKWss/FlowBB/issues/93) jest zamknieta na podstawie wspolnego przebiegu, nie samej obecnosci kodu.
- `dotnet build backend/FlowBB.sln`, `dotnet test backend/FlowBB.sln` i workflow CI przechodza.
- `npm run lint` i `npm run build` przechodza w `client/` i `dashboard/`.
- Testy adapterow wykonuja sie na prawdziwym Neo4j bez pominiec.
- Walking skeleton `/client` -> API -> Neo4j -> SignalR -> `/dashboard` pokazuje `+1`.
- Test prywatnosci PULSE potwierdza prog `count >= 10` i brak danych indywidualnych.
- Scenariusz demonstracyjny zostal wykonany dwa razy z timerem, a wynik i backup zapisano zgodnie z runbookiem.
