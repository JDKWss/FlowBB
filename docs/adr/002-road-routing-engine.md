# ADR 002: Road-routing service architecture

- Status: **Proposed**
- Date: 2026-09-20
- Owners: Core Backend Owner; infrastructure/dependency changes require explicit approval
- Technical specification: [FlowBB road-routing service](../ROUTING_SERVICE.md)
- Related accepted decision: [ADR 001: runtime persistence](001-runtime-persistence.md)

## Context

FlowBB currently depends on `IRoutePlanner` and retains a deterministic,
offline `DemoRoutePlanner`. The resident experience eventually needs
road-following Walking, Bike and Car routes with snapping, distance, duration
and geometry. PublicTransport is a separate timetable-routing concern.

An earlier proposal placed a dedicated engine directly beside ASP.NET and used
a provider-specific .NET adapter. The team subsequently selected a different
architecture: routing computation belongs in a private Python/FastAPI process
inside the same FlowBB Docker Compose deployment. The browser still calls only
the ASP.NET public API.

Reasons for the separate routing process:

- access to the Python OSM/graph ecosystem;
- isolation of graph initialization and compute-heavy routing work;
- a small, explicit internal REST boundary;
- preservation of the existing provider-neutral `IRoutePlanner` abstraction;
- independent replacement of the Python graph implementation;
- one repository and one Compose deployment rather than a separate platform.

Costs include another container/process, an internal HTTP hop, graph readiness
and timeout handling, artifact lifecycle work, and additional deployment
complexity. FastAPI is only the service layer; a graph library or dedicated
engine still performs routing.

Neo4j remains the only FlowBB application runtime database. The routing graph
is an infrastructure artifact and the service does not need an application
database. The current public contract also lacks road geometry and total
distance; changing it is a separate decision.

## Proposed decision

Introduce a private FastAPI `routing` service in the existing Compose stack.
ASP.NET calls it over the internal network, conceptually at
`http://routing:8000`. Do not publish that service to the browser or expose a
host port outside development/debugging needs.

Keep Application dependent on:

```text
IRoutePlanner
```

Add a technology-neutral ASP.NET Infrastructure adapter, conceptually:

```text
RoutingServiceRoutePlanner : IRoutePlanner
```

It serializes the internal request, calls FastAPI with a short timeout,
validates the response and maps it to FlowBB models. It contains no graph or
pathfinding logic. Domain and Application remain unaware of Python, HTTP and
the selected graph library.

The private service accepts origin, destination and `Walking`/`Bike`/`Car` in
one request and returns a normalized DTO containing mode, distance, duration,
GeoJSON `LineString` geometry in `[longitude, latitude]` order, and optional
steps. It owns no users, events, Attendance, Neo4j, CREW, PULSE, SignalR,
authentication or public-transport routing.

For the first bounded spike, prefer **OSMnx + NetworkX** with local,
preprocessed, mode-specific graphs. Evaluate pyrosm as the local PBF ingestion
step. Use established weighted shortest-path algorithms; do not implement a
custom A* without a measured problem that existing algorithms cannot solve.

This library recommendation remains subject to spike results and dependency
approval. If it fails resource, correctness or route-quality gates, compare a
dedicated Valhalla engine behind the same FastAPI contract. OSRM and GraphHopper
remain alternatives.

Load versioned graph artifacts once during FastAPI lifespan startup and serve
many requests. Never download or rebuild the graph per request. Provide a
cheap `/health` endpoint that distinguishes process liveness from successful
graph loading.

Retain `DemoRoutePlanner` as the mandatory deterministic fallback. Permit
automatic fallback only for explicitly classified infrastructure failures
such as connection failure, timeout or graph unavailability, and only when
demo fallback is enabled. Invalid input/mode, a genuine route-not-found result,
corrupt response or persistent configuration error must remain meaningful
failures rather than silently becoming fake data.

Use `IHttpClientFactory`, cancellation propagation and a short total timeout.
No infinite retries; at most one bounded retry may be accepted after
measurement for transient failures with deadline remaining.

## Alternatives considered

### OSMnx + NetworkX — recommended first spike

OSMnx provides OSM-oriented graph creation/load/save, spatial nearest-node
helpers, edge attributes and route geometry utilities on NetworkX graphs.
NetworkX provides maintained weighted shortest-path and A* implementations.
This is the smallest Python-native architecture for a bounded city graph, but
memory, startup time, latency, access semantics and turn restrictions must be
measured and checked with golden routes.

