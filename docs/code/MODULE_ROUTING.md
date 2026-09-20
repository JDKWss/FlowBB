# Modul Routing

Trasa tam i z powrotem. Wlasciciel: Core Backend / Backend Feature. Issues #4, #13.

**Status (2026-09-20, `develop`): modul jest zaimplementowany i zarejestrowany w API.** `IRoutePlanner`,
`DemoRoutePlanner`, `CompositeRoutePlanner` i klient prywatnej uslugi drogowej sa na `develop`; wybor trybu opisuje
sekcja "Tryby planera" ponizej. Decyzja architektoniczna: [ADR 002](../adr/002-road-routing-engine.md) (przyjety dla MVP).
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
> `contracts/openapi.yaml` na `develop` ma wariant `GET` (`getEventRoute`).

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
| `ReturnGap` | `false` (planer sam nie ocenia luki) |

Dlaczego planer zwraca `ReturnGap = false`: nie ma rozkladu ani godzin kursowania, wiec nie ma na jakiej
podstawie stwierdzic, ze powrotu nie ma. Luke powrotowa ustala osobno `GetEventRouteHandler` regula
`DemoReturnGapPolicy` (uczestnik `PublicTransport`, wydarzenie konczace sie o 22:00 lub pozniej w `Europe/Warsaw`):
zastepuje plan wartosciami `returns: []` i `returnGap: true`, niezaleznie od planera. Ta sama regula zasila
`participantsWithoutReturn` i alert w PULSE, patrz [MODULE_PULSE.md](MODULE_PULSE.md).

`DemoRoutePlanner` nie uzywa danych MZK. Komunikacje miejska planuje domyslnie planer z rozkladu
(sekcja ponizej), a `DemoRoutePlanner` jest jego kontrolowanym fallbackiem.

## `MzkTimetableRoutePlanner` - komunikacja miejska z rozkladu MZK

`Infrastructure/Routing/Mzk/`. Obsluguje wylacznie `TransportMode.PublicTransport` i zwraca
`plannerSource: MzkTimetable`. Dane to osadzone w assembly pliki `data/gtfs/mzk/parsed/`
(`departures.json`, `calendar_days.json`, `stops.json`); zero nowych pakietow NuGet, bez sieci i bez zegara,
wiec wynik zalezy wylacznie od `RouteRequest` i jest deterministyczny.

| Co | Zrodlo |
|---|---|
| Godzina odjazdu z przystanku wsiadania | **rozklad** (wydrukowana godzina) |
| Godzina przyjazdu na przystanek wysiadania i czas jazdy | **rozklad** (rekonstrukcja kursu po wydrukowanych godzinach kolejnych przystankow) |
| Dojscie pieszo do i z przystanku | **szacunek**: linia prosta x 1,3 / 4,8 km/h, oznaczone w `instruction` jako "szacunek, linia prosta" |
| Wspolrzedne przystankow | OSM (Overpass), krok `stops` pipeline'u, 85/85 nazw |

Zasada: lepiej pominac odjazd, ktorego nie umiemy zinterpretowac, niz zaproponowac kurs, ktory nie dojedzie.
Rozklad jest skladany wylacznie z sasiednich przystankow, bo bezposrednie parowanie odleglych stron dawaloby
aliasing (najblizszy odjazd nalezy do poprzedniego kursu i czas jazdy wychodzilby za krotki).

**Zalozenia (nie potwierdzona praktyka MZK):**

- **Z-1.** Odjazd nalezy do doby, nad ktora jest wydrukowany (00:30 to 00:30 tej doby). Dla linii nocnych N1 i N2
  oba warianty daja dzis identyczny wynik, bo godziny sa takie same we wszystkich czterech kolumnach.
- **Z-2.** Flagi `N`, `R` i `W` dotycza wylacznie 25 grudnia, Nowego Roku i pierwszego dnia Wielkanocy.
  Pole `public_holiday` z kalendarza swiadomie NIE jest uzywane: obejmuje tez 1 i 11 listopada oraz 26 grudnia,
  wiec skasowaloby istniejace kursy wieczorne i pokazalo nieistniejaca luke powrotowa.
