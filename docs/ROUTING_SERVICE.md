# FlowBB road-routing service

Status: **Integrated for the local end-to-end demo; ADR 002 remains Proposed**
Decision record: [ADR 002](adr/002-road-routing-engine.md)

Implementation status on `develop` (2026-09-20): the private FastAPI service,
OSMnx artifact builder, private Compose services, ASP.NET adapter and composite
planner are wired. Walking/Bike/Car return `RoadRouting` with distance and
GeoJSON; PublicTransport and controlled transient fallback use `Demo`.
ADR 002 remains `Proposed`; integration does not change its governance status.

## 1. Purpose

FlowBB needs real road-following routes for `Walking`, `Bike` and `Car` while
keeping its public API, domain model and application orchestration independent
of a particular routing library. The approved direction is a private
Python/FastAPI service in the same Docker Compose deployment as FlowBB.

FastAPI is the HTTP boundary, not the routing algorithm. The first recommended
spike uses OSMnx and NetworkX against prebuilt, mode-specific OSM graphs. A
dedicated engine can replace that implementation later without changing the
public FlowBB API or `IRoutePlanner`.

This remains a hackathon/local-demo implementation, not a production routing SLO.

## 2. Architecture and trust boundaries

```text
Resident React client
        |
        | public FlowBB REST API
        v
FlowBB.Api (ASP.NET)
        |
        v
GetEventRouteHandler
        |
        v
IRoutePlanner
        |
        +-- RoutingServiceRoutePlanner (Infrastructure)
        |       |
        |       | private HTTP
        |       v
        |   FastAPI routing service
        |       |
        |       v
        |   OSM-derived road graph
        |
        `-- DemoRoutePlanner (controlled fallback)
                |
                v
FlowBB public RouteResponse
        |
        v
