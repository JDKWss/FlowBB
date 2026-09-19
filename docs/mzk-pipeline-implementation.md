# MZK Bielsko-Biała — plan implementacji v2 (PDF → GTFS → baza)

Wersja poprawiona. Zmiany względem v1 są w sekcji 11, zmiany wprowadzone po v2 w sekcji 12. Stan wiedzy: 2026-09-19.

> **Stan realizacji (2026-09-19):** powstał proof of concept, który odbiega od tego planu (Node zamiast Pythona, ścieżka `data/gtfs/mzk/`, zakres: linia 7 + N1/N2, bez GTFS/OTP). Szczegóły i nowe ustalenia są w sekcji 13, a instrukcja uruchomienia w `data/gtfs/mzk/README.md`.

Oznaczenia: **[Z]** = zweryfikowane na prawdziwych plikach, **[?]** = założenie do sprawdzenia, **[NIEZWERYFIKOWANE]** = wartość wpisana z pamięci lub odczytu, wymaga potwierdzenia przed użyciem jako oczekiwanie testu.

---

## 0. Decyzje przed startem (blokują etap A)

### D0. Kontakt z MZK — decyzja zespołu: nie piszemy

Wiadomo, że MZK produkuje GTFS (rozkłady są w Google Maps z GTFS-RT od maja 2025, zasilają aplikację itsBB i portal `rozklady.bielsko.pl`), ale publicznego adresu feedu nie znaleziono. Zespół zdecydował, że **nie wysyłamy zapytania do MZK**. Konsekwencje:

- pipeline PDF → GTFS jest ścieżką docelową, a nie prowizorką do czasu dostania feedu,
- ryzyko "oficjalny feed istnieje, a my o nim nie wiemy" jest świadomie zaakceptowane (sekcja 9),
- w pitchu dane opisujemy jako **wtórne, z publicznych rozkładów MZK**, ze wskazaniem źródła.

### D1. Które linie — rozdzielone na dwie role

| Rola | Linia | Uzasadnienie |
|---|---|---|
| **Linia testowa parsera** | **7** (oba kierunki) | już zweryfikowana: 28 stron, kolejność stron potwierdzona, pozycje kolumn odczytane; fixtures powstają na niej |
| **Linie demo** | **do ustalenia** | musi pokrywać scenariusz demo |

Kandydaci do linii demo **[NIEZWERYFIKOWANE]** — trasy przepisane z rozkladzik.pl (wrzesień 2026), nie sprawdzone własnymi plikami PDF. Zweryfikowana jest tylko linia 7.

- **7** — Karbowa Hala Sportowa ↔ Wapienica Dzwonkowa; przez Szyndzielnię, pl. Żwirki i Wigury, 3 Maja Dworzec, Piastowska Dworzec, Szpital Miejski
- **4** — Langiewicza Basen ↔ Os. Wojska Polskiego; przez Os. Złote Łany, pl. Żwirki i Wigury, PCK PKP Lipnik
- **16** — Os. Sarni Stok ↔ Wapienica Zapora; przez Warszawska Dworzec, Piastowska Dworzec, Sarni Stok CH
- **N1** — Zajezdnia MZK ↔ Wapienica Centrum (nocny powrót na zachód)
- **N2** — Zajezdnia MZK ↔ Hałcnów Kościół; przez Os. Złote Łany, Kwiatkowskiego PKP Północ (nocny powrót na wschód)

Ten zestaw dawałby: trzy punkty styku z koleją (Bielsko-Biała Główna, Lipnik, PKP Północ), przesiadkę autobus–autobus na pl. Żwirki i Wigury, nocne powroty do Złotych Łanów i Wapienicy. 10 plików PDF.

**Konflikt do rozstrzygnięcia:** w poprzednich wersjach pojawił się inny scenariusz ("Aleksandrowice → Centrum"). Trzeba wybrać jeden i dopisać tu linie, które go obsługują. Do czasu rozstrzygnięcia pobieramy tylko 7 (testowa) i wstrzymujemy resztę.

### D2. Kontrakt `RouteResult`

Uzgodnić z Backend #1 i frontendem **przed** implementacją `DemoRoutePlanner` (pola: kroki, godziny, linie, przystanki, powrót). Bez tego ścieżka awaryjna z 6.2 nie jest ścieżką awaryjną, tylko drugim projektem.

---

## 1. Cel i definicja ukończenia

**Cel:** zamienić publiczne rozkłady przystankowe MZK (PDF) na dane, z których korzystają routing (OTP albo `DemoRoutePlanner`) i PULSE (np. alert "brak powrotu po 22:00").

**Wyjścia pipeline'u, w kolejności ważności:**
1. `data/mzk/parsed/departures.json` — odjazdy z przystanków (wystarcza do demo)
2. `data/mzk/parsed/stops.json` — przystanki ze współrzędnymi
3. `data/mzk/parsed/gtfs/mzk-derived.zip` — GTFS dla OTP
4. Tabele `transit_*` w PostgreSQL (sekcja 6.1)

**Definicja ukończenia:**
- `python -m pipeline all --lines 7` generuje pliki 1–3 dla obu kierunków linii 7 bez ręcznej interwencji
- **Test złoty przechodzi — pod warunkiem, że jego wartości oczekiwane zostały wcześniej zweryfikowane i podpisane (etap A0).** Dopóki fixture nie jest podpisany, ten punkt DoD nie obowiązuje i nie wolno go "naprawiać" zmianami w parserze
- Ręczne porównanie 3 przystanków z `rozklady.bielsko.pl` zgadza się co do minuty
- `report.md` nie zawiera błędów krytycznych
- Pliki 1–3 są w repo, więc demo działa bez internetu
- Import do bazy (6.1) kończy się bez błędów, a zapytanie z 6.3 zwraca `has_return` dla wydarzenia z demo

---

## 2. Fakty

### [Z] Zweryfikowane

- Strona z listą: `https://komunikacja.bielsko-biala.pl/index.php/rozklad-jazdy-do-wydruku/`. **Statyczny HTML (141 KB) zawiera wszystkie 112 linków do PDF-ów** — nie jest to akordeon dociągany JS-em (pobrane `curl --cacert bundle.pem`). HEAD dał rozmiar dla 111 plików: **razem 353 MB, największy 8 MB**.
- Niekompletny łańcuch TLS. Działa `curl --cacert bundle.pem`, gdzie bundle = systemowe CA + pośredni Sectigo (`http://crt.sectigo.com/SectigoPublicServerAuthenticationCADVR36.crt`, DER → PEM). `openssl verify -untrusted` → `OK`.
- PDF linii 7 kier. Wapienica Dzwonkowa: 28 stron, strona = przystanek, generator PDF24, tekst osadzony.
- **Kolejność stron = kolejność trasy.** Nagłówki: Szyndzielnia 13 → Armii Krajowej Karbowa 11 → Karbowa Hala Sportowa 01 → Karbowa Hala Sportowa 03 → Karbowa Dębowiec 05 → … → Plac Żwirki i Wigury 01 → Hotel Prezydent 05 → 3 Maja Dworzec 01 → Piastowska Dworzec 02 → Słowackiego BCK → … → Dzwonkowa Cieszyńska 02. Zgodne z "Trasą przejazdu" ze strony 14.
- **Dwie kolejne strony o tej samej nazwie i różnych tabliczkach** ("Karbowa Hala Sportowa 01" i "…03") — nazwa sama nie identyfikuje przystanku.
- **Przystanek końcowy nie ma własnej strony** — jego nazwa jest w `KIERUNEK:`.
- Nagłówek strony: `PRZYSTANEK: <nazwa> <nr tabliczki>`, `KIERUNEK: <cel>`, `Obowiązuje od: 01.09.2026 r.`. Numer tabliczki bywa nieobecny (Słowackiego BCK).
- Pozycje x nagłówków kolumn (strona 1): `Godzina` 17, `Dni Robocze` 114, `Dni Robocze wakacyjne` 245, `Soboty` 364, `Niedziele i Święta` 452.
- Komórka: minuty rozdzielone `, ` z opcjonalnymi flagami (`53#`, `52R`, `45#N`).
- Legenda: `#` = zjazd do zajezdni, `N` = nie kursuje 25.12 i w pierwszy dzień Wielkanocy, `R` = nie kursuje 25.12, w Nowy Rok i w pierwszy dzień Wielkanocy.
- `pdftotext -bbox-layout` daje słowa z pozycjami — to jest narzędzie do parsowania. `-layout` tylko do podglądu; tam `\f` rozdziela strony, więc `^PRZYSTANEK` nie pasuje na stronach 2+ bez podziału.
- Brak `pypdf`, `pdfplumber`, `pdfminer`, `fitz`. Jest `pdftotext` (poppler) i Python 3.14.
- W repo nie ma Playwrighta ani skryptu `screenshots.py` (repo zawiera tylko `README.md` i `docs/`).
- OSM (Overpass, bbox `49.72,18.90,49.92,19.20`): 887 węzłów `highway=bus_stop`, 875 z nazwą, 486 unikalnych nazw. Nazwy z PDF-ów występują w OSM, po dwa węzły na nazwę. Numery `ref` w OSM **nie odpowiadają** numerom tabliczek MZK.
- Overpass bywa niedostępny (504 przy pierwszej próbie). Wynik cache'ować do pliku.