- **Z-4.** W noc cofniecia zegara (2026-10-25) godzina 02:50 jest niejednoznaczna; uzywamy offsetu standardowego.
- **Z-8.** Promien dojscia do przystanku to 800 m (tyle co komorka heksagonalna PULSE), predkosc pieszo 4,8 km/h.
- **Z-9.** Tylko kursy bez przesiadek na jednej parze (linia, kierunek).
- Data poza kalendarzem (2026-09-01..2026-12-31) jest odrzucana, a nie zgadywana po dniu tygodnia.

**Co ta regula zaniza (bezpieczny kierunek bledu - mniej polaczen niz istnieje):** wszystkie zjazdy do zajezdni
(`#`), takze te z trasa przelotowa; kursy przez odcinek o niepewnym czasie jazdy; 19 odjazdow z flaga `W`
w dni zwykle; brak przesiadek; tylko piec pobranych linii (4, 7, 16, N1, N2).

**Luka powrotowa.** `ReturnGap = true`, gdy po wydarzeniu nie ma zadnego odjazdu **albo** od gotowosci do wyjscia
(koniec wydarzenia + 10 min) do odjazdu mija wiecej niz 45 minut. Gdy luka jest prawdziwa, a pozna opcja istnieje
(np. linia nocna), zostaje w `Returns` - lepiej pokazac pozny powrot niz pusta liste. `GetEventRouteHandler` nie nadpisuje
wyniku tego planera regula 22:00 (`DemoReturnGapPolicy` obowiazuje planery bez rozkladu).

**Fallback.** `TimetableFallbackRoutePlanner` (wspolny dla trybow `Demo` i `RoadRouting`, bo domyslny tryb `Demo` nie
uzywa `CompositeRoutePlanner`) degraduje do `DemoRoutePlanner`, gdy rozklad nie umie zaplanowac trasy: brak przystanku
w zasiegu, brak polaczenia, data poza kalendarzem, rozklad niezaladowany. Powod trafia do logu bez wspolrzednych.

**Prywatnosc.** `stops` w odpowiedzi to wspolrzedne przystankow (publiczne dane OSM). Punkt startu uzytkownika nigdy
nie trafia do odpowiedzi ani do logow.

**Rozjazd z PULSE.** Agregat PULSE (`participantsWithoutReturn`, alert `ReturnGap`) nadal liczy luke regula 22:00, a karta
trasy w `/client` - z rozkladu. Oba wyniki moga sie roznic; dashboard mowi o tym w przypisie przy alercie.

## Tryby planera

Konfiguracja `Routing:Mode` (zmienna `ROUTING_MODE` w Compose):

| Tryb | `IRoutePlanner` | Zachowanie |
|---|---|---|
| `Demo` (domyslny) | `DemoRoutePlanner` | wszystkie tryby transportu przez planer demo; API nie wola uslugi drogowej, dziala bez internetu |
| `RoadRouting` | `CompositeRoutePlanner` | Walking/Bike/Car przez prywatna usluge (`plannerSource: RoadRouting`, dystans i geometria), PublicTransport zawsze `DemoRoutePlanner`; fallback do `DemoRoutePlanner` tylko przy `GraphNotReady`, `TransportFailure` lub `Timeout` i gdy `Routing:DemoFallbackEnabled=true` |

Bledy logiczne (nieprawidlowy tryb, brak trasy, uszkodzona odpowiedz) nie sa maskowane fallbackiem. Nieznana wartosc
`Routing:Mode` przerywa start API. Uslugi `routing` i `routing-prepare` opisuje [ROUTING_SERVICE.md](../ROUTING_SERVICE.md);
uruchomienie (profile `real-routing` i `routing-tools`): [DEMO_RUNBOOK.md](../DEMO_RUNBOOK.md).

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