React + MapLibre
```

The browser calls only the ASP.NET FlowBB API. It must never know the routing
service hostname, graph format, Python implementation or selected routing
library, and it must never call FastAPI directly.

`IRoutePlanner` remains the Application boundary. Domain and Application must
not contain HTTP, FastAPI, OSMnx, NetworkX or engine-specific types. The
`RoutingServiceRoutePlanner : IRoutePlanner` belongs in ASP.NET
Infrastructure and contains no pathfinding logic.

## 3. Public and internal APIs

There are two separate contracts.

### Public FlowBB API

The accepted contract remains the one in `contracts/openapi.yaml`:

```http
GET /api/events/{eventId}/route?userId={userId}
```

The resident client supplies event/user identity according to that contract.
It does not supply a routing-service URL and does not send an origin directly
to FastAPI.

`JourneyOption` exposes optional `distanceMeters` and a provider-neutral GeoJSON
`LineString`. They are present for successful `RoadRouting` results and nullable
for the current Demo/PublicTransport path.

### Internal routing API

The proposed private `RouteCalculationRequest` contract is:

```http
POST http://routing:8000/route
Content-Type: application/json
```

```json
{
  "origin": {
    "latitude": 49.8224,
    "longitude": 19.0443
  },
  "destination": {
    "latitude": 49.8220,
    "longitude": 19.0469
  },
  "mode": "Walking"
}
```

Normalized `RouteCalculationResponse`:

```json
{
  "mode": "Walking",
  "distanceMeters": 2840,
  "durationSeconds": 2100,
  "geometry": {
    "type": "LineString",
    "coordinates": [
      [19.0443, 49.8224],
      [19.0451, 49.8222],
      [19.0469, 49.8220]
    ]
  },
  "steps": [
    {
      "instruction": "Continue on the pedestrian path",
      "distanceMeters": 310,
      "durationSeconds": 230
    }
  ]
}
```

GeoJSON coordinates always use `[longitude, latitude]`. The JSON DTO is small,
versionable and provider-neutral. Python graph objects, node identifiers,
encoded provider polylines and library-specific costing fields must not cross
this boundary.

Suggested internal failure semantics:

| Condition | Internal result | ASP.NET behavior |
|---|---|---|
| Invalid coordinate or unsupported mode | `400`/`422` with stable error code | return/map a meaningful validation error; no fallback |
| Valid input but no routable path | `404` with `route_not_found` | meaningful route-not-found result; no fallback |
| Graph not loaded | `/health` not ready; route returns `503` | infrastructure failure; demo fallback only when enabled |
| Unexpected calculation failure | `500` with correlation ID, no raw coordinates | treat as infrastructure failure only when classified as transient |

The exact internal error schema, endpoint versioning and maximum response size
remain spike decisions. They are not part of public OpenAPI.

## 4. End-to-end data flow

1. The client calls the public FlowBB route endpoint with event/user identity.
2. `GetEventRouteHandler` reads the event destination and the Attendance
   snapshot containing origin and selected `TransportMode`.
3. Application calls `IRoutePlanner.PlanAsync(RouteRequest, cancellationToken)`.
4. `RoutingServiceRoutePlanner` serializes a private request and calls FastAPI.
5. FastAPI selects the correct profile, snaps both points, calculates the
   route, reconstructs geometry and returns normalized metrics.
6. ASP.NET validates and maps the internal DTO to provider-neutral FlowBB
   models.
7. FlowBB.Api returns the approved public `RouteResponse`.
8. React validates and consumes the public GeoJSON.
9. MapLibre renders the line; it does not calculate the route.

## 5. Service responsibility

The routing service owns only:

- loading the prepared road graphs;
- selecting the Walking, Bike or Car graph/profile;
- snapping origin and destination to a routable network;
- shortest/fastest-path calculation;
- distance and estimated duration;
- GeoJSON `LineString` construction;
- optional provider-neutral maneuvers if the selected stack supports them;
- routing readiness reporting.

It does not own users, Events, Attendance, Neo4j, CREW, PULSE, SignalR,
authentication, the dashboard, the public FlowBB contract or public-transport
timetables. It has no application database and does not persist requests or
user origins.

Neo4j remains FlowBB's only application runtime database. The road graph is a
versioned infrastructure artifact, not application persistence. Do not add
PostgreSQL, PostGIS, Redis, SQLite or another Neo4j instance to this service
without a separate demonstrated requirement and decision.

## 6. Python-compatible routing options

Research was refreshed on 2026-09-20 against official project documentation.

| Candidate | What it provides | Fit and cost for FlowBB |
|---|---|---|
| OSMnx 2.1.1 + NetworkX 3.6.1 | OSM-oriented `MultiDiGraph`, nearest-node helpers, edge length/speed/travel-time utilities, graph save/load, shortest-path and route geometry support | Best first Python-native spike for a bounded city graph; simple to keep behind FastAPI, but memory and latency must be measured |
| pyrosm | Fast local `.osm.pbf` parsing for driving, cycling and walking networks and conversion to NetworkX-compatible graphs | Useful preprocessing/input option; it is not itself the complete routing service or algorithm |
| python-igraph | Efficient weighted shortest paths and compact graph structures | Potential performance backend, but requires conversion plus custom retention of OSM attributes, snapping and geometry; not the simplest first path |
| Valhalla | Mature OSM routing, dynamic pedestrian/bicycle/auto costing, snapping, geometry and maneuvers | Strong evolution path if Python graph performance/quality fails; running it behind FastAPI makes FastAPI a gateway and adds another engine process |
| OSRM | High-performance OSM engine, nearest service, GeoJSON and steps | Strong per-profile routing; its profile is applied at preprocessing, so three modes normally mean separately prepared datasets/services or additional orchestration |
| GraphHopper | Mature OSM import, foot/bike/car profiles, snapping, geometry and turn instructions | Capable alternative, but adds a JVM and graph preparation while FastAPI remains a proxy |

Official references:

- [OSMnx user reference](https://osmnx.readthedocs.io/en/stable/user-reference.html)
- [NetworkX shortest-path algorithms](https://networkx.org/documentation/stable/reference/algorithms/shortest_paths.html)
- [pyrosm graph documentation](https://pyrosm.readthedocs.io/en/latest/graphs.html)
- [python-igraph shortest paths](https://python.igraph.org/en/main/tutorials/shortest_path_visualisation.html)
- [Valhalla routing overview](https://valhalla.github.io/valhalla/api/turn-by-turn/overview/)
- [OSRM profiles](https://project-osrm.org/docs/v26.4.0/profiles)
- [GraphHopper repository and feature overview](https://github.com/graphhopper/graphhopper)

### Implemented bounded spike

The bounded spike uses **OSMnx 2.1.1 + NetworkX 3.6.1** on Python 3.12.11,
using local prepared GraphML graphs and NetworkX weighted shortest paths.
`pyrosm` is not required by the implemented Overpass preprocessing path.

Reasons:

- it stays within the approved Python service and has the smallest number of
  runtime processes;
- OSMnx supplies OSM/network conveniences while NetworkX supplies established
  weighted shortest-path algorithms;
- graph loading, snapping, routing and geometry can be validated independently;
- the bounded Bielsko-Biala region is a reasonable hypothesis for an in-memory
  graph, but the spike must prove it;
- the normalized REST DTO isolates ASP.NET from later replacement by Valhalla,
  OSRM or GraphHopper.

This is a recommendation for measurement, not an accepted dependency list.
Pin exact versions only after the spike passes and the owner approves them.

### Custom A* decision

Do not implement custom A* for the first version. NetworkX already provides
weighted shortest-path and A* implementations. A custom search would not solve
the harder correctness work: OSM access rules, directionality, profile costs,
snapping, parallel edges and geometry reconstruction. Consider a different
algorithm backend only after profiling shows a concrete latency or memory
problem and golden routes protect behavior.

### Dedicated-engine decision

A dedicated engine may be better if the spike cannot meet route-quality,
turn-restriction, maneuver, memory or latency targets. Prefer Valhalla for the
next comparison because one local dataset supports dynamic pedestrian,
bicycle and auto costing. Keep FastAPI as the stable internal FlowBB boundary;
an engine-specific client remains inside the routing service.

That option has a real operational cost: FastAPI plus an engine process,
additional health checks, artifacts and configuration. OSRM and GraphHopper
remain credible alternatives, not silently selected defaults.

## 7. Graph profiles

Walking, Bike and Car must not share an identical graph/cost function merely
for convenience.

| FlowBB mode | Required graph semantics | Initial cost |
|---|---|---|
| `Walking` | pedestrian-accessible roads and paths; bidirectional travel where legally valid; excludes motor-only ways | length or walking travel time |
| `Bike` | bicycle-accessible ways, correct one-way/bicycle exceptions and preference for suitable cycle infrastructure | bicycle travel time plus documented suitability penalties if needed |
| `Car` | drivable directed roads, one-way and access restrictions, road-class speeds | travel time |

For OSMnx, prepare separate `walk`, `bike` and `drive` graphs rather than
changing only a speed constant. Preserve edge geometries and lengths; add and
validate mode-appropriate travel-time weights. For local PBF ingestion, verify
that pyrosm/OSMnx filtering retains required OSM access and direction tags.

`PublicTransport` never enters this road-routing service. It stays on the
current `DemoRoutePlanner` path until a separately accepted timetable planner
(for example OTP with complete GTFS) is available.

## 8. OSM data and artifact lifecycle

```text
pinned OSM extract for Bielsko-Biala + surrounding margin
        |
        v
