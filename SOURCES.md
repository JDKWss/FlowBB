# FlowBB — źródła danych o wydarzeniach i transporcie (Bielsko-Biała)

Stan na **2026-09-19**. Zakres: **tylko miasto Bielsko-Biała**. Każdy wpis oznaczony jako **[potwierdzone]** został sprawdzony zapytaniem HTTP z tego repo. Wpisy **[niesprawdzone]** pochodzą z wyszukiwarki lub z domysłu i wymagają własnego `curl`.

Nagłówek User-Agent użyty do testów: `Mozilla/5.0 (FlowBB hackathon research)`. Do żadnego źródła nie stosowano obchodzenia zabezpieczeń.

## Werdykt (co bierzemy do MVP)

| # | Źródło | Format | Po co | Trudność |
|---|---|---|---|---|
| 1 | Teatr Polski `/api/repertoire` | JSON | Spektakle z pojemnością sali i wolnymi miejscami | niska |
| 2 | Cavatina Hall `/wp-json/wp/v2/events` | JSON (WordPress + ACF) | Koncerty w Cavatinie | niska |
| 3 | bb2026.pl (Polska Stolica Kultury) | JSON (The Events Calendar) + iCal | Program festiwali PSK 2026 | niska |
| 4 | Pełna Kultura `pelnakultura.info` | HTML (TYPO3) | Kanoniczna baza miasta, ale tylko HTML | średnia |
| 5 | Rozkłady MZK (PDF) | PDF | Dojazd autobusem (brak GTFS) | średnia |
| 6 | GTFS Kolei Śląskich | ZIP | Dojazd koleją (6 stacji w mieście) | niska |

Pozostałe strony to uzupełnienia albo odpadają (sekcje niżej).

---

## 1. Teatr Polski — `https://teatr.bielsko.pl` [potwierdzone]

- Aplikacja Next.js (App Router). **Nie ma** `__NEXT_DATA__` ani `_next/data`, więc podejście z poprzedniej analizy odpada.
- Repertuar ładuje się w przeglądarce z **`GET https://teatr.bielsko.pl/api/repertoire`** (endpoint znaleziony w bundlu JS strony `/repertuar`; zwraca `200 application/json`).
- Odpowiedź: `{ "events": [...] }`, w próbie 44 pozycje, daty od 2026-09-05 do 2026-10-31.
- Pola pozycji: `repertoireEventId`, `title`, `date`, `duration` (minuty), `capacity`, `freeSeats`, `status`, `paymentUrl`, `saleStartsAt`, `saleEndsAt`, `stage.name` (np. "Mała Scena"), `showEvent.slug`, `ageCategories`, `repertoireCategories`.
- Uwaga: `date` jest w **UTC** (`2026-09-05T14:00:00.000Z` to 16:00 w Warszawie). Trzeba konwertować do `Europe/Warsaw`.
- Bonus dla PULSE: `capacity` i `freeSeats` to gotowy sygnał popytu (wyprzedane vs wolne).
- Brakuje: ceny, współrzędnych. Adres teatru: ul. 1 Maja 1, 43-300 Bielsko-Biała (ze stopki strony).
- `robots.txt` nie istnieje (zwraca stronę HTML aplikacji). Endpoint jest publiczny i używany przez sam front.
- Adres `teatr-polski.bielsko.pl` z poprzednich notatek **nie istnieje** (błąd DNS). Właściwa domena to `teatr.bielsko.pl`.

## 2. Cavatina Hall — `https://cavatinahall.pl` [potwierdzone]

- WordPress z otwartym REST: **`GET /wp-json/wp/v2/events?per_page=100&page=N`** (`200 application/json`).
- Nagłówek `X-WP-Total: 1236` wpisów (w tym prawdopodobnie przeszłe; nie sprawdzałem, ile jest przyszłych).
- Pola: `id`, `slug`, `link`, `title.rendered`, `content.rendered`, `event_category`, `acf`, `yoast_head_json`, `featured_media`.
- **Pułapka:** `date` to data publikacji wpisu, a nie wydarzenia. Termin jest w `acf`: `event_date` (`20270514`), `event_time` (`20:30:00`), `event_datetime` (`2027-05-14 20:30:00`).
- Brakuje: ceny i współrzędnych w polach, które widziałem. Adres sali stały (geokodować raz).

## 3. bb2026.pl — Bielsko-Biała Polska Stolica Kultury 2026 [potwierdzone]

