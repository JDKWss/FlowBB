# FlowBB Frontend

FlowBB has two independent React + Vite + TypeScript applications.

## Client

Path: `/client`

Purpose:

- resident-facing application
- mobile-first web UI
- designed primarily for a phone-sized browser viewport

Main flow:

```text
Events
→ Event details
→ choose transport
→ "I'm going"
→ Route
→ Crew
```

API rules:

- `/contracts/openapi.yaml` is the source of truth for API paths, DTOs, enums and response shapes
- do not invent new API fields or endpoints
- use fixtures from `/contracts/fixtures` before the backend is ready

Before adding a new dependency:

- inspect `/client/package.json`
- reuse existing libraries whenever possible

Important currently installed client libraries:

- React
- Vite
- TypeScript
- Tailwind CSS
- TanStack Query
- Zustand
- Zod
- React Hook Form
- @microsoft/signalr
- Motion
- Lucide React

## Dashboard

Path: `/dashboard`

Purpose:

- city/admin PULSE interface
- desktop-oriented web UI

Main responsibilities:

- event selector
- KPI cards
- transport modal split
- return-gap alerts
- demand map / hexagons
- live SignalR updates

API rules:

- `/contracts/openapi.yaml` is the source of truth
- do not invent DTO fields or endpoints
- use fixtures from `/contracts/fixtures` before the backend is ready

Before adding a new dependency:

- inspect `/dashboard/package.json`
- reuse existing libraries whenever possible

Important currently installed dashboard libraries:

- React
- Vite
- TypeScript
- Tailwind CSS
- TanStack Query
- Zod
- @microsoft/signalr
- Recharts
- deck.gl
- Lucide React

## Shared frontend rules

- Client and dashboard should use a visually consistent FlowBB design language.
- Prefer simple, reliable UI over unnecessary complexity.
- Keep frontend work aligned with the P0 demo flow from `AGENTS.md`.
- Do not add new dependencies when the current stack already solves the problem.
- Do not duplicate backend/domain models independently from `contracts/openapi.yaml`.
- Fixture data should follow exactly the API contract shapes.
- Handle loading, error and empty states where they are visible during the demo.
