# Air Quality - kontrakt i polityki

## Cel i przeplyw danych

Air Quality opisuje warunki przy **lokalizacji wydarzenia**, nigdy przy domu
uczestnika ani na trasie. Jedyny dozwolony przeplyw to:

```text
Client / Dashboard -> FlowBB API -> IAirQualityProvider -> GIOS
```

Przegladarka nie wywoluje GIOS bezposrednio. Pozwala to utrzymac jedna
normalizacje, obsluge awarii i polityke fallback po stronie FlowBB.

## Kontrakt publiczny

Kanoniczny kontrakt to `GET /api/events/{eventId}/air-quality` oraz
`AirQualityResponse` w `contracts/openapi.yaml`. Operacja ma
`x-runtime-status: planned`, poniewaz issue #94 nie implementuje endpointu.

Odpowiedz zawiera `eventId`, stacje, czas pomiaru, poziom jakosci, status,
zrodlo, pomiary `pm10`, `pm25`, `no2`, `o3` oraz opcjonalny alert. Wszystkie
cztery pola pomiarowe sa obecne, lecz moga byc `null`: brak pomiaru nie oznacza
zera. Publiczna odpowiedz nie zawiera identyfikatora uzytkownika,
wspolrzednych mieszkanca, origin Attendance, Crew ani indywidualnej trasy.

`400` oznacza niepoprawny UUID, `404` poprawny UUID nieistniejacego wydarzenia,
a `500` tylko nieoczekiwany blad FlowBB. Awaria lub timeout GIOS ma w przyszlej
implementacji zwrocic `200` z `Fallback + Demo`, a nie publiczne `5xx`.

## Status i zrodlo

- `Fresh + Gios`: pomiar zewnetrzny ma nie wiecej niz 90 minut.
- `Stale + Gios`: uzyteczny pomiar zewnetrzny ma wiecej niz 90 minut.
- `Fallback + Demo`: brak uzytecznych danych zewnetrznych; FlowBB zwraca
  deterministyczny snapshot.

Kombinacje `Fresh + Demo` i `Fallback + Gios` nie powinny byc produkowane.
Prog 90 minut wynika z godzinnego cyklu publikacji GIOS i zostaje zamrozony dla
MVP+. Klasyfikacje wieku i wybor fallback beda nalezec do issue #96.

## Poziom jakosci, pomiary i czas

Mapowanie aktualnych kategorii Polskiego indeksu jakosci powietrza:

| GIOS | FlowBB `AirQualityLevel` |
|---|---|
| Bardzo dobry | `VeryGood` |
| Dobry | `Good` |
| Umiarkowany | `Moderate` |
| Dostateczny | `Sufficient` |
| Zly | `Bad` |
| Bardzo zly | `VeryBad` |
| Brak indeksu | `Unknown` |

`Unknown` oznacza, ze istnieja pomiary, ale GIOS nie wyznaczyl uzytecznego
indeksu stacji. Nie jest synonimem braku wszystkich pomiarow.

Jedyna jednostka stezenia w FlowBB to dokladnie `µg/m³` (znak micro `µ` U+00B5).
GIOS prezentuje jednostke rowniez jako mikrogramy na metr szescienny; adapter
normalizuje zapis dostawcy do kanonicznej pisowni FlowBB. `measuredAt` jest
ISO 8601 z jawnym offsetem, normalizowanym dla strefy `Europe/Warsaw`, np.
`2026-09-20T07:00:00+02:00`. Alert jest opcjonalny, informacyjny (`Info` albo
`Warning`) i nie stanowi diagnozy medycznej.

## Wybor stacji

Przyszly use case pobiera wspolrzedne wydarzenia i przekazuje je do
`IAirQualityProvider`. Adapter oblicza odleglosc w linii prostej i wybiera
najblizsza stacje, ktora udostepnia co najmniej jeden obslugiwany pomiar:
PM10, PM2.5, NO2 albo O3. Brakujace zanieczyszczenia pozostaja `null`.
`station.distanceMeters` jest odlegloscia wydarzenie-stacja. Publiczne API nie
udostepnia wspolrzednych stacji.

## GIOS - stan zweryfikowany 2026-09-20

Oficjalna dokumentacja wskazuje aktualna rodzine uslug `v1` pod baza
`https://api.gios.gov.pl/pjp-api`; wersje bez `/v1` wycofano 30 czerwca 2025 r.:

- lista stacji: `/v1/rest/station/findAll`;
- stanowiska/czujniki stacji: `/v1/rest/station/sensors/{stationId}`;
- bieżące pomiary: `/v1/rest/data/getData/{sensorId}`;
- indeks stacji: `/v1/rest/aqindex/getIndex/{stationId}`.

Wygenerowany dokument OpenAPI ma techniczne `info.version: v0`, ale oficjalna
strona GIOS oznacza powyzsze publiczne sciezki jako aktualna wersje `v1`.

Opublikowana specyfikacja OpenAPI nie definiuje mechanizmu uwierzytelnienia ani
klucza API. GIOS publikuje limit 1500 zapytan/min dla bieżących pomiarow i
indeksu; metadane i czesc pozostalych uslug maja limit 2 zapytan/min. Regulamin
dodatkowo zawiera ogolna zasade pobierania nie czesciej niz dwa razy na godzine,
dlatego implementacja #97 powinna buforowac metadane, przestrzegac ostrzejszej
zasady tam, gdzie ma zastosowanie, i obslugiwac `429`.

Dane bieżące sa godzinne, niezweryfikowane i moga pozniej ulec zmianie.
Oficjalne zrodla:

- <https://powietrze.gios.gov.pl/pjp/content/api>
- <https://api.gios.gov.pl/pjp-api/swagger-ui/index.html>
- <https://api.gios.gov.pl/pjp-api/v3/api-docs>
- <https://powietrze.gios.gov.pl/pjp/content/terms_of_service>
- <https://powietrze.gios.gov.pl/pjp/content/health_informations>

Przy `source: Gios` UI musi czytelnie pokazac co najmniej
`Źródło danych: GIOŚ - EKOINFONET` (regulamin wymaga tez czasu wytworzenia i
informacji o zakresie/przetworzeniu). Przy `source: Demo` UI pokazuje
`Źródło: dane demonstracyjne FlowBB`.

## Fallback dla issue #98

Issue #98 ma utworzyc `data/air-quality/demo-snapshot.json` o ksztalcie
dokladnie `AirQualityResponse`, z `status: Fallback`, `source: Demo` i stalym
`measuredAt`. Wzorem formatu jest `contracts/fixtures/air-quality-fallback.json`; `ContractFixturesTests`
sprawdza go wzgledem kontraktu razem z regula, ze `Fresh` i `Stale` wystepuja tylko ze zrodlem `Gios`, a `Fallback`
tylko z `Demo`. Snapshot jest jeden dla wszystkich wydarzen: `eventId` i `station.distanceMeters` w pliku sa stale (dane
demonstracyjne), a use case (#96) podstawia `eventId` zadanego wydarzenia i zwraca pozostale pola bez zmian. Plik jest wersjonowany i nie moze byc aktualizowany automatycznie
w runtime. Przeplyw ma pozostac prosty: deserializacja snapshotu i zwrot tego
samego kontraktu publicznego.

## Izolacja awarii

Air Quality jest osobnym, tylko do odczytu endpointem. Zadna inna operacja API (Events, Attendance, Crew, Routing,
PULSE, SignalR) nie wywoluje `IAirQualityProvider` i nie zalezy od dostepnosci GIOS; GIOS nie wplywa tez na `/health`
ani `/health/ready`. Awaria, timeout, limit zapytan (`429`) albo bezuzyteczna odpowiedz GIOS konczy sie na granicy
`getEventAirQuality`: ten endpoint zwraca `200` z `Fallback + Demo` (#96), a pozostale endpointy dzialaja bez zmian.
Client i Dashboard traktuja karte jakosci powietrza jako opcjonalny widget: jej ladowanie i blad nie moga blokowac
renderowania wydarzenia, KPI, mapy ani aktualizacji SignalR (#95).

## Granice kolejnych issue

- #95: Client i Dashboard, w tym etykiety zrodla; bez polaczen do GIOS.
- #96: use case, prog 90 minut, cache i wybor fallback.
- #97: adapter GIOS, normalizacja danych i endpoint runtime.
- #98: deterministyczny snapshot zgodny 1:1 z `AirQualityResponse`.

Air Quality nie jest zapisywane w Neo4j i nie zmienia zasad prywatnosci PULSE.