- WordPress + wtyczka **The Events Calendar** (typy `tribe_events`, `tribe_venue`, `tribe_organizer`).
- REST: `GET /wp-json/tribe/events/v1/events?per_page=50&start_date=2026-09-19` (`total: 22`, `total_pages: 11` przy `per_page=2`).
- Alternatywnie `GET /wp-json/wp/v2/tribe_events` (`X-WP-Total: 26`) i lista miejsc `GET /wp-json/tribe/events/v1/venues` (31 miejsc, ma adresy, np. "Hala pod Dębowcem, Karbowa 26").
- iCal: `GET https://bb2026.pl/wydarzenia/?ical=1` (`text/calendar`, 26 `VEVENT`, `REFRESH-INTERVAL PT1H`). Żaden `VEVENT` nie ma `GEO`, tylko 7 ma `LOCATION`.
- Pola eventu w JSON: `title`, `description`, `start_date`, `end_date`, `timezone` (`Europe/Warsaw`), `cost`, `url`, `categories`, `tags`, `venue`, `organizer`, `image`, `all_day`.
- **Pułapka:** to program festiwalowy, a nie pojedyncze koncerty. Przykłady z próby: "Soundesign" 26–28 września, "Akademia Street Artu" 1 maja – 30 września. `venue` bywa pusta (`[]`), `cost` pusty.
- `robots.txt` permisywny (blokuje tylko `/wp-admin/`).
- Kalendarz ma też stronę `https://bb2026.pl/kalendarium/` (z wyników wyszukiwania, niesprawdzona).

## 4. Pełna Kultura — `https://www.pelnakultura.info` [potwierdzone]

- TYPO3, renderowanie po stronie serwera, `200` (z `pelnakultura.info` jest przekierowanie na `www.`).
- Strony wydarzeń: `/item/{slug}.html`, np. `/item/to-ja-grzanka.html`, `/item/bielsko-biala-street-photo-festival.html`.
- Sekcje serwisu: Muzyka, Galerie, Teatr, Film, Książki, Muzea, Rozmaitości.
- Treść strony wydarzenia: tytuł, zakres dat **jako tekst** ("17 września – 30 października"), wstęp ("wg cennika"), opis. Trzeba parsować polskie nazwy miesięcy.
- JSON-LD to tylko `{"@type":"ItemPage"}`, czyli bezużyteczne. Brak RSS, brak `robots.txt`, brak `sitemap.xml` (wszystkie `404`).
- Domena `pelnakultura.bielsko.pl` **nie istnieje** (błąd DNS), a poprzedni raport uznał przez to źródło za martwe.
- Nie sprawdzałem, czy kalendarium Urzędu Miejskiego linkuje do tej bazy.

## 5. Urząd Miejski — kalendarium `https://bielsko-biala.pl/kalendarium` [częściowo]

- `200` po przekierowaniu z `www.bielsko-biala.pl`. Drupal (wg poprzedniej analizy, niesprawdzone).
- Nie sprawdzałem struktury ani formatu danych.

---

## Uzupełnienia i słabsze źródła

### bielsko.info — `https://www.bielsko.info` [potwierdzone]
- `https://www.bielsko.info/rss.xml` to poprawny RSS 2.0 (`ttl` 120), 50 pozycji, ale to **wiadomości** (linki `/wiadomosci/...`), a nie kalendarz wydarzeń. Przykładowy tytuł: "Bielsko-Biała potrzebuje miejsca na duże koncerty".
- Nadaje się co najwyżej do sygnału "co się dzieje w mieście", nie do listy wydarzeń.
- Sekcja "Co, gdzie, kiedy?" istnieje (`/cgk`, `200`), ale to HTML bez feedu.
- `robots.txt` permisywny (blokuje tylko `/a.php`, `*/cache/`, `/skrypty/`).
- Poprzednia analiza (wersja "RSS z wydarzeniami") była zbyt optymistyczna.

### BCK — `https://bck.bielsko.pl` [potwierdzone]
- Drupal, `200` (z tego środowiska; wcześniejsze 403 to najpewniej blokada po stronie innego proxy).
- `https://bck.bielsko.pl/rss.xml` to RSS z **10 pozycjami**, ale to wpisy CMS: `pubDate` to data publikacji, a `description` to zaśmiecony HTML z autorem i datą wpisu. **Daty wydarzenia w feedzie nie ma**, więc do lokalnego MVP trzeba scrapować strony wydarzeń.
- Uwaga: część tytułów zawiera godzinę ("spektakl O MAŁO CO g. 18.30"), więc da się ją wyciągnąć regexem, ale to kruche.

