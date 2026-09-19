FlowBB Frontend
This document is mandatory reading for any task touching /client or /dashboard.
It supplements AGENTS.md.
contracts/openapi.yaml remains the source of truth for API paths, DTOs, enums, and response shapes.
Frontend architecture
FlowBB has two independent React + Vite + TypeScript applications:

- /client — resident-facing, mobile-first web application
- /dashboard — city/admin PULSE dashboard, desktop-oriented
  Both applications should share one coherent FlowBB visual language, but they have different UX goals.
  Client
  Path: /client
  Purpose
  Resident-facing application designed primarily for a phone-sized browser viewport.
  Main flow:
  Events
  → Event details
  → choose transport
  → "I'm going"
  → Route
  → Crew
  The critical client demo must remain simple, fast, and reliable.
  Client stack
  Core:
- React
- Vite
- TypeScript
- Tailwind CSS
- @tailwindcss/vite
  UI / interaction:
- shadcn/ui — preferred source of reusable UI primitives when installed/configured
- Radix UI — underlying accessible primitives used by shadcn/ui where applicable
- Lucide React — icons
- Motion — restrained animations and interaction feedback
  Data / state:
- TanStack Query — server-state fetching, caching, mutations
- Zustand — only for genuinely shared client-side state
- Zod — runtime validation of API/fixture data
  Forms:
- React Hook Form — forms with meaningful validation/state
- @hookform/resolvers — connect React Hook Form with Zod when needed
  Realtime:
- @microsoft/signalr — live updates where required by the product flow
  Current client dependency rule
  Before adding or using a dependency:

1. inspect /client/package.json
2. check whether the required capability already exists
3. reuse an already-installed library where practical
4. do not force a library into the implementation just because it is installed
5. do not add a new dependency without explicit justification
   If shadcn/ui or Radix UI are not currently installed/configured, do not silently install them. Report that first.
   Client UI expectations
   Use shared UI primitives instead of repeating large one-off Tailwind class sets.
   Prefer consistent components for:

- Button
- Card
- Badge
- status / alert panel
- loading state
- empty state
- error state
- progress / journey steps
- transport option
- Crew card
  Use Lucide icons instead of improvised text symbols where an icon communicates the state better.
  Use Motion only where it improves the demo:
- subtle page/screen transitions
- event-card entrance
- transport-selection feedback
- attendance success feedback
- route section reveal
- Crew join/leave feedback
  Respect reduced-motion preferences.
  Do not turn animation into a requirement for the application to function.
  Dashboard
  Path: /dashboard
  Purpose
  Desktop-oriented city/admin interface for PULSE.
  Main responsibilities:
- event selector
- KPI cards
- participant/demand counts
- transport modal split
- return-gap alerts
- demand map / hexagons
- charts
- live SignalR updates
  Dashboard stack
  Core:
- React
- Vite
- TypeScript
- Tailwind CSS
- @tailwindcss/vite
  UI:
- shadcn/ui — preferred reusable UI primitives when installed/configured
- Radix UI — accessible primitives used by shadcn/ui where applicable
- Lucide React — icons
  Data / validation:
- TanStack Query — API fetching, caching, mutations
- Zod — runtime validation of API/fixture data
  Realtime:
- @microsoft/signalr — PulseUpdated and other approved live updates
  Charts / visualisation:
- Recharts — KPI/detail charts
- deck.gl
- @deck.gl/react — geospatial overlays / demand visualisation
  Maps:
- MapLibre GL JS — preferred map renderer if/when installed and approved
- OpenFreeMap — preferred map tile/style source for the hackathon map
- deck.gl — overlay for demand/hexagon layers
  MapLibre is optional and must not become a blocker for P0.
  Current dashboard dependency rule
  Before adding or using a dependency:

1. inspect /dashboard/package.json
2. reuse an already-installed library when possible
3. do not install a new library just because it appears in this document
4. if a listed preferred library is not installed, report it before changing dependencies
   API and contract rules
   For both applications:

- /contracts/openapi.yaml is the source of truth
- do not invent DTO fields
- do not invent endpoint paths
- do not silently rename enums
- do not create a second incompatible frontend domain model
- fixture data must match the OpenAPI contract exactly
  Before the backend is ready, use fixtures from:
  /contracts/fixtures/
  Recommended fixture set:
  events.json
  event-details.json
  groups.json
  route.json
  pulse-summary.json
  pulse-event.json
  pulse-hexagons.geojson
  If a required fixture is missing, a temporary frontend mock may be used only if it matches the OpenAPI shape exactly.
  Shared frontend rules
  Design
  Client and dashboard should use a visually consistent FlowBB design language:
- consistent spacing
- consistent radius scale
- consistent typography
- consistent colors
- consistent button hierarchy
- consistent status colors
- consistent loading/error/empty patterns
  Do not create a different design system independently in each feature.
  Simplicity
  Prefer:
- reliable UI
- obvious user actions
- reusable primitives
- small components with clear responsibility
- straightforward state flow
  Avoid:
- unnecessary abstractions
- premature architecture
- complex state management for local screen state
- animation-heavy interactions
- extra dependencies for problems already solved by the current stack
  Demo-data rule
  Synthetic/demo content must be clearly marked in the UI as:
  DEMO DATA / SYMULACJA
  P0 priority
  Frontend work must support the critical FlowBB demo:
  CLIENT
  open event
  → choose transport
  → "I'm going"
  → attendance saved

BACKEND
→ PostgreSQL
→ aggregate recalculated
→ SignalR PulseUpdated

DASHBOARD
→ KPI changes live without refresh
If a frontend change does not help the critical demo, reliability, or clarity, it is not P0.
Library usage guide for AI agents
Use the library because it solves a real problem, not because it exists.
Library Use it for Do not use it for
Tailwind CSS layout, spacing, responsive styling, visual tokens duplicating huge one-off style blocks everywhere
shadcn/ui reusable polished UI primitives blindly installing every component
Radix UI accessible low-level primitives rebuilding components already provided by shadcn/ui
Lucide React interface icons decoration with no semantic value
Motion subtle transitions and feedback making the app dependent on animation
TanStack Query API/async server state simple local UI state
Zustand genuinely shared client state state local to one component/view
Zod validating API/fixture boundaries decorative typing already guaranteed internally
React Hook Form non-trivial forms simple button/selection state
@hookform/resolvers RHF + Zod integration use without a form/schema need
@microsoft/signalr approved realtime flows mock-only UI polish
Recharts dashboard charts maps
deck.gl geospatial overlays / hexagons generic UI components
MapLibre GL JS map rendering blocking P0 if map setup fails

Verification
For /client:
npm run lint
npm run build
For /dashboard:
npm run lint
npm run build
Before finishing a frontend task, report:

1. changed files
2. libraries actually used
3. any new dependency request
4. lint result
5. build result
6. remaining risks / TODOs
   Do not claim completion when lint or build fails.
