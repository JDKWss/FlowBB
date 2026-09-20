# FlowBB private road-routing service

Private FastAPI service for `Walking`, `Bike` and `Car` routes. It loads three
prepared OSMnx GraphML artifacts once at startup and uses NetworkX weighted
shortest paths. It is not a public FlowBB API and has no host port in the
normal Compose configuration.

## Prepare graph artifacts

Graph generation is the only step that contacts OSMnx/Overpass. It uses a
6 km bounding-box distance around `49.8176, 19.0391`, which covers the golden
demo route, and writes `walk.graphml`, `bike.graphml`, `drive.graphml` and a
checksummed `manifest.json` into the `routing-data` volume.

```bash
docker compose -f infra/docker-compose.yml --env-file .env \
  --profile routing-tools run --rm routing-prepare
```

The `routing` service belongs to the Compose profile `real-routing` and
`routing-prepare` to the one-shot profile `routing-tools` (so `up` never rebuilds
the graphs), so a default `docker compose up` starts neither (the API then
uses `Routing__Mode=Demo`, the deterministic `DemoRoutePlanner`, with no calls to
this service). To use it, set `ROUTING_MODE=RoadRouting` in `.env` and add
`--profile real-routing` to `up`.

The ordinary `routing` service only reads those local artifacts and never
downloads or rebuilds OSM data while handling `/route`.

## Run and verify

```bash
docker compose -f infra/docker-compose.yml --env-file .env --profile real-routing up --build routing
docker compose -f infra/docker-compose.yml --env-file .env --profile real-routing exec routing \
  python -c 'import json,urllib.request; print(json.load(urllib.request.urlopen("http://localhost:8000/health")))'
```

Run the network-independent synthetic test suite through the image test stage:

```bash
docker build --target test -t flowbb-routing-test routing-service
docker run --rm flowbb-routing-test
```

The service uses nearest-node snapping with a default 500 m limit. Walking and
Bike minimize edge `length`; their durations use explicit profile speeds of
4.8 km/h and 15 km/h. Car minimizes prepared edge `travel_time` and sums it for
duration. Geometry is reconstructed from the selected directed edges in
GeoJSON `[longitude, latitude]` order. Maneuver `steps` are intentionally empty.

## Internal endpoints

- `GET /health` is cheap and reports `ready` only after all checksummed graphs
  are loaded.
- `POST /route` accepts only `Walking`, `Bike` or `Car` plus WGS84 origin and
  destination. Stable errors are `INVALID_INPUT`, `UNSUPPORTED_MODE`,
  `GRAPH_NOT_READY`, `SNAP_TOO_FAR`, `ROUTE_NOT_FOUND` and `ROUTING_FAILED`.

Exact coordinates, request bodies and route geometry are not written to normal
logs. `PublicTransport`, FlowBB user/event identifiers and application data do
not enter this service.
