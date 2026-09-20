# Architektura backendu

## Projekty i kierunek zaleznosci

```text
FlowBB.Api  --->  FlowBB.Application  --->  FlowBB.Domain
     |                    ^
     |                    | (implementuje porty)
     +-----------> FlowBB.Infrastructure
```

| Projekt | Rola | Czego NIE zawiera |
|---|---|---|
| `FlowBB.Domain` | modele i reguly: `AttendanceIntent`, `Events/Event`, `Crews/Crew`, `Routing/*`, `Common/TransportMode`, `Common/GeoPoint` | repozytoriow, zaleznosci od Neo4j, HTTP i DI |
| `FlowBB.Application` | przypadki uzycia (handlery) + porty w `Abstractions/` | Cyphera, SignalR, typow ASP.NET Core |
| `FlowBB.Infrastructure` | adaptery: `Neo4j/`, `Routing/DemoRoutePlanner` | logiki biznesowej |
| `FlowBB.Api` | Minimal API, DTO, mapowania, SignalR `Hubs/` | regul domenowych |

Wzorzec w calym backendzie: **port w `Application/Abstractions`, adapter w `Infrastructure` lub `Api`**.
Handler nigdy nie wie, ze pod spodem jest Neo4j, a kod domenowy nigdy nie wie, ze pod spodem jest OpenTripPlanner.

`FlowBB.Domain` nie zawiera interfejsow repozytoriow: dawny `IFlowBbGraphRepository` i modele
`Domain/Models/*` zostaly usuniete (#59).

## Konwencje, ktore powtarzaja sie w kazdym module

1. **Walidacja w konstruktorze.** Obiekty domenowe (`AttendanceIntent`, `Event`, `Crew`, `RouteRequest`,
   `JourneyOption`, `GeoPoint`, `PulsePoint`) nie daja sie utworzyc w niepoprawnym stanie. Nie ma osobnej
   warstwy walidatorow.
2. **`null` zamiast wyjatku dla "nie znaleziono".** Handler zwraca `null` (lub enum statusu), gdy zasobu
   nie ma; wyjatek jest zarezerwowany dla bledu programisty (pusty `Guid`, niepoprawny zakres).
   Endpoint zamienia `null` na 404.
3. **Czas przez `TimeProvider`.** Zaden handler nie wola `DateTimeOffset.UtcNow`. Dzieki temu testy
   sterujacy zegarem nie sa flaky. Rejestracja: `services.TryAddSingleton(TimeProvider.System)`.
4. **Rejestracja modulu = dwie metody rozszerzajace.** `AddXModule(IServiceCollection)` i
   `MapXEndpoints(IEndpointRouteBuilder)`. Moduly nie edytuja `Program.cs` poza jedna linia wywolania -
   dlatego branche modulow moga powstawac rownolegle bez konfliktu.
5. **Enumy w JSON jako nazwy** (`"PublicTransport"`), nie liczby.
6. **Czas w odpowiedziach w `Europe/Warsaw`.** Konwersja w mapowaniach DTO (`EventResponseMapping`,
   `RouteResponseMapping`). Wewnatrz wszystko jest w UTC.
7. **Bledy jako `ProblemDetails`** (`TypedResults.Problem`), zgodnie z `contracts/openapi.yaml`.

## Stan podpiecia

To jest najwazniejsza informacja o dzisiejszym stanie kodu.

`backend/src/FlowBB.Api/Program.cs` rejestruje **wylacznie**:

```csharp
builder.Services.AddPulseHub();   // SignalR + IPulseNotifier
app.UseCors(FrontendCorsPolicy);
app.MapOpenApi(); app.MapScalarApiReference();   // tylko Development
app.MapPulseHub();                               // /hubs/pulse
app.MapGet("/health", ...);                      // /health
// TODO(#21): Register and map Attendance, Pulse, Events, Crew and Routing after their adapters are available.
```

Czyli uruchomione API odpowiada dzis na `/health`, `/hubs/pulse` i (w Development) `/scalar`.
**Zaden endpoint biznesowy nie jest osiagalny przez HTTP**, mimo ze kod endpointow istnieje i ma testy.

| Modul | Metody rejestracji | Gdzie jest kod | Podpiete | Adapter Neo4j |
|---|---|---|---|---|
| Attendance | `AddAttendanceModule` / `MapAttendanceEndpoints` | na develop | nie | brak (#17) |
| PULSE | `AddPulseModule` / `MapPulseEndpoints` | na develop | nie | brak (#15) |
| SignalR | `AddPulseHub` / `MapPulseHub` | na develop | **tak** | nie dotyczy |
| Events | `AddEventsModule` / `MapEventsEndpoints` | branch `feature/events-api` | nie | brak (#16) |
| Crew | `AddCrewModule` / `MapCrewEndpoints` | branch `feature/crew-application-api` | nie | brak (#18) |
| Routing | `AddRoutingModule` / `MapRoutingEndpoints` | branch `feature/routing-api` | nie | nie potrzebuje wlasnego, korzysta z `IEventLookup` i `IAttendanceOriginLookup` |

Podpiecie nalezy do Core Backend Ownera (issue #21) i jest jedna z dwoch rzeczy blokujacych demo.
Druga sa adaptery Neo4j - patrz [PERSISTENCE_PORTS.md](PERSISTENCE_PORTS.md).

## Konfiguracja

| Zrodlo | Klucze |
|---|---|
| `.env` (wzor w `.env.example`, `.env` nie trafia do repo) | `NEO4J_URI`, `NEO4J_DATABASE`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`, `ASPNETCORE_ENVIRONMENT`, `Cors__AllowedOrigins__0/1` |
| `appsettings.json` | Serilog (Console + Seq na `http://localhost:5341`), `Cors:AllowedOrigins` (domyslnie `5173` i `5174`), `AllowedHosts` |
| `infra/docker-compose.yml` | serwis `api` (port 8080) oraz `neo4j` w profilu `local-db` (szkic; docelowa konfiguracja z issue #8) |

CORS wymaga jawnej listy origin, bo polityka uzywa `AllowCredentials()` - wymaga tego SignalR.
Wszystkie zmienne w Compose sa oznaczone `:?`, wiec brak wartosci w `.env` zatrzymuje start
zamiast po cichu uruchomic API z bledna konfiguracja.

## Weryfikacja

```bash
dotnet build backend/FlowBB.sln
dotnet test  backend/FlowBB.sln
```

Testy oznaczone `[Neo4jFact]` sa pomijane, dopoki nie ustawisz `FLOWBB_NEO4J_TESTS=1`
i poprawnych zmiennych `NEO4J_*`. Zmierzone wyniki: [TESTS.md](TESTS.md).