### Biletyna — `https://biletyna.pl/Bielsko-Biala` [częściowo]
- Strona główna `200`. Strona miasta pochodzi z wyników wyszukiwarki, nie fetchowałem jej.
- Brak `sitemap.xml` i `wp-json`. `robots.txt` zawiera `Content-Signal: ai-train=no` i blokuje m.in. `/event/view/`, `/event/index/`, `/ajax/`.
- To pośrednik biletowy z regulaminem, więc scraping to ryzyko dla pitchu przed miastem. **Nie polecam** jako źródła.

### Portal miasta — `https://bielsko.biala.pl` [częściowo]
- `200`. `/rozrywka/zapowiedzi` ("Wydarzenia, kultura i rozrywka w Bielsku-Białej") nie ma JSON-LD/schema.org Event. `wp-json` zwraca `403`, brak feedu, `robots.txt` pusty, mały `sitemap.xml`.
- Uzupełnienie HTML, niski priorytet.

### Bielski Rynek — `https://bielskirynek.pl` [częściowo]
- `200`. Ma stronę programu PSK 2026 (z wyszukiwarki). `/feed/` i `sitemap.xml` zwracają tylko 333 bajty (najpewniej puste). Brak `wp-json`.

### Bielskonews — `https://bielskonews.pl` [częściowo]
- `200`, portal informacyjny. Brak feedu i `wp-json`. Nie nadaje się jako źródło wydarzeń.

### 43300.pl [częściowo]
- `200`. `robots.txt` blokuje tylko `/manage/`. Zawartości nie sprawdzałem.

### Nie udało się otworzyć [niesprawdzone]
- `imprezybielsko.pl` — `curl` kończy się kodem 60 (weryfikacja certyfikatu TLS), nie dało się nawiązać połączenia.
- Nie wyłączałem weryfikacji certyfikatów. Sprawdźcie z innej sieci albo przeglądarką.
- `komunikacja.bielsko-biala.pl` (MZK) miało ten sam problem, ale udało się go obejść poprawnie (patrz sekcja MZK).

---

## Transport — kolej w Bielsku-Białej

### Koleje Śląskie, statyczny GTFS [potwierdzone]
- **`https://koleje-ks.pl/gtfs/2025-2026.zip`** — `200`, 2,4 MB, zmodyfikowany 2026-09-16. Adres wzięty ze źródeł feedu w Transitland (`f-u2v-koleje~slaskie`, tam też starsze roczniki `2023-2024`, `2024-2025`).
- Zawartość: 239 przystanków (cała sieć), `stop_times`, `shapes`, `calendar_dates`. Kalendarz ważny **od 2025-12-14 do 2026-12-13**. Agencja: Koleje Śląskie (`Europe/Warsaw`).
- **Stacje w Bielsku-Białej (ze współrzędnymi):**

  | stop_id | Nazwa | lat | lon |
  |---|---|---|---|
  | 76109 | Bielsko-Biała Gł. | 49.83071 | 19.04558 |
  | 76133 | Bielsko-Biała Północ | 49.84097 | 19.04173 |
  | 76141 | Bielsko-Biała Komorowice | 49.86127 | 19.03579 |
  | 76349 | Bielsko-Biała Leszczyny | 49.79786 | 19.05980 |
  | 76356 | Bielsko-Biała Lipnik | 49.81414 | 19.04965 |
  | 76364 | Bielsko-Biała Mikuszowice | 49.78348 | 19.07371 |

- Feed nie ma realtime (opóźnień). Do dojazdu na wydarzenie wystarczy statyczny rozkład.

## Transport — autobusy MZK Bielsko-Biała