### pyrosm for ingestion

pyrosm can parse a local `.osm.pbf` into walking, cycling and driving networks
and export NetworkX-compatible graphs. It is useful in the offline artifact
pipeline, not a complete substitute for snapping, route calculation,
normalization and serving.

### python-igraph backend

igraph provides efficient weighted shortest paths and could reduce algorithm
or memory cost. It would add conversion and custom mapping for OSM attributes,
parallel edges, snapping and geometry. Revisit it only if profiling proves
NetworkX is the bottleneck.

### Valhalla behind FastAPI

Valhalla provides mature OSM ingestion, pedestrian/bicycle/auto dynamic
costing, snapping, geometry and maneuvers. It is the preferred dedicated-engine
comparison if the Python-native spike fails. The downside is that FastAPI
becomes a gateway to another engine process, increasing operational complexity.

### OSRM behind FastAPI

OSRM is fast and supports nearest, steps and GeoJSON. Its routing profile is
applied during preprocessing, so Walking, Bike and Car generally require
separate prepared datasets and operational orchestration.

### GraphHopper behind FastAPI

GraphHopper is mature, imports OSM and supports car/bike/foot profiles,
snapping, geometry and instructions. It adds a JVM and its own graph
preparation/runtime while FastAPI acts as a proxy.

### Custom A*

Rejected for the first implementation. A search algorithm alone does not solve
OSM access/direction rules, profile costs, spatial snapping, parallel edges or
geometry. NetworkX already offers the relevant algorithms.

### In-process .NET routing

Superseded by the human architecture decision. Itinero and generic .NET graph
libraries remain historical alternatives, but ASP.NET must not load the road
graph or perform routing in the target architecture.

### Public Internet routing API

Rejected as the primary path because it adds availability, quota and privacy
dependencies to the demo.

### Neo4j/PostGIS as the routing engine

Rejected. Neo4j owns FlowBB application data; PostGIS is an isolated MZK PoC.
Neither is the approved road-graph store or routing engine.

## Consequences

Positive:

- clean public ASP.NET and private routing-service boundaries;
- Application and Domain remain stable behind `IRoutePlanner`;
- access to Python-native OSM tooling;
- graph initialization and memory isolated from FlowBB.Api;
- normalized DTO permits later engine replacement;
- deterministic offline fallback remains available;
- no second application database;
- raw origins remain internal and do not reach PULSE/dashboard.

Costs and risks:

- one additional service, internal HTTP call and failure boundary;
- graph artifact build, versioning, distribution and readiness checks;
- duplicated graph memory if worker count is configured carelessly;
- profile/access correctness and snapping require golden tests;
- timeout/fallback classification can mask outages if implemented too broadly;
- public OpenAPI and client work is still required to expose real geometry;
- MapLibre basemap tiles may need Internet even when route calculation does not.

## Privacy and transport boundary

React calls only FlowBB.Api. ASP.NET reads origin from the Attendance snapshot
and sends origin, event destination and mode over the private Compose network.
The routing service receives no `userId`/`eventId`, persists no origin and logs
no exact coordinates. Individual routes never reach PULSE or dashboard.

`PublicTransport` bypasses the road-routing service. It remains on the demo
planner until a separately approved timetable-routing solution with complete
data exists.

## Rollback and acceptance gate

Before production integration, a disposable spike must demonstrate all three
road profiles, correct snapping and GeoJSON, plausible distance/duration,
reproducible offline artifacts, acceptable startup/latency/memory on demo
hardware, health semantics and correct timeout/fallback behavior.

If OSMnx/NetworkX fails, evaluate Valhalla behind the same FastAPI contract. If
no option passes before feature freeze, keep `DemoRoutePlanner` and defer real
routing. Rollback after future integration selects the demo planner through
configuration and removes/disables the routing service; handlers and
`IRoutePlanner` remain unchanged.

## Unresolved decisions

- exact Python and library versions;
- PBF preprocessing path and artifact format;
- clip margin and maximum snap distance;
- detailed Walking/Bike/Car costs and bicycle preferences;
- timeout, worker count and whether one retry is justified;
- internal error DTO/versioning and maneuver scope;
- artifact storage/update owner and OSM attribution placement;
- separate public OpenAPI fields for distance, geometry and planner source.

This ADR remains **Proposed** until the Core Backend Owner accepts the spike,
dependencies and deployment changes.
