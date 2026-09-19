# Jak wdrozyc konfiguracje agentow FlowBB

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

## 3. Uzupelnij fixture'y przed kodem

Plik `contracts/openapi.yaml` jest juz przygotowany. Przed rozpoczeciem implementacji zatwierdzcie go wspolnie i dodajcie fixture'y:

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

## 5. Pierwsze przypisania

1. Kuba + `@core-backend`: minimalne API Attendance i `PulseUpdated`.
2. Drugi programista C# + `@integration-backend`: Docker Compose, health check i `DemoRoutePlanner`.
3. Frontend Lead + `@frontend`: pusty dashboard z klientem SignalR oraz proste trzy ekrany Expo na fixture'ach.
4. Data Lead + `@data`: PostGIS, pierwsza migracja i seed wystarczajacy do widocznych heksagonow.

Pierwsza wspolna bramka: fizyczny telefon -> POST Attendance -> PostgreSQL -> SignalR -> licznik dashboardu `+1`.

## 6. Bezpieczna praca rownolegla

- Jedna osoba = jedna galaz i jeden worktree.
- Agent moze edytowac tylko worktree wlasciciela zadania.
- Czlowiek przeglada diff i wykonuje commit.
- Integrujcie po malych pionowych fragmentach, nie dopiero na koniec.
- Nie uruchamiajcie kilku autonomicznych agentow z prawem zapisu w tym samym katalogu.
