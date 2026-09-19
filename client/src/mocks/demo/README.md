# Golden demo route fixtures

These files are frontend-only demo fixtures for the single FlowBB golden event,
`Koncert na Rynku` (`11111111-1111-1111-1111-111111111111`). They are not part
of the official OpenAPI contract and must not be treated as backend responses.

The fixtures demonstrate the geometry and maneuver data needed by the Route UI.
They were generated from the OSM-based Valhalla routing engine by running
`npm run generate:demo-routes`. Valhalla is never contacted by the React
application at runtime; the generated JSON is bundled locally.

The generator defaults to the FOSSGIS public endpoint
`https://valhalla1.openstreetmap.de`. Set `VALHALLA_BASE_URL` to use another
compatible Valhalla instance at generation time.

All modes share the deterministic origin `49.81272, 19.03384` (`Your location`)
and destination `49.82245, 19.04431` (`Rynek w Bielsku-Białej`). Walking uses
Valhalla `pedestrian`, Bike uses `bicycle`, and Car uses `auto` costing.

## Normalized shape

```json
{
  "eventId": "11111111-1111-1111-1111-111111111111",
  "mode": "Walking",
  "origin": {
    "latitude": 49.81272,
    "longitude": 19.03384,
    "label": "Your location"
  },
  "destination": {
    "latitude": 49.82245,
    "longitude": 19.04431,
    "label": "Rynek w Bielsku-Białej"
  },
  "distanceKm": 1.7,
  "durationMinutes": 20,
  "geometry": {
    "type": "LineString",
    "coordinates": [
      [19.033864, 49.812727],
      [19.033839, 49.812762]
    ]
  },
  "steps": [
    {
      "instruction": "Walk northwest on the walkway.",
      "distanceMeters": 47,
      "durationSeconds": 37
    }
  ]
}
```

Geometry is a GeoJSON `LineString`; every coordinate uses GeoJSON order
`[longitude, latitude]`. The React app consumes this normalized structure and
does not decode Valhalla's encoded polyline. A future backend route response can
replace these local fixtures without rewriting `RouteMap`.
