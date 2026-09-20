# FlowBB

FlowBB to prototyp HackBB 2026 laczacy wybor wydarzenia i dojazdu (FLOW),
bezpieczne mikrogrupy uczestnikow (CREW) oraz anonimowe sygnaly przyszlego
popytu transportowego dla miasta (PULSE).

## Orientacja w repozytorium

- [AGENTS.md](AGENTS.md) — kanoniczne zasady projektu, architektura i granice odpowiedzialnosci.
- [START_HERE.md](START_HERE.md) — instrukcja bootstrap/onboarding dla agentow i zespolu.
- [contracts/openapi.yaml](contracts/openapi.yaml) — kanoniczny kontrakt HTTP i DTO.
- [docs/MVP_WORK_PLAN.md](docs/MVP_WORK_PLAN.md) — plan MVP oraz datowany status implementacji.
- [docs/DEMO_RUNBOOK.md](docs/DEMO_RUNBOOK.md) — biezacy stan uruchomienia i scenariusz demo.
- [ADR 001](docs/adr/001-runtime-persistence.md) — przyjeta decyzja o Neo4j jako jedynej bazie runtime; PostGIS pozostaje odseparowanym PoC MZK.
- [docs/ROUTING_SERVICE.md](docs/ROUTING_SERVICE.md) — prywatny routing drogowy Walking/Bike/Car, aktywowany przez publiczne API z kontrolowanym fallbackiem demo.
- [docs/AIR_QUALITY.md](docs/AIR_QUALITY.md) — kontrakt, polityki i zweryfikowane warunki integracji Air Quality z GIOŚ.
- [routing-service/README.md](routing-service/README.md) — przygotowanie grafow, uruchomienie i testy prywatnej uslugi FastAPI.
- [dashboard/README.md](dashboard/README.md) — uruchomienie desktopowego PULSE i lekkiego przeplywu `Add event`.
- [ADR 002](docs/adr/002-road-routing-engine.md) — prywatna usluga Python/FastAPI dla routingu Walking/Bike/Car; status `Accepted for the MVP` (produkcyjna akceptacja otwarta).

Dokumenty planistyczne i ADR-y moga zachowywac kontekst historyczny. Biezacy
stan implementacji zawsze nalezy weryfikowac w kodzie na `develop`.
