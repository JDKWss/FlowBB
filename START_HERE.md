# Jak wdrozyc konfiguracje agentow FlowBB

> **Charakter dokumentu:** bootstrap/onboarding. Kroki ponizej opisuja
> poczatkowe przygotowanie repozytorium i podzial pracy, a nie biezacy status
> implementacji. Stan kodu na `develop` z 2026-09-20 opisuje
> [docs/MVP_WORK_PLAN.md](docs/MVP_WORK_PLAN.md), a uruchamialnosc scenariusza
> [docs/DEMO_RUNBOOK.md](docs/DEMO_RUNBOOK.md).

## 1. Skopiuj pliki do roota repozytorium

```text
flowbb/
|-- AGENTS.md
|-- CLAUDE.md
|-- .gitignore
|-- contracts/
|   `-- openapi.yaml
`-- .agents/
    `-- agents.md
```

- Codex: czyta `AGENTS.md` z roota repozytorium.
- Claude Code: `CLAUDE.md` importuje `AGENTS.md` przez `@AGENTS.md`.
- Antigravity: `.agents/agents.md` definiuje persony i nakazuje im odczyt rootowego `AGENTS.md`.

## 2. Uzupelnij dane zespolu

W `AGENTS.md` pozostawiono role zamiast imion poza Kuba. Wpisz imiona pozostalych osob dopiero, gdy potwierdzicie odpowiedzialnosci.

## 3. Poczatkowe przygotowanie fixture'ow

Ten krok byl przewidziany przed rozpoczeciem implementacji. `contracts/openapi.yaml`
pozostaje zrodlem prawdy; fixture'y nalezy utrzymywac zgodnie z jego aktualnym
ksztaltem:

```text
contracts/openapi.yaml
contracts/fixtures/events.json
contracts/fixtures/groups.json
contracts/fixtures/route.json
contracts/fixtures/pulse-summary.json
contracts/fixtures/pulse-hexagons.geojson
```

Kuba zatwierdza kontrakty. Frontend od razu korzysta z fixture'ow, a backend implementuje te same ksztalty odpowiedzi.

## 4. Zweryfikuj ladowanie instrukcji

### Codex

Uruchom nowa sesje w roocie repo i popros:

```text
Wymien aktywne instrukcje projektu, moja role, zamrozony stack i krytyczny scenariusz demo. Niczego nie edytuj.
```

### Claude Code

Uruchom `/context` i sprawdz, czy zaladowano `CLAUDE.md` oraz import `AGENTS.md`. Nastepnie uzyj tego samego promptu kontrolnego.

### Antigravity

Otworz repo jako workspace. W zadaniu wybierz wlasciwa persone z `.agents/agents.md` i rozpocznij od:

```text
Przeczytaj AGENTS.md i contracts/. Potwierdz granice roli @nazwa-roli. Nie edytuj plikow, dopoki nie podasz planu i testu akceptacyjnego.
```

## 5. Przypisania implementacyjne

| Rola / persona | Odpowiedzialnosc | Wylaczna wlasnosc |
|---|---|---|
| Frontend Owner (`@frontend`) | Client i Dashboard | `client/**`, `dashboard/**` |
| Backend 1 - Core/Integration (`@backend-core`, Kuba) | PULSE, Routing, SignalR, konfiguracja aplikacji i Compose | `Program.cs`, PULSE, Routing, SignalR, `infra/docker-compose.yml` |
| Backend 2 - Features/Quality (`@backend-features`) | Events, Crew, OpenAPI, CI, testy black-box i dokumentacja | Events, Crew, `contracts/openapi.yaml`, `.github/workflows/**`, runbook |
| Data/Neo4j Owner (`@data`) | model grafu, adaptery, Cypher, seed i testy na prawdziwym Neo4j | `backend/src/FlowBB.Infrastructure/Neo4j/**`, `database/**`, `docs/NEO4J_CONTRACT.md` |

Kuba pozostaje liderem projektu oraz zatwierdza kontrakty i nowe zaleznosci. `contracts/openapi.yaml` edytuje Backend 2 po jego akceptacji.

Pierwsza wspolna bramka: przegladarka `/client` -> API -> Neo4j -> SignalR -> dashboard.

Baza runtime to Neo4j ([ADR 001](docs/adr/001-runtime-persistence.md)); PostgreSQL/PostGIS w `data/gtfs/mzk/` to odseparowany PoC. Kolejnosc prac i bramki: [docs/MVP_WORK_PLAN.md](docs/MVP_WORK_PLAN.md). Realny routing drogowy dziala jako prywatna usluga Python/FastAPI wywolywana przez ASP.NET i jest opisany w [docs/ROUTING_SERVICE.md](docs/ROUTING_SERVICE.md); decyzja produkcyjna nadal ma status `Proposed` ([ADR 002](docs/adr/002-road-routing-engine.md)).

## 6. Bezpieczna praca rownolegla

- Jedno issue = jedna galaz = jeden PR; jedna osoba ma najwyzej jedno aktywne issue.
- Testy potrzebne do ukonczenia funkcji sa czescia tego samego issue.
- Dwa rownolegle zadania nie moga modyfikowac tych samych plikow.
- Zmiana wspolnego kontraktu musi zostac scalona przed zalezna implementacja.
- Jedna osoba = jeden worktree dla aktywnego zadania.
- Agent moze edytowac tylko worktree wlasciciela zadania.
- Czlowiek przeglada diff i wykonuje commit.
- Integrujcie po malych pionowych fragmentach, nie dopiero na koniec.
- Nie uruchamiajcie kilku autonomicznych agentow z prawem zapisu w tym samym katalogu.
