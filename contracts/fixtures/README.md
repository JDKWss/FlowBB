# FlowBB contract fixtures

Wszystkie dane w tym katalogu sa syntetyczne: **DEMO DATA / SYMULACJA**. Fixture'y pokazuja odpowiedzi
rzeczywistego API i sa weryfikowane przez `ContractFixturesTests` pod katem nazw pol oraz typow JSON.

| Plik | Endpoint / przypadek |
|---|---|
| `events-list.json` | `GET /api/events` - lista wydarzen |
| `event-details.json` | `GET /api/events/{eventId}` - szczegoly wydarzenia |
| `attendance-created.json` | `POST /api/events/{eventId}/attendance` - nowa deklaracja (`isNew: true`) |
| `attendance-repeated.json` | Ten sam `POST` ponowiony dla pary user-event (`isNew: false`) |
| `pulse-summary.json` | `GET /api/pulse/summary` - zagregowane KPI |
| `pulse-event.json` | `GET /api/pulse/events/{eventId}` - agregaty wydarzenia |
| `pulse-hexagons.geojson` | `GET /api/pulse/hexagons` - niepusta mapa; kazda komorka ma co najmniej 10 uczestnikow |
| `pulse-hexagons-empty.geojson` | `GET /api/pulse/hexagons` - wydarzenie bez komorki przekraczajacej prog prywatnosci |
| `groups.json` | `GET /api/events/{eventId}/groups` - lista mikrogrup |
| `route.json` | `GET /api/events/{eventId}/route` - deterministyczna trasa z `DemoRoutePlanner` |
| `problem-400.json` | ProblemDetails 400 - niepoprawny identyfikator |
| `problem-404.json` | ProblemDetails 404 - brak zasobu |
| `problem-409.json` | ProblemDetails 409 - pelna grupa |

Fixture'y PULSE zawieraja tylko agregaty. Nie zawieraja `userId` ani punktow startowych uzytkownikow. Wspolrzedne
w plikach GeoJSON sa wierzcholkami zagregowanych heksagonow, nie lokalizacjami pojedynczych osob.