offline clipping/filtering and mode-specific graph preparation
        |
        v
validation against golden coordinates and restrictions
        |
        v
versioned artifacts + provenance/checksum manifest
        |
        v
FastAPI lifespan startup: load each graph once
        |
        v
many in-memory routing requests
```

Do not query Overpass, download OSM, parse all of Poland or rebuild a graph per
request. FastAPI's lifespan startup mechanism is suitable for loading shared,
expensive resources once before serving requests; see the official
[lifespan documentation](https://fastapi.tiangolo.com/advanced/events/).

The artifact manifest should record at least:

- OSM source URL/provider and attribution;
- download timestamp and source checksum;
- geographic extent;
- preprocessing code/configuration version;
- Python/routing library versions;
- artifact checksum and schema/version;
- supported modes.

Generated graph artifacts and source PBF files should normally stay outside
Git history and be reproducible from pinned inputs. The service must verify the
artifact version/checksum at startup. Graph-load failure keeps readiness false;
it must not cause an empty graph to appear healthy.

## 9. Snapping, geometry and validation

For each request:

1. validate finite WGS84 coordinates and supported mode;
2. choose the mode-specific graph;
3. snap origin and destination to the nearest graph node;
4. reject points outside the approved maximum snap distance;
5. calculate the weighted path;
6. select the traversed edge for every node pair, including parallel edges;
7. concatenate stored edge geometries in travel order;
8. calculate distance/duration from traversed edges;
9. return a valid GeoJSON `LineString` in `[longitude, latitude]` order.

The bounded implementation scans the in-memory nodes with haversine distance;
it does not claim edge snapping. It rejects either endpoint beyond the
configurable `ROUTING_MAX_SNAP_METERS` (default 500 m). A spatial index and
nearest-edge snapping remain a measured follow-up if graph size or route
quality requires them. A valid but unroutable pair is `ROUTE_NOT_FOUND`, not a
reason to fabricate a demo route.

If the Python-native stack cannot produce reliable provider-neutral maneuvers
within P0, return an empty `steps` array and keep distance/duration/geometry
correct. Do not invent misleading turn instructions.

## 10. ASP.NET adapter and configuration

Implemented Infrastructure component:

```text
RoutingServiceRoutePlanner : IRoutePlanner
```

Responsibilities:

- map the FlowBB request to the internal DTO;
- call FastAPI using `IHttpClientFactory`/a typed client;
- propagate `CancellationToken`;
- enforce a short total timeout;
- deserialize and validate the normalized response;
- validate echoed mode, non-negative metrics and valid LineString coordinates;
- map the result to `RoutePlan`;
- classify failures for the fallback policy;
- emit privacy-safe timing/failure telemetry.

It must not load graphs or implement pathfinding. Suggested configuration:

```yaml
Routing:
  ServiceUrl: http://routing:8000
  TimeoutSeconds: 3
  DemoFallbackEnabled: true