### [NIEZWERYFIKOWANE] Warianty tras — zakładamy, że występują

Poprzednie wersje planu twierdziły, że linia 7 w jednym kierunku zaczyna się i wraca na Karbowa Hala Sportowa, a linia 16 zaczyna się i kończy na Warszawska Dworzec. Tego nie sprawdzono na plikach (potwierdzone jest tylko powtórzenie nazwy z różnymi tabliczkami, patrz wyżej). Plan i tak przyjmuje, że pętle są możliwe:

- `stop_id` **nie może** wynikać z samej nazwy przystanku — sekwencja jest pozycyjna (`seq`), powtórzenie tego samego przystanku w kursie jest legalne w GTFS i musi przeżyć import
- kontrola "odległość między kolejnymi przystankami 100–2000 m" musi dopuszczać 0 m dla powtórzenia tej samej tabliczki
- nie każdy kurs jedzie pod ten sam przystanek → flagi w komórkach mogą kodować warianty, nie tylko wyjątki kalendarzowe

### [?] Do sprawdzenia

- Część PDF-ów może mieć 3 kolumny (bez `Dni robocze wakacyjne`) → kolumny wykrywamy dynamicznie **i twardo walidujemy** (5.2)
- Linie nocne N1/N2 mogą mieć inny zestaw typów dnia niż cztery standardowe (np. "noc z pt. na sob.")
- Nazwy w nagłówkach nie zawsze identyczne z OSM (kolejność słów, skróty)

---

## 3. Struktura repo i uruchamianie

```
data/mzk/
├── README.md
├── pipeline/
│   ├── __init__.py
│   ├── config.py          # URL-e, progi, ścieżki
│   ├── fetch.py           # krok 1
│   ├── extract.py         # krok 2
│   ├── assemble.py        # krok 3
│   ├── stops.py           # krok 4
│   ├── calendar.py        # krok 5a
│   ├── gtfs.py            # krok 5b
│   ├── load_db.py         # krok 6 (import do PostgreSQL)
│   ├── validate.py        # krok 7
│   └── cli.py
├── tests/
│   ├── fixtures/
│   │   ├── l7_p1_bbox.html        # wycinek bbox, ~kilkadziesiąt KB
│   │   └── l7_p1_expected.json    # WYMAGA PODPISU, patrz A0
│   └── test_*.py
├── calendar_config.json
├── stops_overrides.json
├── raw/                   # .gitignore
├── cache/                 # .gitignore
└── parsed/                # WYNIK, commitowany
    ├── departures.json
    ├── trips.json
    ├── stops.json
    ├── calendar_days.json
    ├── gtfs/mzk-derived.zip
    └── report.md
Dockerfile.mzk
```

- **Zależności:** wyłącznie stdlib Pythona + `pdftotext` z `poppler-utils`. Zero `pip install` na laptopach zespołu. Wyjątek: import do bazy (`load_db.py`) może użyć sterownika PostgreSQL albo wygenerować plik SQL/CSV ładowany `psql \copy`.
- **Docker** (`python:3.12-slim` + `poppler-utils`) jako sposób uruchamiania — eliminuje różnice systemów, w szczególności brak poppler na Windowsie.
- Każdy krok czyta pliki poprzedniego i zapisuje własne → można go uruchamiać i debugować osobno.

---

## 4. Kontrakty danych

**`raw/manifest.json`** (krok 1):
```json
[{"url": "https://komunikacja.bielsko-biala.pl/wp-content/uploads/2026/08/7-kier.-Wapienica-Dzwonkowa.pdf",
  "file": "raw/7-kier.-Wapienica-Dzwonkowa.pdf",
  "sha256": "...", "last_modified": "...", "etag": "...",
  "line": "7", "kind": "kier",
  "label": "Wapienica Dzwonkowa",
  "label_source": "anchor_text",
  "uploads_date": "2026/08"}]
```
`kind` ∈ `kier` | `pozostale` | `dodatkowe` | `zajezdnia`. W MVP bierzemy tylko `kier`.

**`parsed/departures.json`** (krok 2) — jeden rekord = jedna strona PDF:
```json
{"line": "7", "direction": "Wapienica Dzwonkowa", "seq": 1,
 "stop_name": "Szyndzielnia", "plate": "13", "valid_from": "2026-09-01",
 "source": "7-kier.-Wapienica-Dzwonkowa.pdf", "page": 1,
 "columns_raw": ["Godzina", "Dni Robocze", "Dni Robocze wakacyjne", "Soboty", "Niedziele i Święta"],
 "departures": {
   "weekday":        [{"t": "05:01", "flags": []}, {"t": "08:53", "flags": ["#"], "depot_run": true}],
   "weekday_holiday": [],
   "saturday":       [{"t": "04:57", "flags": []}],
   "sunday":         [{"t": "05:52", "flags": ["R"]}]},
 "legend": {"#": "zjazd do zajezdni", "N": "...", "R": "..."}}
```
- `seq` = numer strony = kolejność trasy. Nie sortować.
- `columns_raw` zapisujemy w całości, żeby zmiana układu była widoczna w diffie repo.
- `depot_run: true` wyliczane z flagi `#`.

**`parsed/trips.json`** (krok 3):
```json
{"trip_id": "7_0_wd_003", "line": "7", "direction_id": 0, "service": "weekday",
 "stops": [{"seq": 1, "t": 301, "estimated": false}, {"seq": 2, "t": 307, "estimated": false}],
 "complete": true, "end_reason": null, "match_cost_max": 1.5}
```
`end_reason` ∈ `null` | `depot` (`#`) | `shortened` | `unmatched`. `t` to minuty od północy dnia usługowego (w bazie i GTFS przeliczane na sekundy).

**`parsed/stops.json`** (krok 4):
```json
{"stop_id": "karpacka-dom-kultury_05", "name": "Karpacka Dom Kultury", "plate": "05",
 "lat": 49.79, "lon": 19.03, "osm_id": 123456, "match": "auto|override|missing", "score": 0.93,
 "transfer_group": null}
```
`stop_id` = `slug(nazwa)_tabliczka` (albo `slug(nazwa)_x`, gdy tabliczki brak). Identyfikuje fizyczny przystanek, wspólny dla wielu linii. Kolejność w trasie niesie `seq` w `departures.json` i `stop_sequence` w kursach.

**`parsed/calendar_days.json`** (krok 5a):
```json
[{"day": "2026-11-01", "service": "sunday", "public_holiday": true, "note": "Wszystkich Świętych"}]
```

---

## 5. Moduły

### 5.0 `A0` — weryfikacja fixture'a (30 min, blokuje etap B)

**To jest osobny, obowiązkowy krok przed napisaniem `extract.py`.** Wartości testu złotego pochodziły z odczytu, a jednocześnie test był w DoD. To zamyka się w pętlę: przy błędnej wartości oczekiwanej implementujący "naprawia" poprawny parser, aż zacznie zwracać złe dane, i to zostaje uznane za ukończenie etapu B.

