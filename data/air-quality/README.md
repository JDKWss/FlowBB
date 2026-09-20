# Deterministyczny snapshot Air Quality

## Purpose

`demo-snapshot.json` dostarcza deterministyczne, offline'owe dane fallback/demo
jakości powietrza dla FlowBB. Plik jest statyczny i gotowy do późniejszego
wykorzystania przez logikę fallback z issue #96.

## Contract

Snapshot odpowiada dokładnie `AirQualityResponse` z
[`contracts/openapi.yaml`](../../contracts/openapi.yaml). Nie definiuje nowego
kontraktu ani dodatkowych właściwości runtime.

## Runtime semantics

- `status = Fallback`
- `source = Demo`

Wartości pochodzą z zamrożonego odczytu historycznego, ale aplikacja udostępnia
lokalny plik demonstracyjny, a nie aktualne dane GIOŚ.

## Event

Snapshot jest przypisany do wydarzenia `Koncert na Rynku`:

- event ID: `11111111-1111-1111-1111-111111111111`
- lokalizacja wydarzenia: `49.82245, 19.04431` (Rynek w Bielsku-Białej)

Współrzędne pochodzą z kanonicznego seedu
[`database/flowbb-demo-seed.cypher`](../../database/flowbb-demo-seed.cypher).

## Station

- nazwa: `Bielsko-Biała, ul. Kossak-Szczuckiej`
- oficjalny identyfikator GIOŚ: `789`
- kod stacji: `SlBielKossak`
- adres: `ul. Kossak-Szczuckiej 19, Bielsko-Biała`
- współrzędne WGS84: `49.813464, 19.027318`

Metadane stacji pochodzą z oficjalnej usługi GIOŚ / EKOINFONET.

## Measurements

Snapshot zamraża stan z `2026-09-20 10:00:00` czasu lokalnego:

- PM10: `null` — odczyt dla tej godziny był niedostępny;
- PM2.5: `null` — oficjalna lista stanowisk stacji nie zawiera czujnika PM2.5;
- NO2: `5.3 µg/m³` (stanowisko `5162`);
- O3: `80.8 µg/m³` (stanowisko `5164`).

Brak pomiaru jest zawsze reprezentowany przez `null`, nigdy przez `0`.
`qualityLevel = Good` odpowiada oficjalnej kategorii indeksu stacji `Dobry`
zwróconej przez GIOŚ w czasie pobrania.

## Units

Każdy niepusty pomiar używa kanonicznej jednostki `µg/m³`, w tym znaku micro
`µ` (U+00B5). Zapis dostawcy `μg/m3` został znormalizowany bez przeliczania
wartości.

## Time

`measuredAt` ma stałą wartość `2026-09-20T10:00:00+02:00`. Jest to ISO 8601
z jawnym offsetem `Europe/Warsaw`; we wrześniu 2026 obowiązuje czas letni
`+02:00`. Plik nie korzysta z zegara systemowego ani czasu modyfikacji pliku.

## Provenance

Wartości są pochodną rzeczywistych, godzinnych danych GIOŚ / EKOINFONET,
pobranych `2026-09-20T10:22:27+02:00`:

- metadane stacji:
  <https://api.gios.gov.pl/pjp-api/v1/rest/station/findAll?page=0&size=500>
- stanowiska stacji:
  <https://api.gios.gov.pl/pjp-api/v1/rest/station/sensors/789>
- PM10, stanowisko `5167`:
  <https://api.gios.gov.pl/pjp-api/v1/rest/data/getData/5167>
- NO2, stanowisko `5162`:
  <https://api.gios.gov.pl/pjp-api/v1/rest/data/getData/5162>
- O3, stanowisko `5164`:
  <https://api.gios.gov.pl/pjp-api/v1/rest/data/getData/5164>
- indeks stacji:
  <https://api.gios.gov.pl/pjp-api/v1/rest/aqindex/getIndex/789>

GIOŚ zwraca dane w czasie lokalnym i opisuje oryginalną jednostkę jako
`μg/m3`. Snapshot zachowuje wartości bez przeliczania i normalizuje wyłącznie
zapis jednostki oraz jawnie dodaje warszawski offset. Po zapisaniu dane są
zamrożonym fallbackiem demo, nie danymi live.

## Distance

`distanceMeters = 1576` oznacza odległość w linii prostej od lokalizacji
wydarzenia (`49.82245, 19.04431`) do stacji (`49.813464, 19.027318`).
Obliczenie używa wzoru Haversine'a dla promienia Ziemi `6 371 000 m`, a wynik
`1576.2547 m` jest zaokrąglony do najbliższego pełnego metra. Nie użyto
lokalizacji mieszkańca.

## Offline behavior

Snapshot jest statyczny, wersjonowany i odczytywany wyłącznie z lokalnego
pliku. Runtime i testy nie pobierają go z Internetu, nie wymagają dostępności
GIOŚ, zmiennych środowiskowych ani bazy danych.

## Privacy

Plik nie zawiera identyfikatora użytkownika, współrzędnych mieszkańca, origin
Attendance, członkostwa Crew, geometrii trasy, adresu e-mail ani lokalizacji
urządzenia. Jedyna relacja geograficzna to wydarzenie → stacja pomiarowa.

## Neo4j

Historia Air Quality nie jest zapisywana w Neo4j. Snapshot nie tworzy węzłów,
relacji, constraintów ani indeksów.

## Updating snapshot

Aktualizacja jest wyłącznie ręczna:

1. pobierz oficjalny pomiar GIOŚ;
2. zweryfikuj stację i jej identyfikator;
3. znormalizuj jednostkę do `µg/m³`;
4. zapisz stały czas ISO 8601 z offsetem `Europe/Warsaw`;
5. zaktualizuj JSON i prawdziwy opis pochodzenia;
6. uruchom testy walidacji kontraktu i deterministyczności;
7. przejrzyj diff.

Nie należy dodawać automatycznego downloadera ani odświeżania tego pliku.
