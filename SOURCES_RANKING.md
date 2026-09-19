# FlowBB — które źródła wczytywać (ranking)

Stan na **2026-09-19**. Ocena oparta na `SOURCES.md` oraz na **ponownym sprawdzeniu na żywo** trzech źródeł JSON i nagłówków GTFS (liczby poniżej pochodzą z tego sprawdzenia). Nie sprawdzałem zawartości ZIP-a GTFS ani stron MZK/Pełnej Kultury. Tam opieram się na `SOURCES.md`.

## Wniosek w trzech zdaniach

1. **Bierzemy trzy źródła JSON: Teatr Polski, Cavatina Hall i bb2026.** Każde ma wydarzenia z datą i godziną, a razem dają wystarczająco dużo różnorodnych wydarzeń do seedu.
2. **Wczytujemy je jednorazowo do pliku seed w repo, nie na żywo.** Demo ma przejść bez ręcznego poprawiania danych i bez zależności od cudzych serwerów (`AGENTS.md`, sekcja 2 i 8). Endpoint Teatru nie jest udokumentowany, a liczby się zmieniają.
3. **Transport: tylko GTFS Kolei Śląskich (P1, OTP) i ręcznie jedna linia MZK do `DemoRoutePlanner`.** Reszta odpada.

## Kryteria

Wartość dla demo FlowBB liczy się tak: czy źródło daje **datę i godzinę wydarzenia** (bez tego nie ma "Idę" ani luki powrotowej), **wolumen przyszłych wydarzeń**, **lokalizację** (do mapy i trasy), **stabilny format** (JSON lepszy niż HTML), **ryzyko prawne/reputacyjne** przed miastem oraz **koszt integracji** w 24 godzinach.

## Ranking

| # | Źródło | Format | Godzina wydarzenia | Wolumen (przyszłe) | Lokalizacja | Ryzyko | Koszt | Decyzja |
|---|---|---|---|---|---|---|---|---|
| 1 | Teatr Polski `/api/repertoire` | JSON | tak (UTC) | 44 sesje / 13 spektakli | nazwa sceny | niskie | niski | **BIERZEMY (P0)** |
| 2 | Cavatina Hall `/wp-json/wp/v2/events` | JSON | tak (`acf`) | 370 z 1236, 156 w ciągu 3 mies. | jedna stała sala | niskie | niski | **BIERZEMY (P0)** |
| 3 | bb2026.pl `tribe/events/v1` | JSON | dla części | 22 od dziś, 8 jednodniowych | 5 z 22 ma miejsce | niskie | średni | **BIERZEMY (P0, wybiórczo)** |
| 4 | GTFS Kolei Śląskich | ZIP/GTFS | rozkład | 6 stacji w mieście | tak (lat/lon) | niskie | średni | **BIERZEMY (P1, OTP)** |
| 5 | MZK — PDF linii 7 | PDF | rozkład | 1 linia ręcznie | przystanki po nazwie | średnie | średni | **RĘCZNIE do `DemoRoutePlanner`** |
| 6 | Pełna Kultura | HTML | tekst po polsku | nieznany | brak | niskie | wysoki | pomijamy |
| 7 | Kalendarium UM `bielsko-biala.pl` | HTML | niesprawdzone | niesprawdzone | niesprawdzone | ? | ? | tylko jeśli zostanie czas |
| — | bielsko.info RSS, BCK RSS | RSS | **brak dat wydarzeń** | — | — | — | — | odpada |
| — | Biletyna | HTML | — | — | — | **wysokie** | — | odpada |
| — | `rozklady.bielsko.pl` (API OnTime) | wewn. API | — | — | — | **wysokie** | — | odpada |
| — | bielskonews, 43300, portal miasta, Bielski Rynek, imprezybielsko | różne | — | — | — | — | — | odpadają |

## Uzasadnienie i pułapki (tylko źródła, które bierzemy)

### 1. Teatr Polski — najlepsze, zaczynamy od niego

- Jedyne źródło z **pojemnością i wolnymi miejscami** (`capacity`, `freeSeats`): 43 z 44 sesji ma pojemność, 10 jest wyprzedanych. To gotowy, realny sygnał popytu.
- Trzy sceny: Duża Scena, Mała Scena i Salon Muzyczny w Zamku Książąt Sułkowskich. **Zamek to inny adres niż teatr**, więc słownik lokali potrzebuje trzech pozycji, nie jednej.
- Pułapki: `date` jest w **UTC** (konwersja do `Europe/Warsaw`); `status` ma wartości `open` (40), `blocked` (3) i `null` (1), więc odfiltrujcie `blocked`/`null`; część sesji jest już w przeszłości (najwcześniejsza 5.09), więc filtr `date >= teraz`.
- Słabość: endpoint jest wewnętrzny dla frontu strony. Może zniknąć bez ostrzeżenia, dlatego snapshot do seeda, nie zależność w runtime.
- Mała liczba unikalnych spektakli (13) wystarczy: FlowBB potrzebuje sesji, nie katalogu.

### 2. Cavatina Hall — największy wolumen, najmniej pracy z lokalizacją

- Jedna sala = **jedno geokodowanie**. 370 przyszłych wpisów daje bufor na wybór terminów pod demo.
- Pułapki:
  - **Termin jest w `acf.event_datetime`**, nie w `date` (data publikacji).
  - Format czasu jest **niespójny**: `2026-11-05 19:00` (948 wpisów) i `2026-12-07 17:00:00` (283). Parser musi przyjąć oba.
  - 5 wpisów bez daty i 227 bez kategorii. Tytuły się powtarzają (jedno wydarzenie w wielu terminach).
  - Nie sprawdzałem filtrowania po stronie API. Filtr `>= dziś` robimy po pobraniu.