```

Equivalent environment settings can be used, for example
`ROUTING_SERVICE_URL=http://routing:8000`. The hostname belongs to API host
configuration, never Domain/Application. `3` seconds is a spike starting point,
not a final SLO; measure cold and warm requests on demo hardware.

## 11. Timeout, retry and fallback

Use one short end-to-end timeout and propagate caller cancellation through the
ASP.NET HTTP call. Never retry indefinitely. At most one small retry may be
enabled for idempotent calls after measurement, only for connection/transient
failures and only when enough of the original deadline remains.

| Failure | Automatic demo fallback? | Reason |
|---|---:|---|
| Connection refused/DNS/internal transport failure | Yes, when explicitly enabled | routing infrastructure unavailable |
| Routing request timeout | Yes, when explicitly enabled | infrastructure did not answer within demo budget |
| Graph failed to load / service not ready | Yes, when explicitly enabled | infrastructure artifact unavailable; health remains degraded |
| Explicit transient `5xx` | Yes, when classified and enabled | temporary service failure |
| Invalid coordinate or unsupported mode | No | caller/domain validation problem |
| Route genuinely not found | No | valid logical result that demo data must not hide |
| Malformed/corrupt response | No silent fallback | contract/integrity defect; surface and alert |
| Persistent configuration/version mismatch | No silent fallback | deployment defect requiring correction |

Fallback results must retain the existing visible demo/synthetic provenance.
Fallback activation is logged and measurable; a fallback success must not make
the real-routing health check green.

`DemoRoutePlanner` remains deterministic, offline and mandatory as the
PublicTransport implementation and controlled transient runtime fallback.

## 12. Health and observability

Internal endpoint:

```http
GET /health
```

Suggested cheap response:

```json
{
  "status": "ready",
  "processAlive": true,
  "graphLoaded": true,
  "artifactVersion": "bielsko-2026-09-20-v1",
  "modes": ["Walking", "Bike", "Car"]
}
```

Liveness is the process answering. Readiness requires all approved graph
artifacts to be loaded and validated. `/health` must inspect state only; it
must not calculate a route. ASP.NET/Compose can use it to detect a degraded
routing service.

ASP.NET should log correlation ID, mode, elapsed time, success/failure class
and fallback activation. FastAPI should log graph version, mode, elapsed time
and a privacy-safe error code. Neither side logs exact origins/destinations,
full request bodies or raw route geometries in routine logs.

## 13. Docker Compose topology

Implemented topology for this spike:

```text
docker compose
|-- api          ASP.NET FlowBB.Api; public FlowBB API
|-- routing      FastAPI; private port 8000; mounted/bundled graph artifacts
|-- routing-prepare  manual profile; populates the routing-data volume
|-- neo4j        only application runtime database
`-- seq          local structured-log UI
```

Only `api` calls `http://routing:8000` on the private Compose network. The
`routing` service normally has no public host port; an optional development
override may expose one for debugging. This is one FlowBB repository and one
deployment, not a separate routing repository.

`routing` is in the Compose profile `real-routing` and `routing-prepare` in the one-shot
profile `routing-tools`; the default start (`ROUTING_MODE=Demo`) creates neither and the
API uses `DemoRoutePlanner` directly. To use road routing, set `ROUTING_MODE=RoadRouting` in `.env`, prepare the
artifacts once, then start the runtime stack with both profiles:

```bash
docker compose -f infra/docker-compose.yml --env-file .env \
  --profile routing-tools run --rm routing-prepare
docker compose -f infra/docker-compose.yml --env-file .env \
  --profile local-db --profile real-routing up --build
```

`routing` mounts `routing-data` read-only and has only Compose `expose: 8000`,
not a public `ports` mapping. The API has no healthy dependency on routing, so
an unavailable graph service does not prevent the valid demo API path from
starting.

Route calculation must work without Internet after the artifact and images
are present. MapLibre/OpenFreeMap basemap rendering can still depend on network
tiles unless they are separately cached; that is distinct from routing.

## 14. Privacy

- React sends event/user identity only to the public FlowBB endpoint.
- ASP.NET obtains origin from the internal Attendance snapshot and destination
  from Event data.
- ASP.NET sends only origin, destination and mode over the private Compose
  network to the routing service.
- FastAPI does not persist user origin or associate it with a FlowBB user ID.
- Raw coordinates and individual routes never flow to PULSE or dashboard.
- Routine logs, traces and metrics omit exact coordinates and geometry.

The internal routing request therefore contains no `userId` or `eventId`.

## 15. Verification and spike gate

The synthetic suite proves validation, readiness, all three modes, stable
errors, snapping limits, deterministic paths, distance and directed edge
geometry without network access. The real Bielsko artifact/golden-route run
must still be recorded on demo hardware before the spike can be accepted. Its
remaining gate is:

1. a reproducible clipped Bielsko-Biala dataset plus margin;
2. distinct Walking/Bike/Car graphs and golden routes;
3. snapping success and maximum-distance rejection;
4. correct directed/one-way behavior for Car and bicycle exceptions;
5. correct GeoJSON order and road-following geometry;
6. distance/duration plausibility and deterministic repeat requests;
7. startup/load time, warm latency, memory and artifact size on demo hardware;
8. no route-time Internet dependency;
9. `/health` distinguishes process alive from graph ready;
10. timeout, cancellation, route-not-found and controlled fallback behavior;
11. no exact coordinates in ordinary logs;
12. the public API remains the only browser boundary.

If OSMnx/NetworkX fails the measured gate, compare Valhalla behind FastAPI.
Do not silently switch to custom A*, a public routing API or a new database.
If no real router passes before feature freeze, retain `DemoRoutePlanner` and
defer real routing.

## 16. Remaining decisions

- whether to keep Python 3.12.11, OSMnx 2.1.1 and NetworkX 3.6.1 after the
  measured golden-route run;
- whether later preprocessing should replace the implemented manual Overpass
  `graph_from_point` step with a pinned local PBF;
- artifact distribution and refresh ownership beyond the local named volume;
- whether nearest-edge/indexed snapping should replace the implemented
  bounded nearest-node scan;
- whether Bike needs a separately approved suitability cost beyond length;
- internal contract versioning;
- final timeout and whether one retry is justified by measurements;
- worker count, given that each worker may duplicate in-memory graphs;
- whether P0 requires maneuvers or allows `steps: []`;
- OSM attribution presentation and artifact refresh owner;
- production acceptance of ADR 002 and the operational ownership that follows.

The public contract delta is implemented: `PlannerSource.RoadRouting`, total
distance and GeoJSON LineString are mapped without exposing engine-specific
identifiers. The resident client runs on the host and calls only ASP.NET.
