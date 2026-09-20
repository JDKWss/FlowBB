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
`AirQualityResponse` w `contracts/openapi.yaml`. Endpoint jest zaimplementowany
i zawsze zwraca dane znormalizowane przez backend FlowBB.

Odpowiedz zawiera `eventId`, stacje, czas pomiaru, poziom jakosci, status,
zrodlo, pomiary `pm10`, `pm25`, `no2`, `o3` oraz opcjonalny alert. Wszystkie
cztery pola pomiarowe sa obecne, lecz moga byc `null`: brak pomiaru nie oznacza
zera. Publiczna odpowiedz nie zawiera identyfikatora uzytkownika,
wspolrzednych mieszkanca, origin Attendance, Crew ani indywidualnej trasy.

`400` oznacza niepoprawny UUID, `404` poprawny UUID nieistniejacego wydarzenia,
a `500` tylko nieoczekiwany blad FlowBB. Awaria, timeout lub bezuzyteczna
odpowiedz GIOS zwraca `200` z `Fallback + Demo`, a nie publiczne `5xx`.

## Status i zrodlo

- `Fresh + Gios`: pomiar zewnetrzny ma nie wiecej niz 90 minut.
- `Stale + Gios`: uzyteczny pomiar zewnetrzny ma wiecej niz 90 minut.
- `Fallback + Demo`: brak uzytecznych danych zewnetrznych; FlowBB zwraca
  deterministyczny snapshot.

Kombinacje `Fresh + Demo` i `Fallback + Gios` nie sa produkowane.
Prog 90 minut wynika z godzinnego cyklu publikacji GIOS. Use case klasyfikuje
wiek, utrzymuje cache (wynik `Fresh`/`Stale` 30 minut, `AirQuality:CacheMinutes`; wynik `Fallback` tylko 60 sekund,
`AirQuality:FallbackCacheSeconds`, zeby po awarii szybko wrocic do GIOS), ogranicza rownolegle wywolania dla wydarzenia
i wybiera fallback po timeoutcie lub bledzie dostawcy. Minimum 30 minut dla udanego odczytu wynika z ogolnej zasady
GIOS o pobieraniu danych nie czesciej niz dwa razy na godzine; krotki cache fallbacku nie zwieksza liczby zapytan
ponad ograniczenie rownoleglych wywolan (jedno wywolanie fabryki na wydarzenie w oknie). Metadane listy stacji
oraz stanowisk sa wspoldzielone przez wydarzenia i buforowane przez 12 godzin,
aby respektowac ostrzejszy limit uslug metadanych GIOS.

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

Use case pobiera wspolrzedne wydarzenia i przekazuje je do
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

## Fallback demonstracyjny

`data/air-quality/demo-snapshot.json` ma ksztalt
dokladnie `AirQualityResponse`, z `status: Fallback`, `source: Demo` i stalym
`measuredAt`. Wzorem formatu jest `contracts/fixtures/air-quality-fallback.json`; `ContractFixturesTests`
sprawdza go wzgledem kontraktu razem z regula, ze `Fresh` i `Stale` wystepuja tylko ze zrodlem `Gios`, a `Fallback`
tylko z `Demo`. Plik jest wersjonowany, osadzany jako zasob assembly i nie moze byc aktualizowany automatycznie
w runtime. Jest deserializowany lokalnie i nie wymaga GIOS, Neo4j ani zmiennych srodowiskowych. Snapshot jest jeden
dla wszystkich wydarzen: use case podstawia `eventId` zadanego wydarzenia i przelicza jedynie odleglosc
wydarzenie-stacja, zachowujac zamrozone pomiary oraz pozostale dane demonstracyjne.

## Izolacja awarii

Air Quality jest osobnym, tylko do odczytu endpointem. Zadna inna operacja API (Events, Attendance, Crew, Routing,
PULSE, SignalR) nie wywoluje `IAirQualityProvider` i nie zalezy od dostepnosci GIOS; GIOS nie wplywa tez na `/health`
ani `/health/ready`. Awaria, timeout, limit zapytan (`429`) albo bezuzyteczna odpowiedz GIOS konczy sie na granicy
`getEventAirQuality`: ten endpoint zwraca `200` z `Fallback + Demo` (#96), a pozostale endpointy dzialaja bez zmian.
Client i Dashboard traktuja karte jakosci powietrza jako opcjonalny widget: jej ladowanie i blad nie moga blokowac
renderowania wydarzenia, KPI, mapy ani aktualizacji SignalR (#95).

## Implementacja

- #94: kanoniczny kontrakt i modele provider-neutral.
- #95: karta w Client i Dashboard; przegladarki wywoluja tylko FlowBB API.
- #96: use case, prog 90 minut, cache, timeout i wybor fallback.
- #97: adapter GIOS v1, normalizacja danych i endpoint runtime.
- #98: deterministyczny snapshot zgodny 1:1 z `AirQualityResponse`.

Air Quality nie jest zapisywane w Neo4j i nie zmienia zasad prywatnosci PULSE.