- **Pobierajcie z `_fields=id,title,link,acf,event_category`.** Bez tego jedna strona po 100 wpisów waży ok. 1,1 MB (`content.rendered`, `yoast_head_json`), a stron jest 13.

### 3. bb2026.pl — narracja pitchu, mało konkretnych wydarzeń

- Tytułowy kontekst "Polska Stolica Kultury 2026" świetnie pasuje do tematu "Bielsko-Biała 2030", więc warto mieć **2-3 wydarzenia stąd w demo**.
- Ale z 22 wydarzeń tylko **8 jest jednodniowych**, a `venue` ma tylko 5. Wielodniowe zakresy ("Projekt Arting", cały październik) nie mają sensu jako "idę o 19:00". Bierzemy tylko jednodniowe z miejscem.
- **Lista `venues` (31 miejsc) ma adresy, ale zero współrzędnych.** Użyjcie jej jako punktu startowego dla `data/seed/venues.json` (geokodowanie ręczne lub Nominatim raz, 1 zapytanie/s).

### 4. GTFS Kolei Śląskich — jedyny otwarty feed transportowy (P1)

- Sprawdzone dziś: `200`, 2 443 926 B, zmodyfikowany 2026-09-16. Zawartość (6 stacji, kalendarz do 2026-12-13) pochodzi z `SOURCES.md`.
- Jest w P1 (OTP z limitem 2 h). Do P0 wystarczy wpisać rozkłady kilku stacji ręcznie do `DemoRoutePlanner`.
- Nie pobieramy w runtime: ściągnąć raz do `data/gtfs/`, przyciąć do stacji Bielska-Białej.
- Nie sprawdziłem, czy stacje kolejowe są blisko konkretnych lokali (Teatr, Cavatina, Rynek). To warunek sensu trasy kolejowej w demo, do weryfikacji przed decyzją.

### 5. MZK — ręcznie, jedna linia

- Brak publicznego GTFS, więc pełny parser PDF to zadanie po hackathonie. Do demo przepisać **jedną linię** (np. 7) do `DemoRoutePlanner`.
- Serwer ma niekompletny łańcuch TLS. `SOURCES.md` opisuje obejście bez wyłączania weryfikacji.
- W pitchu: prośba do MZK/UM o GTFS i GTFS-RT jako element wdrożenia.

## Zasady wczytywania

1. **Jedna droga, jeden skrypt:** skrypt pobierający i normalizujący dane trafia do `data/seed/` (obszar Data/PostGIS Leada), a jego wynik (`events.seed.json`) jest **commitowany**. Demo nie odpytuje zewnętrznych API.
2. **Zdarzenia prawdziwe, popyt syntetyczny.** Tytuły i terminy są realne, ale `AttendanceIntent` w seedzie już nie. UI musi pokazać `DEMO DATA / SYMULACJA` i nie sugerować, że liczniki są prawdziwe. `capacity`/`freeSeats` z Teatru to sygnał realny, nie mieszać go z syntetycznym popytem w jednym liczniku.
3. **Wybór do demo:** ok. 8-12 wydarzeń: 4-5 sesji Teatru, 4-5 z Cavatiny, 2-3 z bb2026. Termin na najbliższe dni, żeby scenariusz "82 → 83" miał sens. Gęstość heksagonów zależy od seedu `AttendanceIntent`, nie od źródeł wydarzeń.
4. **Lokalizacja:** ręczny `data/seed/venues.json` (ok. 15-20 lokali). Współrzędnych nie daje żadne źródło.
5. **Czas:** wszystko do `Europe/Warsaw` i ISO 8601.

## Otwarte kwestie

- **Kontrakt:** `contracts/openapi.yaml` jeszcze nie istnieje. Mapowanie źródeł na `Event` z `SOURCES.md` to propozycja, do uzgodnienia z Kubą przy tworzeniu kontraktu. Ten ranking niczego w kontrakcie nie zmienia.
- **Regulamin (sekcja 13 `AGENTS.md`):** skrypt ingestu to kod aplikacji. Zapytajcie organizatora/mentora, czy przygotowanie danych obejmuje taki skrypt przed startem okna, albo napiszcie go dopiero w oknie hackathonu.
- **Warunki korzystania:** nie sprawdzałem regulaminów Teatru, Cavatiny i bb2026. Wszystkie trzy udostępniają dane publicznie, ale w pitchu zaznaczcie źródło i że dane są snapshotem.

## Szybka weryfikacja przed startem (5 minut)

```bash
UA="Mozilla/5.0 (FlowBB)"
curl -s -A "$UA" "https://teatr.bielsko.pl/api/repertoire" | python3 -c "import sys,json; e=json.load(sys.stdin)['events']; print(len(e), e[0]['date'])"
curl -s -A "$UA" "https://cavatinahall.pl/wp-json/wp/v2/events?per_page=3&_fields=id,title,acf"
curl -s -A "$UA" "https://bb2026.pl/wp-json/tribe/events/v1/events?per_page=5&start_date=$(date +%F)" | head -c 400
curl -sI -A "$UA" "https://koleje-ks.pl/gtfs/2025-2026.zip" | head -5
```