### Dane na żywo i GTFS
- Fakt: od maja 2025 rozkłady MZK są w Google Maps z **GTFS-Realtime**. Dane real-time są też w aplikacji **itsBB** (pokazuje pojazdy na mapie i faktyczne odjazdy) i na stronie `https://rozklady.bielsko.pl/`. Źródła: [bielsko.info](https://www.bielsko.info/wiadomosci/43608-autobusy-mzk-bielsko-biala-z-rozkladami-jazdy-w-czasie-rzeczywistym-w-google-maps-bielsko-biala), [TransInfo](https://transinfo.pl/infotrans/mzk-bielsko-biala-z-dynamicznymi-rozkladami-jazdy-w-google-maps/), [komunikacja.bielsko-biala.pl](https://komunikacja.bielsko-biala.pl/index.php/2025/06/09/dynamiczna-informacja-pasazerska-w-mapach-google/).
- **Publicznego adresu feedu MZK (GTFS ani GTFS-RT) nie znalazłem**: nie ma go w katalogu Otwartych Danych Transportowych (`odt.org.pl/data`), a w wynikach wyszukiwania po Transitland/Mobility Database też nie. Zgadywane adresy (`rozklady.bielsko.pl/gtfs.zip`, `/gtfs`, `/api`) zwracają `404`.
- `https://rozklady.bielsko.pl/` (`200`) to aplikacja webowa "OnTime" (Cordova, OpenLayers) ładowana skryptami JS. Jej wewnętrznego API **nie analizowałem**. To backend miejskiej aplikacji, więc nie polecam go jako źródła do pokazania miastu.
- Skoro MZK przekazuje feed Google, istnieje jego oficjalny adres. Najlepszy ruch: **napisać do MZK / Urzędu Miejskiego z prośbą o feed GTFS i GTFS-RT do celów hackathonu** (a w pitchu ująć to jako element wdrożenia).
- Transitland: REST API zwraca `401` bez klucza (darmowy klucz po rejestracji). Operatora MZK tam nie potwierdziłem.
- `dane.gov.pl`: wystawka "BusLive - Otwarte Dane" odpowiada `200`, treści nie odczytałem (**niesprawdzone**).

### Rozkłady tabliczkowe (PDF) — skąd pobierać [potwierdzone]
Skoro nie ma GTFS dla autobusów, jedyną otwartą, maszynowo czytelną formą są **rozkłady w PDF**. PDF-y pobrałem i przeczytałem `pdftotext -layout` (tekst jest osadzony, nie skan, więc OCR nie jest potrzebny).

- **Strona z listą:** `https://komunikacja.bielsko-biala.pl/index.php/rozklad-jazdy-do-wydruku/` ("Przystankowy rozkład do wydruku PDF").
- Na stronie jest **112 PDF-ów**, po jednym na linię i kierunek (`...-kier.-<cel>.pdf`) plus `...-pozostale.pdf` (przystanki na trasie wyjazdu/zjazdu z zajezdni). Linie: 1, 2, 3, 4, 6, 7, 8, 10–20, 22–24, 26–29, 31–36, 50, 53, 56, 57, 13W, N1, N2, P2.
- Adres pliku: `https://komunikacja.bielsko-biala.pl/wp-content/uploads/<rok>/<miesiąc>/<nazwa>.pdf`. Przykład: `.../2026/08/7-kier.-Wapienica-Dzwonkowa.pdf`. Miesiąc w ścieżce jest **różny dla każdej linii** (data wgrania), więc listę trzeba za każdym razem brać ze strony.
- Zawartość (linia 7, kier. Wapienica Dzwonkowa): 7,5 MB, **28 stron, jedna strona = jeden przystanek**. Nagłówek: `PRZYSTANEK: Szyndzielnia 13`, `KIERUNEK: ...`, `Obowiązuje od: 01.09.2026`, trasa przejazdu, telefon dyspozytora MZK. Tabela: wiersz = godzina, kolumny = `Dni robocze` / `Soboty` / `Niedziele i Święta`, w komórkach minuty (np. `01, 53#`), z legendą oznaczeń (`R`, `#`).
- Inne strony MZK: przewodnik po systemie OnTime (`.../wp-content/uploads/2019/11/Przewodnik-po-systemie-OnTime.pdf`), "Pogotowie rozkładowe" (`.../index.php/brak-rozkladu-jazdy/`), dynamiczny rozkład `https://rozklady.bielsko.pl/`.
- **Uwaga TLS:** serwer wysyła niekompletny łańcuch certyfikatów (brak pośredniego Sectigo DV R36), więc `curl` i `WebFetch` zgłaszają błąd. Poprawka bez wyłączania weryfikacji: pobrać certyfikat pośredni z `http://crt.sectigo.com/SectigoPublicServerAuthenticationCADVR36.crt`, zamienić na PEM i dodać do `--cacert`. W przeglądarkach strona działa.

### Jak to wykorzystać
- Parsować PDF-y (`pdftotext -layout`, lub `pdfplumber`) do własnego formatu `stop_times`. Tabela godzina × dzień na stronę-przystanek jest regularna, więc parser jest wykonalny.
- Do demo wystarczy ręcznie przepisać **jedną linię** (np. 7) do `DemoRoutePlanner`. Pełny parser to zadanie po hackathonie.
- Rozkład z PDF-a jest statyczny (bez opóźnień). Dane na żywo dalej wymagają feedu od MZK.
- Nie sprawdzałem warunków korzystania z tych PDF-ów. Do pitchu warto zaznaczyć, że dane pochodzą z publicznych rozkładów MZK.

## Tabela: kto ma jakie dane o transporcie w mieście

| Przewoźnik | Statyczny GTFS | Realtime | Dostęp |
|---|---|---|---|
| Koleje Śląskie (6 stacji w mieście) | tak, `koleje-ks.pl/gtfs/2025-2026.zip` | brak w tym feedzie | **otwarte** |
| MZK Bielsko-Biała | nieznaleziony (są PDF-y) | GTFS-RT istnieje (Google, itsBB) | brak publicznego adresu |

## Rekomendacja dla routingu

1. **Kolej:** Koleje Śląskie GTFS (`koleje-ks.pl`) do OTP.
2. **Autobusy MZK:** do czasu zdobycia feedu użyć `DemoRoutePlanner` z ręcznie spisanymi liniami (np. linia 7 z planu demo). Nie budować routingu na scrapowaniu OnTime/itsBB.
3. **OSM** (Geofabrik, województwo śląskie) do dojść pieszych.
4. Interfejs `IRoutePlanner` z planu zostaje bez zmian. To dobra izolacja przed brakiem danych autobusowych.
5. **Pismo do MZK / Urzędu Miejskiego** o feed GTFS i GTFS-RT można pokazać jako element wdrożenia po hackathonie.

---

## Mapowanie na encję `Event`

| Pole `Event` | Teatr Polski | Cavatina | bb2026 | Pełna Kultura |
|---|---|---|---|---|
| Name | `title` | `title.rendered` | `title` | tytuł strony |
| StartAt | `date` (UTC, konwersja) | `acf.event_datetime` | `start_date` | parsowanie tekstu |
| EndAt | `date` + `duration` | brak | `end_date` (całe zakresy) | parsowanie tekstu |
| VenueName | `stage.name` (+ stała) | stała: Cavatina Hall | `venue` (bywa puste) | z opisu |
| Location (lat/lon) | **geokodowanie / słownik** | **geokodowanie / słownik** | **geokodowanie / słownik** | **geokodowanie / słownik** |
| Category | `repertoireCategories` | `event_category` | `categories` | sekcja serwisu |
| Price | brak | brak | `cost` (puste w próbie) | tekst "wg cennika" |
| Capacity | **`capacity`, `freeSeats`** | brak | brak | brak |
| Source / ExternalId | `repertoireEventId` | `id` | `id` | slug z `/item/{slug}.html` |

**Współrzędnych nie daje żadne źródło.** Najprościej zrobić ręczny słownik ~15–20 lokali (`data/seed/venues.json`: nazwa, adres, lat, lon) i dopasowywać po nazwie. Nominatim tylko jako uzupełnienie (limit 1 zapytanie/s, wymaga własnego User-Agent).

## Ostrzeżenia

1. `date` w Cavatinie to data publikacji, użyjcie `acf.event_datetime`.
2. `date` w Teatrze Polskim jest w UTC.
3. bb2026 to program festiwalowy z wielodniowymi zakresami, a nie pojedyncze koncerty. W PULSE rozdzielcie "festiwal" od "wydarzenia z godziną".
4. RSS bielsko.info i BCK nie zawierają dat wydarzeń. Nie budujcie na nich ingestu.
5. Nie ma publicznego pliku GTFS dla autobusów MZK.
6. Wszystkie liczby (26 wydarzeń, 44 pozycje, 1236 wpisów, 112 PDF-ów) to stan z chwili sprawdzenia i się zmienią.

## Komendy do własnej weryfikacji

```bash
UA="Mozilla/5.0 (FlowBB)"
curl -s -A "$UA" "https://teatr.bielsko.pl/api/repertoire" | head -c 600
curl -s -A "$UA" "https://cavatinahall.pl/wp-json/wp/v2/events?per_page=2" | head -c 600
curl -s -A "$UA" "https://bb2026.pl/wp-json/tribe/events/v1/events?per_page=5&start_date=2026-09-19" | head -c 600
curl -s -A "$UA" "https://bb2026.pl/wydarzenia/?ical=1" | head -20
curl -sIL -A "$UA" "https://www.pelnakultura.info/" | head -3
curl -sIL -A "$UA" "https://koleje-ks.pl/gtfs/2025-2026.zip" | head -3
```
