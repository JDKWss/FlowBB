# Modul Routing

Trasa tam i z powrotem. Wlasciciel: Core Backend / Backend Feature. Issues #4, #13.

**Status: kod istnieje wylacznie na lokalnych branchach `feature/demo-route-planner`
i `feature/routing-api`. Nie ma go na `origin/develop`.**
Nie wymaga wlasnego adaptera Neo4j, ale potrzebuje `IEventLookup` (#16) i `IAttendanceOriginLookup`.

## Endpoint

| Metoda | Sciezka | Odpowiedzi |
|---|---|---|
| `GET` | `/api/events/{eventId}/route?userId=...` | 200 `RouteResponse`, 400, 404 |

> **Konflikt kontraktu - rozstrzygniety na korzysc `GET`.** PR #32 (frontend) zmienil
> `contracts/openapi.yaml` na `POST /api/events/{eventId}/route` z `origin` w ciele zadania
> i usunal parametr `RequiredUserIdQuery`. Core Backend Owner utrzymal wariant `GET`
> (`GET /api/events/{eventId}/route?userId={userId}`, `operationId: getEventRoute`), ktory
> implementuje ten modul. Uzasadnienie: punkt startu i srodek transportu pochodza ze snapshotu
> deklaracji "Ide", a nie z ciala zadania - inaczej klient wysylalby wspolrzedne, ktore w tym
> projekcie sa dana wewnetrzna, i trasa przestalaby byc zgodna z deklaracja.
> Kontrakt wraca do `GET` osobna zmiana na branchu `fix/restore-route-get-contract`.

`userId` jest wymagany. Trasa zalezy od punktu startu i srodka transportu zapisanych
w deklaracji "Ide", wiec **bez wczesniejszego POST attendance endpoint zwraca 404**.
To nie jest blad, tylko konsekwencja kolejnosci w scenariuszu demo: najpierw "Ide", potem trasa.

## Przeplyw

```text
GetEventRouteHandler
  -> IEventLookup.FindByIdAsync           -> null  => EventNotFound      (404)
  -> IAttendanceOriginLookup.FindAsync    -> null  => AttendanceNotFound (404)
  -> tryb == Unknown                               => InvalidTransportMode (400)
  -> new RouteRequest(...)  -> IRoutePlanner.PlanAsync  -> RoutePlan
```

`TransportMode.Unknown` jest odrzucany jawnie zamiast planowany jak komunikacja miejska.
Chodzi o to, zeby brak danych nie udawal poprawnej trasy - blad jest widoczny, a nie zamaskowany.

## Port `IRoutePlanner`

```csharp
Task<RoutePlan> PlanAsync(RouteRequest request, CancellationToken ct = default);
```

Wymaganie z `AGENTS.md`: **kod domenowy i endpointy nigdy nie wolaja OpenTripPlanner bezposrednio**.
Dodanie OTP (P1) to dopisanie drugiej implementacji tego portu i zmiana rejestracji w DI.
`DemoRoutePlanner` zostaje jako fallback nawet po dodaniu OTP.

`RouteRequest` jest samowystarczalny: zawiera `eventId`, czasy wydarzenia, cel, punkt startu i tryb.
Planer nie ma dostepu do bazy ani zegara, wiec wynik zalezy **wylacznie** od wejscia.
`Origin` to dana wewnetrzna i nie pojawia sie w odpowiedzi.

## `DemoRoutePlanner` - zalozenia symulacji

`Infrastructure/Routing/DemoRoutePlanner.cs`. To jest **rozwiazanie zastepcze oznaczone jako
DEMO DATA / SYMULACJA**, nie model transportu. Dziala bez internetu, bez Neo4j i bez danych MZK.

| Zalozenie | Wartosc |
|---|---|
| Odleglosc | linia prosta (haversine), `GeoDistance.KilometersBetween` |
| Predkosci | pieszo 4,8 / rower 15 / samochod 30 / autobus 22 km/h |
| Minimum na odcinek | 1 minuta |
| Komunikacja miejska | dojscie 6 min, oczekiwanie 5 min, linia `"7 (demo)"`, dojscie 4 min (w powrocie odwrotnie) |
| Przyjazd na wydarzenie | 10 minut przed `StartAt` |
| Powrot | 10 minut po koncu; dla komunikacji miejskiej dodatkowo drugi po 40 minutach |
| Brak `EndAt` | wydarzenie trwa 2 godziny |
| `ReturnGap` | **zawsze `false`** |

Dlaczego `ReturnGap` jest zawsze `false`: planer nie ma rozkladu ani godzin kursowania,
wiec nie ma na jakiej podstawie stwierdzic, ze powrotu nie ma. Alert luki powrotowej na
dashboardzie nie ma dzis zrodla danych - to samo ograniczenie co `participantsWithoutReturn`
w [MODULE_PULSE.md](MODULE_PULSE.md).

Dane MZK w `data/gtfs/mzk/parsed/` **nie sa tu uzywane**: to odjazdy z przystankow, bez kursow,
kolejnosci przystankow i wspolrzednych. Nie stanowia systemu routingu i nie nalezy ich tak opisywac.

## Determinizm

Planer nie uzywa losowosci, zegara systemowego ani sieci. Ta sama para (wydarzenie, deklaracja)
daje zawsze te sama trase - mozna ja pokazac wielokrotnie podczas prezentacji bez niespodzianek
i mozna ja testowac asercjami na konkretnych minutach.

## Model trasy

```text
RoutePlan
  Source: Demo | OpenTripPlanner
  Outbound: JourneyOption
  Returns:  JourneyOption[]        (1 pozycja, 2 dla komunikacji miejskiej)
  ReturnGap: bool
JourneyOption: DurationMinutes, DepartureAt, ArrivalAt, Steps[]  (minimum jeden krok)
RouteStep: Type (Walk|Transit|Bike|Car|Wait), Instruction, DurationMinutes, Line?
```

Czasy w odpowiedzi sa konwertowane na `Europe/Warsaw` w `RouteResponseMapping`.

## Pliki

```text
Domain/Routing/{RouteRequest,RoutePlan,JourneyOption,RouteStep,RouteStepType,PlannerSource,GeoDistance}.cs
Application/Abstractions/Routing/IRoutePlanner.cs
Application/Abstractions/Persistence/IAttendanceOriginLookup.cs
Application/Routing/AttendanceOrigin.cs
Application/Routing/GetEventRoute/GetEventRouteHandler.cs
Infrastructure/Routing/DemoRoutePlanner.cs
Api/Endpoints/Routing/{RoutingEndpoints,RoutingResponses}.cs
```