Procedura:
1. `pdftotext -bbox-layout -f 1 -l 1 7-kier.-Wapienica-Dzwonkowa.pdf tests/fixtures/l7_p1_bbox.html`
2. Otworzyć stronę 1 PDF-a **wzrokowo** i przepisać ręcznie całą zawartość tabeli do `tests/fixtures/l7_p1_expected.json`.
3. Na górze pliku nagłówek: `"verified_by": "<imię>", "verified_at": "<data>"`.
4. Drugi członek zespołu sprawdza niezależnie i dopisuje się do `verified_by`.

Wartości poniżej są **[NIEZWERYFIKOWANE]** — to punkt startowy do porównania, nie oczekiwanie testu:

> Szyndzielnia 13, strona 1 linii 7 kier. Wapienica Dzwonkowa:
> `weekday` ≈ 17 odjazdów (05:01, 06:01, 07:01, 08:01, 08:53#, 09:41…17:41 co godzinę, 19:11, 20:41, 22:11);
> `weekday_holiday` ≈ identyczny z `weekday`;
> `saturday` ≈ 26 odjazdów (04:57, 05:37, 06:17, 06:57, …, 21:57, 22:45#);
> `sunday` ≈ 18 odjazdów (05:52R…08:52R, 09:52…17:52, 18:57N, 19:57N, 20:57N, 21:57N, 22:45#N).

Jeśli ręczny odczyt da inne liczby — **wygrywa ręczny odczyt**, a ten akapit się usuwa.

Dopóki `verified_by` jest puste, `test_golden` jest oznaczony `@skip("fixture unverified")` i nie liczy się do DoD.

---

### 5.1 `fetch.py` (krok 1, ~45 min)

1. **Lista PDF-ów jest w statycznym HTML [Z]** (112 linków w 141 KB). Wystarczy pobranie strony i wyciągnięcie `href` regexem lub `html.parser`. Kontrola: liczba znalezionych PDF-ów dla `kind == kier` i pozostałych ma się zgadzać z ok. 112; duża rozbieżność = zmiana strony, `fetch` kończy się błędem.
2. Pobranie przez `curl --cacert cache/bundle.pem` (`subprocess`) — sposób **[Z]**. Wersja czysto pythonowa z `ssl.create_default_context(cafile=...)` jest niesprawdzona; jeśli ktoś ją zrobi, ma przejść ten sam test.
3. `cache/bundle.pem`: jeśli brak — zbuduj z `/etc/ssl/certs/ca-certificates.crt` + pobrany pośredni (DER → PEM przez `openssl x509 -inform DER`). Adres pośredniego w `config.py`; przy 404 komunikat: "sprawdź AIA certyfikatu: `openssl x509 -in leaf.pem -noout -text`".
4. **`label` i `line` bierz z HTML, nie z nazwy pliku.** Nazwy plików są niespójne (`1-kier.-…`, `Linia-nr-3-kier.-…`, `13W-…`, `18-Osiedle-Zlote-Lany.pdf` bez `kier.`), a tekst kotwicy jest ustrukturyzowany: "Kierunek: Os. Beskidzkie", "Kierunek: Wapienica Park Przemysłowy II". Numer linii — z nagłówka sekcji. Nazwa pliku służy wyłącznie jako klucz cache'a i nazwa pliku na dysku. Zapisuj `label_source`, żeby było widać, skąd pochodzi.
5. Pobieraj tylko `--lines` i tylko `kind == kier`. Warunkowy GET: `If-Modified-Since` / `If-None-Match` z manifestu → `304` → pomiń.
6. **Duplikaty:** niektóre kierunki mają dwie wersje (np. `18-kier.-Os.-Zlote-Lany.pdf` z 2025/10 i `18-Osiedle-Zlote-Lany.pdf` z 2026/08). Reguła: w obrębie (linia, cel) wygrywa późniejsza data w ścieżce `uploads/RRRR/MM`; starszy trafia do `report.md`.
7. Współbieżność **maks. 4**, `User-Agent` identyfikujący projekt. To serwis miejski.
8. Pełny zestaw ≈ 353 MB — domyślnie pobieramy tylko `--lines`.

**Akceptacja:** manifest zawiera oba kierunki linii 7 z `sha256` i `label` z kotwicy; ponowne uruchomienie nie pobiera nic.

---

### 5.2 `extract.py` (krok 2, 2–3 h, największe ryzyko)

**Wejście:** jeden PDF. **Wyjście:** lista rekordów `departures.json`.

1. `pdftotext -bbox-layout <pdf> -` → XHTML z `<page>` i `<word xMin yMin xMax yMax>`. Parsować `xml.etree`.
2. Dla każdej strony:

   **Nagłówek.** Znajdź `PRZYSTANEK:`, weź słowa z tego samego wiersza (`|Δy| < 3`, `x < 400`). Złóż nazwę, odetnij końcową liczbę 1–2 cyfrową jako `plate` (`\s(\d{1,2})$`). Nazwy zaczynające się od cyfry ("3 Maja Dworzec") są bezpieczne, bo odcinamy tylko końcówkę. `KIERUNEK:` → `direction`. `Obowiązuje od:` → `valid_from`.

   **Kolumny — z twardą walidacją.** Znajdź wiersz nagłówka tabeli (słowo `Godzina`, `y ≈ 208`). Dopasuj znane wzorce: `Godzina`, `Dni Robocze`, `Dni Robocze wakacyjne`, `Soboty`, `Niedziele i Święta`. Granice kolumn = środki między kolejnymi `xMin`.

   > **Po zbudowaniu listy kolumn sprawdź, czy KAŻDE słowo z wiersza nagłówka zostało skonsumowane przez znany wzorzec. Jakiekolwiek nierozpoznane słowo → wyjątek z numerem strony, nazwą pliku i pełną treścią wiersza nagłówka.**

   Bez tego algorytm przy nieznanym nagłówku (np. "noc z pt. na sob." na N1) po prostu zbuduje mniej kolumn i **cicho** wyprodukuje rekord z częścią odjazdów.

   **Wiersze.** Słowa poniżej nagłówka i powyżej legendy. Pierwsza kolumna (`x < ~60`) to godzina. Pozostałe przypisz do kolumny po `xMin`. Tokeny w komórce: `(\d{2})([#A-Za-z*]*),?`. Wpis `01, 53#` to dwa odjazdy.

   **Legenda.** Wiersze pod tabelą zaczynające się od pojedynczego znaku i ` - `.

3. **Godziny po północy — decyzja, nie reguła.** Dla linii dziennych godzina `0–3` należy do poprzedniej doby usługowej i zapisujemy ją jako `24–27`. **Dla linii nocnych (N1, N2) ta reguła zastosowana globalnie zamieni rozkład 23:40→04:00 w 23:40→28:00** i trzeba świadomie rozstrzygnąć, czy tego chcemy w `calendar.txt`. Do czasu rozstrzygnięcia: reguła działa tylko dla linii bez prefiksu `N`, a linie nocne wchodzą do pipeline'u dopiero po decyzji. Zapisz decyzję w `config.py`, nie w kodzie parsera.
4. Pomiń puste wiersze i wiersze bez godziny.
5. **Kontrole spójności (wyjątek, nie ostrzeżenie):** liczba rekordów = liczba stron (`pdfinfo`); `seq` rośnie od 1 bez dziur; `direction` identyczny na wszystkich stronach; `valid_from` identyczny na wszystkich stronach.

**Akceptacja:** test złoty z A0 przechodzi; wszystkie 28 stron linii 7 daje rekord; lista `stop_name` zgodna z sekcją 2.

**Pułapki:** pusta komórka przesuwa tekst w `-layout` (dlatego x, nie spacje); obrócony tekst "Trasa przejazdu" jest nieczytelny po ekstrakcji, ale go **nie parsujemy** — cel kursu bierzemy z `KIERUNEK`.

---

### 5.3 `assemble.py` (krok 3, ~2,5 h)

**Cel:** z odjazdów przystankowych odtworzyć kursy.

**Wejście:** rekordy jednej pary (`line`, `direction`) po `seq`, plus pseudo-przystanek końcowy (`terminus`) o nazwie z `KIERUNEK`, bez odjazdów.

#### Dlaczego nie greedy

Algorytm "najwcześniejszy nieprzypisany odjazd w oknie `MAX_HOP` = 25 min" działa tylko wtedy, gdy takt linii jest **większy** niż okno. Na linii kursującej co 10 minut w oknie mieszczą się dwa–trzy kolejne autobusy, a first-fit weźmie pierwszy wolny, niekoniecznie właściwy. Efekt nie jest błędem: kurs dostaje czasy sąsiedniego autobusu, czasy nadal rosną, odcinki są w normie, wszystkie kontrole przechodzą. Rozkład jest po prostu nieprawdziwy. Kontrola "mediana odcinka kursu ±3 min od mediany linii" tego nie łapie, bo przesunięty kurs ma normalne odcinki.

Dodatkowo `MAX_HOP` jako jedna globalna stała nie ma sensu: odcinki na różnych fragmentach linii różnią się rzędem wielkości.

#### Dopasowanie monotoniczne (zamiast greedy)

Autobusy jednej linii się nie wyprzedzają, więc przyporządkowanie odjazdów przystanku `i` do przystanku `i−1` **musi być rosnące**. To zwykłe dopasowanie dwóch posortowanych sekwencji, liczone DP.

Dla każdego klucza dnia osobno, dla każdej pary sąsiednich przystanków:

```
T = czasy aktywnych kursów na przystanku i-1   (rosnąco)
D = odjazdy z przystanku i                      (rosnąco)

f(a,b) = min(
    f(a-1, b)   + PENALTY_END,          # kurs kończy się na i-1
    f(a, b-1)   + PENALTY_NEW,          # d_b zaczyna nowy kurs na i
    f(a-1, b-1) + cost(t_a, d_b)        # sparowanie
)
cost(t,d) = |(d - t) - median_hop|  gdy  lo <= d-t <= hi,  inaczej +inf
```

O(n·m), backtracking daje przypisanie. Nieprzypisane `T` = kursy skrócone, nieprzypisane `D` = kursy zaczynające się na tym przystanku (wyjazdy z zajezdni, kursy skrócone od środka).

**Dwa przebiegi:**
1. szerokie okno (`lo=0, hi=25 min`), `median_hop` nieznana → `cost = 0` dla wszystkich dopuszczalnych; z wyniku policz **medianę tego konkretnego odcinka**
2. wąskie okno (`median_hop ± 3 min`), pełna funkcja kosztu

Zapisz `match_cost_max` per kurs — wysoki koszt to sygnał do ręcznego sprawdzenia, i to jest kontrola, która faktycznie łapie przesunięcia.

#### Zjazdy do zajezdni

**Odjazdy z flagą `#` nie wchodzą do `stop_times`.** `#` = zjazd do zajezdni (legenda **[Z]**). Taki kurs urywa się w dowolnym punkcie trasy, a OTP posadzi na niego pasażera. W teście z A0 `08:53#` siedzi w środku dnia roboczego, między 08:01 a 09:41 — to nie jest krawędź rozkładu, tylko dziura w porannym szczycie.

W MVP: `depot_run: true` → kurs pomijany w GTFS (w bazie zapisany z `is_depot_run = true`), licznik w `report.md`. Po hackathonie można je modelować poprawnie z headsignem "Zajezdnia".

#### Przystanek końcowy

Czas przyjazdu = ostatni odjazd + estymata ostatniego odcinka. **Nie mediana całej linii** — odcinki śródmiejskie i peryferyjne różnią się dwukrotnie. Kolejność preferencji:
1. **Zmierzony odcinek z pliku przeciwnego kierunku.** Przystanek końcowy kierunku A jest przystankiem `seq=1` kierunku B, więc odcinek B(1)→B(2) pokrywa ten sam fragment ulicy. To wartość zmierzona, nie zgadnięta.
2. Mediana 2–3 ostatnich odcinków tego kierunku.

Oznacz `estimated: true`. Niezależnie: **cross-check nazwy** — terminus A musi równać się `stop_name` przy `seq=1` w B. Rozjazd = błąd w `KIERUNEK` albo w kolejności stron.

**Wskaźniki do `report.md`:** odsetek kursów kompletnych; liczba kursów skróconych i "sierot"; rozkład `match_cost_max`; różnica liczby odjazdów między sąsiednimi przystankami.

**Akceptacja:** na linii 7 ≥ 90% kursów (po odfiltrowaniu `#`) dochodzi do końca; każdy niekompletny ma `end_reason`.

---

### 5.4 `stops.py` (krok 4, 1–2 h)

1. **Overpass**, bbox **rozszerzony**: `49.65,18.80,50.00,19.35`. Sieć wychodzi poza gminę (Mazańcowice, Czechowice-Dziedzice, Hałcnów) — przy ciasnym bboxie końcówki wypadną jako niedopasowane i będzie to wyglądać na błąd normalizacji nazw, a będzie błędem geometrii zapytania. Zapytanie: `node["highway"="bus_stop"](<bbox>); out tags center;`. Zapis do `cache/osm_bus_stops.json`. **Nie odpytywać Overpass podczas demo.** Przy 504 ponowić lub zmienić serwer.
2. **Sprawdź najpierw relacje tras.** `relation["type"="route"]["route"="bus"]["ref"="7"]` — jeśli istnieją dla linii bielskich, dają **uporządkowaną** sekwencję przystanków, co załatwia współrzędne *i* niezależnie weryfikuje kolejność stron. Dopiero gdy relacji nie ma, schodzimy do dopasowania po nazwach.
3. **Normalizacja nazw:** małe litery, bez diakrytyków, rozwinięcie skrótów (`os.` → `osiedle`, `pl.` → `plac`, `ul.` → ``), bez interpunkcji.
4. **Dopasowanie:** kandydaci o podobieństwie ≥ 0,85 (`difflib.SequenceMatcher` po sortowaniu tokenów). Zwykle dwóch (dwa kierunki).
5. **Wybór peronu:** minimalizuj sumę odległości między kolejnymi przystankami trasy (DP po kandydatach, skok ≤ 3 km). Dla pary po przeciwnych stronach jezdni decyduje wektor do następnego przystanku — w ruchu prawostronnym właściwy peron leży po **prawej** stronie kierunku jazdy (znak iloczynu wektorowego). Dokładność 20–40 m wystarcza.
6. **Nie scalaj przystanków po podciągu nazwy.** W zestawie demo mogą być trzy różne przystanki przy tym samym szpitalu: "Wyspiańskiego Szpital Miejski" (7), "Grunwaldzka Szpital Miejski" (4, 16), "Lwowska Szpital Miejski" (N2) **[NIEZWERYFIKOWANE dla 4, 16 i N2]**. Podobnie "Osiedle Wojska Polskiego" vs "Cieszyńska Osiedle Wojska Polskiego" (oba z linii 7, **[Z]** na liście stron). To osobne `stop_id`, ale **zrób z nich punkt przesiadkowy** (`transfer_group` → `parent_station` w GTFS), inaczej OTP nie pozwoli się tam przesiąść i stracisz sensowne trasy.
7. `stops_overrides.json` (klucz `nazwa|plate`) ma pierwszeństwo, `match: "override"`.
8. Niedopasowane (`match: "missing"`) → `report.md`. Cel: 100% linii demo, ≥ 95% reszty.

**Akceptacja:** wszystkie przystanki linii 7 mają współrzędne; odległości między kolejnymi w przedziale 100–2000 m, **z wyjątkiem powtórzenia tej samej tabliczki w pętli, gdzie dopuszczalne jest 0 m**.

---

### 5.5 `calendar.py` (krok 5a, ~30 min)

```json
{"valid_from": "2026-09-01", "valid_to": "2026-12-31",
 "school_holiday_ranges": [],
 "extra_holidays": []}
```

- `school_holiday_ranges` — wpisać z oficjalnego kalendarza roku szkolnego albo komunikatów MZK. **Nie zgadywać.**
- `valid_to` — rozkłady nie podają końca obowiązywania; to ustawienie zespołu, odnotowane w README.
- Święta państwowe wyznaczane algorytmicznie (stałe daty + Wielkanoc/Boże Ciało/Zielone Świątki, Meeus/Jones/Butcher). W święto obowiązuje `sunday`, także gdy wypada w sobotę lub dzień roboczy.
- **Wyjście:** `parsed/calendar_days.json` — po jednym rekordzie na każdy dzień od `valid_from` do `valid_to` (`day`, `service`, `public_holiday`, `note`). To zasila tabelę `transit_calendar_day` i `calendar.txt` / `calendar_dates.txt`.
- Flagi `N` / `R` (nie kursuje 25.12 / Nowy Rok / Wielkanoc) → wyjątki `calendar_dates` typu 2 dla kursów z tymi flagami. W MVP wystarczy je zapisać w `departures.json`; wyjątki po demo.

> **Reguła pustych wakacji.** Dopóki `school_holiday_ranges` jest puste, usługa `mzk_weekday_holiday` **nie trafia do `calendar.txt` ani do `trips.txt`**, a w `calendar_days.json` żaden dzień nie ma `service = weekday_holiday`. Inaczej `mzk_weekday` i `mzk_weekday_holiday` miałyby identyczny zakres i identyczne dni tygodnia, czyli **dwa komplety tych samych kursów**. OTP pokazałby zdublowane odjazdy, a feed byłby formalnie poprawny, więc ani walidator, ani test złoty by tego nie złapały.

---

### 5.6 `gtfs.py` (krok 5b, ~1 h)

Zawartość `parsed/gtfs/mzk-derived.zip`:

- **`agency.txt`** (wymagany): `agency_id=mzk`, `agency_name=MZK Bielsko-Biała (dane wtórne z rozkładów PDF)`, `agency_timezone=Europe/Warsaw`, `agency_url=https://komunikacja.bielsko-biala.pl`
- **`stops.txt`**: `stop_id, stop_name, stop_lat, stop_lon` (+ `parent_station` dla grup przesiadkowych z 5.4.6)
- **`routes.txt`**: `route_id=mzk_7`, `route_short_name=7`, `route_type=3`, `agency_id=mzk`
- **`trips.txt`**: `trip_headsign` = `KIERUNEK`; **`direction_id` wyznaczany deterministycznie** — posortuj dwie wartości `KIERUNEK` w obrębie linii alfabetycznie, pierwsza = 0. Nie "według nazwy pliku": nazwy plików są niespójne (5.1.4) i ten sam kierunek dostałby różne id przy różnych wersjach pliku
- **`stop_times.txt`**: `arrival_time`/`departure_time` ze `stop_time.arr_sec`/`dep_sec`, godziny mogą przekraczać `24:00:00`; `timepoint=0` dla czasów szacowanych; `stop_sequence` **pozycyjny** — ten sam `stop_id` może wystąpić w kursie dwa razy (pętle, sekcja 2) i to jest legalne
- **`calendar.txt`**: usługi `mzk_weekday`, `mzk_saturday`, `mzk_sunday` (+ `mzk_weekday_holiday` **tylko** gdy `school_holiday_ranges` niepuste — 5.5)
- **`calendar_dates.txt`**: święta (usunięcie `weekday`/`saturday`, dodanie `sunday`)
- **`feed_info.txt`**: `feed_publisher_name=FlowBB (nieoficjalne)`, informacja o źródle i dacie pobrania

**Walidacja:** przepuść przez `gtfs-validator` MobilityData **zanim** podasz feed do OTP. OTP potrafi odrzucić wadliwy feed po cichu i stracisz dwie godziny na debugowanie nie tej warstwy. Dopiero potem `build-config.json` z dwoma feedami (Koleje Śląskie + MZK).

---

### 5.7 `validate.py` i `report.md` (krok 7, ~1 h)

`report.md` generowany automatycznie: liczba plików / stron / przystanków / odjazdów / kursów; przystanki bez współrzędnych; kursy niekompletne z `end_reason`; rozkład `match_cost_max`; odfiltrowane zjazdy do zajezdni; duplikaty PDF-ów; ostrzeżenia (kolumna `wakacyjne` pusta, `weekday_holiday` pominięte).

Kod wyjścia ≠ 0 przy przekroczeniu progu jakości. Progi w jednym miejscu — `config.py`.

---

## 6. Integracja z aplikacją

### 6.1 Baza (PostgreSQL + PostGIS)

OTP **nie** korzysta z bazy — dostaje wygenerowany GTFS. Baza jest potrzebna dla PULSE, mapy przystanków i awaryjnego routingu bez OTP.

| Tabela | Po co | Źródło | Szacunek wierszy | Poziom |
|---|---|---|---|---|
| `transit_line_direction` | linia + kierunek + `headsign` | `departures.json` | dziesiątki | 1 (PULSE) |
| `transit_stop` | przystanki, współrzędne, wynik dopasowania do OSM | `stops.json` | kilkaset | 1 |
| `transit_departure` | surowe odjazdy z przystanków, źródło prawdy | `departures.json` | ok. 200 tys. (wszystkie linie), ok. 4 tys. (linia 7) | 1 |
| `transit_calendar_day` | data → typ rozkładu | `calendar_days.json` | ok. 120 | 1 |
| `transit_trip` | złożone kursy | `trips.json` | tysiące | 2 (fallback) |
| `transit_stop_time` | czasy kursu na kolejnych przystankach | `trips.json` | ok. 250 tys. (wszystkie linie) | 2 |
| `transit_source` | pochodzenie danych: URL, sha256, data wgrania PDF | `manifest.json` | ok. 112 | 3 (audyt) |

Liczby wierszy to szacunek z jednej strony linii 7 (78 odjazdów, 28 stron), a nie pomiar.

```sql
CREATE TABLE transit_line_direction (
  line text NOT NULL, direction_id smallint NOT NULL,
  headsign text NOT NULL, valid_from date NOT NULL,
  PRIMARY KEY (line, direction_id));

CREATE TABLE transit_stop (
  stop_id text PRIMARY KEY,                  -- slug(nazwa)_tabliczka, np. karpacka-dom-kultury_05
  name text NOT NULL, plate text,            -- plate NULL, gdy PDF nie podaje numeru (Słowackiego BCK)
  geom geometry(Point,4326),                 -- NULL przy match='missing'
  osm_id bigint, match_score real,
  match text NOT NULL CHECK (match IN ('auto','override','missing')),
  transfer_group text);                      -- punkty przesiadkowe (5.4.6)
CREATE INDEX transit_stop_geom_gix ON transit_stop USING gist (geom);

CREATE TABLE transit_source (
  source_id serial PRIMARY KEY, url text UNIQUE NOT NULL, sha256 char(64) NOT NULL,
  last_modified timestamptz, uploads_date text, valid_from date, fetched_at timestamptz,
  line text NOT NULL, direction_id smallint NOT NULL, superseded boolean NOT NULL DEFAULT false,
  FOREIGN KEY (line, direction_id) REFERENCES transit_line_direction);

CREATE TABLE transit_departure (
  departure_id bigserial PRIMARY KEY,
  source_id int NOT NULL REFERENCES transit_source,
  line text NOT NULL, direction_id smallint NOT NULL,
  stop_seq smallint NOT NULL,                -- numer strony w PDF = kolejność trasy
  stop_id text NOT NULL REFERENCES transit_stop,
  service text NOT NULL CHECK (service IN ('weekday','weekday_holiday','saturday','sunday')),
  dep_sec integer NOT NULL,                  -- sekundy od północy dnia usługowego, może być > 86400
  flags text[] NOT NULL DEFAULT '{}', depot_run boolean NOT NULL DEFAULT false,
  FOREIGN KEY (line, direction_id) REFERENCES transit_line_direction,
  UNIQUE (source_id, stop_seq, service, dep_sec));
CREATE INDEX transit_departure_lookup ON transit_departure (stop_id, service, dep_sec) WHERE NOT depot_run;

CREATE TABLE transit_calendar_day (
  day date PRIMARY KEY, public_holiday boolean NOT NULL, note text,
  service text NOT NULL CHECK (service IN ('weekday','weekday_holiday','saturday','sunday')));

-- poziom 2: awaryjny routing bez OTP
CREATE TABLE transit_trip (
  trip_id text PRIMARY KEY, line text NOT NULL, direction_id smallint NOT NULL,
  service text NOT NULL, complete boolean NOT NULL, is_depot_run boolean NOT NULL DEFAULT false,
  end_reason text CHECK (end_reason IN ('depot','shortened','unmatched')), match_cost_max real,
  FOREIGN KEY (line, direction_id) REFERENCES transit_line_direction);

CREATE TABLE transit_stop_time (
  trip_id text NOT NULL REFERENCES transit_trip ON DELETE CASCADE,
  stop_sequence smallint NOT NULL, stop_id text NOT NULL REFERENCES transit_stop,
  arr_sec integer NOT NULL, dep_sec integer NOT NULL, estimated boolean NOT NULL DEFAULT false,
  PRIMARY KEY (trip_id, stop_sequence));
CREATE INDEX transit_stop_time_stop ON transit_stop_time (stop_id, dep_sec);
```

**Decyzje wynikające ze schematu:**
- **Klucz przystanku to nazwa + numer tabliczki.** Na liście stron linii 7 są dwie kolejne strony o tej samej nazwie ("Karbowa Hala Sportowa 01" i "…03"). Numeru czasem nie ma, stąd `plate` może być `NULL`.
- **Pętle:** ten sam `stop_id` może wystąpić w jednym kursie dwa razy, więc `stop_seq` i `stop_sequence` są kolumnami kluczowymi, a nie `UNIQUE (trip_id, stop_id)`.
- **`depot_run`** jest na odjeździe i na kursie; zapytanie o powrót go wyklucza.
- **Czas w sekundach** (`dep_sec`), a nie `interval`. Godziny 0–3 zapisane jako 24–27 tego samego dnia usługowego dają proste porównania także dla koncertów kończących się po północy.
- **`transit_calendar_day`** zastępuje logikę "który to typ dnia" w każdym zapytaniu. Wakacyjne dni robocze wchodzą tam dopiero po uzupełnieniu `school_holiday_ranges`.
- **`estimated`** oznacza czas przyjazdu na przystanku końcowym, którego nie ma w PDF-ie.
- **Nie potrzeba w bazie:** shapes, taryf, GTFS-RT ani samych plików GTFS. Te powstają w pipeline i trafiają do OTP.
- Powiązanie z resztą FlowBB (wydarzenia, miejsca) jest **przestrzenne** (`ST_DWithin` po `geom`), bez kluczy obcych.

**Import (`load_db.py`):** kolejność ładowania `transit_line_direction` → `transit_stop` → `transit_source` → `transit_calendar_day` → `transit_departure` → `transit_trip` → `transit_stop_time`. Przed importem `TRUNCATE ... CASCADE` w odwrotnej kolejności. Dane są w całości odtwarzalne z `parsed/*.json`, więc idempotencja jest darmowa. Ładowanie masowe przez `COPY`.

### 6.2 Routing

- **OTP:** `mzk-derived.zip` + GTFS Kolei Śląskich + OSM (Geofabrik, śląskie) → `OtpRoutePlanner`.
- **Awaryjnie:** `parsed/demo_routes.json` → `DemoRoutePlanner`, albo zapytanie SQL na `transit_stop_time` (poniżej). Kontrakt `RouteResult` uzgodniony w D2 **przed** implementacją.

Połączenie bezpośrednie A→B (bez przesiadek), do demo z jedną linią wystarczy:
```sql
SELECT a.trip_id, a.dep_sec, b.arr_sec
FROM transit_stop_time a
JOIN transit_stop_time b ON b.trip_id = a.trip_id AND b.stop_sequence > a.stop_sequence
JOIN transit_trip t ON t.trip_id = a.trip_id AND NOT t.is_depot_run
JOIN transit_calendar_day c ON c.day = :day AND c.service = t.service
WHERE a.stop_id = ANY(:stops_near_origin) AND b.stop_id = ANY(:stops_near_dest)
  AND a.dep_sec >= :t
ORDER BY b.arr_sec LIMIT 3;
```
Przesiadek to zapytanie nie obsługuje.

### 6.3 PULSE — brak powrotu

```sql
SELECT EXISTS (
  SELECT 1 FROM transit_calendar_day c
  JOIN transit_departure d ON d.service = c.service
  JOIN transit_stop s USING (stop_id)
  WHERE c.day = :event_day
    AND ST_DWithin(s.geom::geography, :venue::geography, 500)
    AND NOT d.depot_run
    AND d.dep_sec BETWEEN :end_sec + 900 AND :end_sec + 5400
) AS has_return;
```
- `:end_sec` = sekundy od północy dnia wydarzenia. Dla wydarzenia kończącego się po północy dodaj 86400 i porównuj z odjazdami zapisanymi jako 24–27 godziny tego samego dnia usługowego.
- `depot_run = false` jest istotne — zjazd do zajezdni nie jest powrotem dla pasażera.
- Tabela `transit_stop` ma kilkaset wierszy, więc brak indeksu na rzutowaniu `geography` nie ma znaczenia.
- Progi (15/90 min, 500 m) w konfiguracji i jawnie w pitchu jako założenia. Nocą `false` jest odpowiedzią prawdziwą, nie błędem danych.

---

## 7. Testy

| Test | Poziom | Warunek |
|---|---|---|
| **Fixture podpisany** | proceduralny | `l7_p1_expected.json` ma niepuste `verified_by` (dwie osoby) — **blokuje pozostałe** |
| Złoty: strona 1 linii 7 | jednostkowy | zgodność z podpisanym fixture'em |
| Nagłówek kolumn | jednostkowy | nierozpoznane słowo w nagłówku → wyjątek (sztuczny fixture z "noc z pt. na sob.") |
| Kolejność stron | integracyjny | lista `stop_name` linii 7 zgodna z sekcją 2 |
| Liczba stron | integracyjny | liczba rekordów = `pdfinfo` |
| Dopasowanie kursów | jednostkowy | sztuczny rozkład o takcie 10 min: monotoniczność zachowana, brak przesunięć |
| Kursy | integracyjny | ≥ 90% kompletnych na linii 7 |
| Zjazdy | jednostkowy | żaden kurs z `#` nie trafia do `stop_times` |
| Duplikacja usług | jednostkowy | puste `school_holiday_ranges` → brak `mzk_weekday_holiday` w `calendar.txt` i w `calendar_days.json` |
| Pętle | jednostkowy | powtórzony `stop_id` w kursie przeżywa eksport i import |
| Współrzędne | integracyjny | 100% przystanków linii 7; skoki 100–2000 m lub 0 m przy powtórzeniu tabliczki |
| GTFS | integracyjny | `gtfs-validator` bez błędów krytycznych, potem OTP |
| Import do bazy | integracyjny | `load_db.py` na pustej bazie bez błędów; liczby wierszy zgodne z `report.md`; `has_return` zwraca wynik dla wydarzenia z demo |
| Ręczny | akceptacyjny | 3 przystanki = `rozklady.bielsko.pl` co do minuty |
| Odporność | regresyjny | zmieniony układ PDF → błąd z raportem, nie ciche gubienie danych |

Fixtures: wycinek `pdftotext -bbox-layout -f 1 -l 1` (kilkadziesiąt KB), nie cały PDF.

---

## 8. Harmonogram i bramki

Godziny zegarowe, nie "T+N" — bramka w postaci offsetu wypada dokładnie wtedy, gdy ktoś debuguje, i nikt jej nie zauważa. Poniżej przykład dla startu o 20:00; dopasować do faktycznego startu i wpisać na tablicę.

| Etap | Kto | Czas | Punkt kontrolny |
|---|---|---|---|
| **A.** Docker, `bundle.pem`, `fetch --lines 7` | Backend #2 | 45 min | dwa PDF-y linii 7, sha256 w manifeście |
| **A0.** Weryfikacja i podpisanie fixture'a | Backend #2 + 1 os. | 30 min | `verified_by` niepuste |
| **B.** `extract` + test złoty | Backend #2 | 2–3 h | test zielony, 28 stron → 28 rekordów |
| **C.** `stops` | Backend #2 + baza | 1–2 h | 100% przystanków linii 7 |
| **D.** `assemble` + `calendar` | Backend #2 | 2,5 h | ≥ 90% kursów kompletnych |
| **E.** `gtfs` + validator + OTP | Backend #2 | 1 h | graf MZK + KŚ bez błędów |
| **F.** Schemat + `load_db.py` + `has_return` | osoba od bazy | 1–1,5 h | wynik dla wydarzenia demo |
| **G.** Kolejne linie | Backend #2 | 2–3 h | `report.md` bez błędów krytycznych |

**Bramki go / no-go:**

- **BRAMKA 1 — godz. _____ (koniec etapu B + 30 min zapasu).** Jeśli test złoty nie przechodzi: przechodzimy na ręczny JSON dla `DemoRoutePlanner`, parser wraca po hackathonie. Zapas jest celowy — bramka wypadająca dokładnie w planowanym końcu B ma zerowy margines.
- **BRAMKA 2 — godz. _____ (etap E + 2 h).** Jeśli OTP nie buduje grafu: ścieżka awaryjna z 6.2.
- **STOP nowych linii** razem ze stop-feature time projektu (~11:30–12:00 następnego dnia).

Etapy A–E należą do fazy D (FLOW). Etap F można robić równolegle w fazie B (PULSE) — potrzebuje tylko przystanków i odjazdów, nie kursów. Schemat poziomu 1 (`transit_line_direction`, `transit_stop`, `transit_departure`, `transit_calendar_day`, `transit_source`) można założyć od razu; poziom 2 (`transit_trip`, `transit_stop_time`) po etapie D.

---

## 9. Ryzyka

| Ryzyko | Skutek | Działanie |
|---|---|---|
| Oficjalny feed GTFS istnieje, a my o nim nie wiemy | część pracy z etapów A–E duplikuje istniejące dane | **zaakceptowane** (decyzja zespołu, D0); dane oznaczone w pitchu jako wtórne |
| Błędna wartość w teście złotym | parser "naprawiany", aż zwróci złe dane | A0 z podpisem dwóch osób |
| Przesunięcie kursów przy krótkim takcie | zły rozkład, wszystkie kontrole zielone | dopasowanie monotoniczne + `match_cost_max` |
| Zjazdy do zajezdni w feedzie | pasażer wysadzony w losowym miejscu | filtr `depot_run` przed `stop_times`, `NOT depot_run` w zapytaniach |
| Duplikacja usług dnia roboczego | podwojone odjazdy w OTP | reguła pustych wakacji (5.5) |
| Nieznany nagłówek kolumn (nocki) | ciche gubienie kolumny | twarda walidacja skonsumowania słów (5.2) |
| Zmiana układu PDF | parser gubi dane | kontrole 5.2.5, błąd z raportem, zamrożone `parsed/` w repo |
| Nazwy ≠ OSM | brak współrzędnych | relacje tras → dopasowanie tolerancyjne → `stops_overrides.json` |
| Za ciasny bbox OSM | brak końcówek poza gminą | bbox rozszerzony (5.4.1) |
| Overpass niedostępny | brak współrzędnych | cache w repo, ponawianie, alternatywny serwer |
| Wakacje/święta bez danych | zły rozkład w części dni | `calendar_config.json`, do tego czasu bez `weekday_holiday` |
| Brak warunków korzystania | ryzyko prawne/wizerunkowe | źródło w feedzie i pitchu, dane oznaczone jako wtórne; warunki korzystania do sprawdzenia |
| Rozmiar 353 MB | wolne pobieranie | tylko `--lines`, warunkowy GET |
| Zmiana schematu strony MZK | `fetch` nie znajduje PDF-ów | kontrola liczby linków (5.1.1), błąd zamiast cichego pustego wyniku |
| OTP nie startuje | brak routingu | `DemoRoutePlanner` albo zapytanie SQL (6.2) |

---

## 10. Komendy

```bash
docker compose run --rm mzk python -m pipeline fetch    --lines 7
docker compose run --rm mzk python -m pipeline extract  --lines 7
docker compose run --rm mzk python -m pipeline stops
docker compose run --rm mzk python -m pipeline assemble --lines 7
docker compose run --rm mzk python -m pipeline gtfs
docker compose run --rm mzk python -m pipeline load-db
docker compose run --rm mzk python -m pipeline validate
docker compose run --rm mzk python -m pipeline all      --lines 7
docker compose run --rm mzk python -m pytest data/mzk/tests
```

Zweryfikowane ręcznie (z katalogu z `bundle.pem`):
```bash
curl -sL --cacert bundle.pem -A "FlowBB hackathon research" \
  -o 7-kier.-Wapienica-Dzwonkowa.pdf \
  "https://komunikacja.bielsko-biala.pl/wp-content/uploads/2026/08/7-kier.-Wapienica-Dzwonkowa.pdf"
pdftotext -bbox-layout 7-kier.-Wapienica-Dzwonkowa.pdf l7_bbox.html
```

---

## 11. Zmiany względem v1

| # | Co | Dlaczego |
|---|---|---|
| 1 | Nowy etap **A0** — weryfikacja i podpisanie fixture'a; test złoty warunkowo w DoD | v1 miała niezweryfikowane wartości oczekiwane w DoD → ryzyko "naprawiania" poprawnego parsera |
| 2 | **Dopasowanie monotoniczne (DP)** zamiast greedy first-fit; mediana per odcinek zamiast globalnego `MAX_HOP`; `match_cost_max` | greedy rozjeżdża kursy przy takcie < 25 min, a kontrole v1 tego nie wykrywały |
| 3 | **Reguła pustych wakacji** — `mzk_weekday_holiday` nie trafia do feedu przy pustej konfiguracji | inaczej dwa komplety identycznych kursów, niewidoczne dla walidatora |
| 4 | **Zjazdy do zajezdni (`#`) odfiltrowane** z `stop_times`, `depot_run` w kontrakcie i w zapytaniu PULSE | kurs z `#` urywa się w środku trasy; w teście złotym `08:53#` jest w porannym szczycie |
| 5 | **Twarda walidacja nagłówka kolumn** — nierozpoznane słowo = wyjątek | inaczej nieznany układ (nocki) gubi kolumnę po cichu |
| 6 | **Decyzja o liniach przeniesiona na początek** (D1), rozdzielona na linię testową i linie demo | v1 opierała DoD na linii 7, a w "otwartych decyzjach" kazała jej nie zakładać |
| 7 | `label` i `line` z tekstu kotwicy, nie z nazwy pliku; `direction_id` deterministyczny z posortowanego `KIERUNEK` | nazwy plików są niespójne, a tekst kotwicy ustrukturyzowany |
| 8 | Czas terminusa z **odcinka przeciwnego kierunku**, nie z mediany całej linii; cross-check nazwy | odcinki śródmiejskie i peryferyjne różnią się dwukrotnie |
| 9 | Dopuszczone **0 m** między kolejnymi przystankami przy pętli; `stop_sequence` pozycyjny | pętle mogą powtarzać przystanek w jednym kierunku |
| 10 | **Bbox OSM rozszerzony**; relacje tras sprawdzane przed dopasowaniem po nazwach | sieć wychodzi poza gminę; relacje dają kolejność za darmo |
| 11 | Osobne `stop_id` dla przystanków o podobnych nazwach, ale wspólny punkt przesiadkowy | scalenie po nazwie psuje dane, brak powiązania psuje przesiadki |
| 12 | `gtfs-validator` **przed** OTP; `agency.txt`, `calendar_dates.txt`, `feed_info.txt` na liście | OTP odrzuca wadliwy feed po cichu |
| 13 | Godziny po północy na nockach jako **decyzja w `config.py`**, nie globalna reguła | reguła 0–3 → 24–27 psuje rozkład, gdy cała linia leży w tym zakresie |
| 14 | **Bramki jako godziny zegarowe** z zapasem | bramka bez zapasu i bez godziny nie zadziała |

## 12. Zmiany po v2

| # | Co | Dlaczego |
|---|---|---|
| 1 | **D0 (mail do MZK) wykreślone**; ryzyko "feed istnieje" przeniesione do sekcji 9 jako zaakceptowane | decyzja zespołu |
| 2 | Sekcja 6.1: pełny schemat `transit_*` w trzech poziomach (PULSE / awaryjny routing / audyt) + kolejność importu | odpowiedź na pytanie o tabele do przechowywania danych |
| 3 | Nowe wyjście `calendar_days.json` i tabela `transit_calendar_day`; zapytanie `has_return` korzysta z niej zamiast z jawnego typu dnia | jedno miejsce z logiką typu dnia |
| 4 | `dep_sec` (sekundy) zamiast `interval`; zapytanie awaryjne A→B na `transit_stop_time` | proste porównania po północy; fallback bez OTP |
| 5 | Sekcja 5.1.1: lista PDF-ów jest w statycznym HTML **[Z]**; usunięte odwołanie do Playwrighta i `screenshots.py` | curl pobrał 141 KB z 112 linkami; w repo nie ma tych narzędzi |
| 6 | Trasy kandydatów do linii demo oraz "pętle" na liniach 7 i 16 oznaczone jako **[NIEZWERYFIKOWANE]**; dodany zweryfikowany fakt o powtórzonej nazwie z różnymi tabliczkami | tylko linia 7 była sprawdzona na plikach |
| 7 | `load_db.py` i komenda `load-db` w pipeline'ie; test importu do bazy | schemat wymaga ładowania i weryfikacji |

## 13. Stan PoC (2026-09-19)

### Decyzje zespołu

- **Linia demo:** tylko linia 7 (obie strony). Linie 4, 16 i inne odpadają.
- **Cel:** dowód, że dane da się wczytać (a docelowe ładowanie do aplikacji ustalimy później), a nie pełny routing.
- **Środowisko:** silna konteneryzacja, wszystko w Dockerze.
- **Zakres poza podstawą:** dochodzą linie nocne N1 i N2. Kolumna wakacyjna i wyjątki `N`/`R` nie wchodzą.

### Odstępstwa od planu v2

| Temat | Plan v2 | PoC | Powód |
|---|---|---|---|
| Język | Python (stdlib) | **Node, bez zależności npm** | zgodność z `data/seed/ingest-events.mjs` i zamrożonym stackiem z `AGENTS.md` (sekcja 4) |
| Katalog | `data/mzk/` | **`data/gtfs/mzk/`** | struktura repo z `AGENTS.md` (sekcja 5) |
| Docker | wspólny compose | osobny `compose.yaml` w katalogu modułu | nie dotykam `infra/docker-compose.yml` (własność Integration Leada) |
| Baza w PoC | stała | efemeryczny PostGIS (tmpfs, bez portów, bez hasła) | brak sekretów w repo, brak kolizji z innymi kontenerami |
| Etapy | A–G | fetch, extract, calendar, load-db, checks | bez `assemble`, `stops` (OSM), `gtfs`, OTP |
| Tabele | 7 | 5 (`transit_line_direction`, `transit_stop`, `transit_source`, `transit_departure`, `transit_calendar_day`) | poziom 1 wystarcza do PULSE; `transit_trip` i `transit_stop_time` czekają na `assemble` |
| Lokalizacja SQL | migracja EF Core | zwykły `sql/schema.sql` | migracja to zadanie Data Leada i Kuby (nowe pakiety EF wymagają jego zgody) |

### Wynik [Z]

- 6 PDF-ów (linia 7 x2, N1 x2, N2 x2) → **140 stron → 7337 odjazdów** → PostGIS (126 przystanków, 122 dni kalendarza).
- 15 testów jednostkowych przechodzi. Ponowne załadowanie daje te same liczby wierszy.
- Asercja SQL na wzorcowej stronie (linia 7, Szyndzielnia 13) przechodzi: 17 / 17 / 26 / 18.
- Fixture wpisany ręcznie z obrazów stron 1 i 4, **bez potwierdzenia człowieka** (etap A0 nadal otwarty).

### Nowe ustalenia, które poprawiają założenia planu [Z]

- **Flagi w komórkach są bogatsze niż `#`, `N`, `R`.** W 6 PDF-ach występują też `K` (kurs skrócony do KARBOWA HALA SPORTOWA, 1288 wystąpień), `Ś` i `W` (kursuje w dniu, w którym normalnie nie kursuje) oraz `#` z dopiskiem "po trasie do: CIESZYŃSKA OS. WOJSKA POLSKIEGO". Znaczenie flagi jest opisane w legendzie strony, więc parser zapisuje legendę przy każdym rekordzie. Flaga `K` musi być uwzględniona przy składaniu kursów (kurs kończy się na Karbowej Hali Sportowej).
- **Układ kolumn nie jest stały.** 139 z 140 stron ma cztery kolumny, ale jedna (linia 7, Karbowa Hala Sportowa 03) ma tylko dwie: dzień roboczy i wakacyjny dzień roboczy. Kolumny wykrywane po nagłówku i przypisywane do najbliższej lewej krawędzi komórek działają na obu układach.
- **Linie nocne mają te same nagłówki kolumn, ale tylko godziny 0–3.** To uchyla wątpliwość z sekcji 2 ("N1/N2 mogą mieć inny zestaw typów dnia"). Odjazdy zapisujemy tak jak w PDF (`dep_sec` od 0 do 14399), bez przesuwania na 24–27. Nie wiadomo z PDF-a, czy godzina 0 w kolumnie "Soboty" to noc z piątku na sobotę, czy z soboty na niedzielę. Do rozstrzygnięcia przy `assemble` i w zapytaniach o powrót po północy.
- **Numer linii z żółtego pola strony** (wysoki tekst po lewej) zgadza się z manifestem na wszystkich stronach. To tania kontrola, że plik jest tym, za co go uważamy.
- **`KIERUNEK` bywa zapisany z kropką na końcu** ("Zajezdnia MZK."), więc parser ją obcina.
- **Data obowiązywania różni się między liniami:** linia 7 od 2026-09-01, N1 od 2026-08-03, N2 od 2025-06-28. `valid_from` jest zapisany per kierunek.
- **Strona z listą PDF-ów jest statycznym HTML-em**, a linię i kierunek da się wziąć z HTML (alt obrazka `7o`, tekst kotwicy "Kierunek: ...").
- **Kolejność stron = kolejność trasy** potwierdzona na obu kierunkach linii 7 (25 i 28 stron).

### Otwarte

1. **Współrzędne przystanków** (`match = 'missing'` dla wszystkich 126). Krok OSM z sekcji 5.4 niezrobiony.
2. **Składanie kursów i GTFS** (`assemble`, `gtfs`), w tym semantyka flag `K`, `#`, `Ś`, `W`.
3. **Podpis człowieka pod fixture** (`tests/fixtures/expected.json`, `human_signoff`).
4. **Święto w sobotę = rozkład niedzielny** to założenie. Wigilia (24.12) nie jest w liście świąt; do potwierdzenia, czy MZK traktuje ją inaczej.
5. **Wakacje szkolne** (`calendar_config.json`) puste, więc kolumna `weekday_holiday` jest w bazie, ale nie występuje w kalendarzu.
6. **Zgoda na nową technologię:** `AGENTS.md` wymaga zgody Backend/Core Leada na nowe zależności. PoC nie dodaje pakietów npm ani NuGet, ale używa `poppler-utils` w obrazie i osobnego PostGIS w compose; warto potwierdzić.
7. **Główny `.gitignore` ma nierozwiązane znaczniki konfliktu merge'a** (`<<<<<<< HEAD`, `=======`, `>>>>>>>` w liniach 1, 94, 107, 112, 231, 247). Poza zakresem tego zadania, ale do naprawienia.
