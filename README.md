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

Dokumenty planistyczne i ADR-y moga zachowywac kontekst historyczny. Biezacy
stan implementacji zawsze nalezy weryfikowac w kodzie na `develop`.
