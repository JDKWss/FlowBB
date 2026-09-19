# FlowBB — źródła wydarzeń: co wczytywać

Stan na **2026-09-19**. Zakres: tylko **wydarzenia** w Bielsku-Białej (transport pomijam, jest w `SOURCES.md` i `SOURCES_RANKING.md`). Liczby poniżej pochodzą z **żywych zapytań wykonanych dzisiaj**, chyba że napisano inaczej. Kryteria z `AGENTS.md`: demo ma przejść bez ręcznych poprawek, więc liczy się godzina wydarzenia, wolumen przyszłych terminów, stabilny format i niski koszt integracji.

## Wniosek

1. **P0: Teatr Polski + Cavatina Hall + bb2026 (wybiórczo).** Tylko te trzy dają JSON z datą i godziną.
2. **Wczytujemy raz do `data/seed/events.seed.json` i commitujemy.** Demo nie odpytuje cudzych serwerów.
3. **Jeśli zabraknie różnorodności: Pełna Kultura jako rezerwa (P1).** Nowe ustalenie: strona wydarzenia zawiera link „Dodaj do kalendarza” z datą i godziną w formacie maszynowym oraz adres ulicy. Wcześniej uznawano ją za tylko-tekstową.
4. **Odpadają:** kalendarium UM, RSS (bielsko.info, BCK), Biletyna, reszta portali.

## Ranking

| # | Źródło | Format | Godzina | Przyszłe terminy | Lokalizacja | Decyzja |
|---|---|---|---|---|---|---|
| 1 | Teatr Polski `teatr.bielsko.pl/api/repertoire` | JSON | tak (UTC) | 32 użyteczne sesje, 11 spektakli | nazwa sceny | **P0** |
| 2 | Cavatina Hall `cavatinahall.pl/wp-json/wp/v2/events` | JSON | tak (`acf`) | 196 po odfiltrowaniu tłumaczeń EN (12 w 14 dni, 27 w 30, 84 w 90) | jedna sala | **P0** |
| 3 | bb2026.pl `/wp-json/tribe/events/v1/events` | JSON | dla 4 z 22 | 22, ale użytecznych 1–2 | 5 z 22 ma `venue` | **P0, wybiórczo** |
| 4 | Pełna Kultura `pelnakultura.info` | HTML | tak (link do kalendarza) | ok. 103 linków na stronie głównej | adres ulicy (mapa embed) | **P1, rezerwa** |
| 5 | Kalendarium UM `bielsko-biala.pl/kalendarium` | HTML | nie znaleziono | nie znaleziono | — | pomijamy |
| — | bielsko.info RSS, BCK RSS | RSS | brak dat wydarzeń | — | — | odpada |
| — | Biletyna | HTML | — | — | — | odpada (ryzyko prawne) |
| — | bielskonews, 43300, portal miasta, Bielski Rynek, imprezybielsko | różne | — | — | — | odpadają |

## Co dokładnie wczytać

### 1. Teatr Polski (start od niego)

- **Dlaczego #1:** jedyne źródło z `capacity` i `freeSeats`, czyli realnym sygnałem popytu. Dziś wśród 32 użytecznych sesji 7 jest wyprzedanych, a kolejnych 7 ma mniej niż 15% wolnych miejsc (np. „Seksmisja” 27.09: 402 miejsca, 0 wolnych; 29.09: 1 wolne; 30.09: 2 wolne). To gotowa historia „wyprzedane → ludzie potrzebują transportu”.
- **Filtr:** `date >= teraz` **i** `status == "open"` **i** `capacity != null`. Z 44 pozycji zostaje 32 sesji. Pozycje `blocked` (3) i `null` (1) odrzucamy. Przykład `null`: „Tina” z 19.09, pokaz zamknięty bez pojemności.
- **Czas:** `date` jest w UTC (`2026-09-27T17:00:00.000Z` to 19:00 w Warszawie). Konwertuj strefą `Europe/Warsaw`, nie stałym przesunięciem, bo 25.10 zmienia się czas na zimowy.
- **Sceny:** Duża Scena (20), Mała Scena (9), Zamek Książąt Sułkowskich – Salon Muzyczny (7). Zamek ma inny adres niż gmach teatru (ul. 1 Maja 1). Czy Mała Scena jest w tym samym gmachu, nie sprawdziłem.
- **Uwaga:** endpoint jest wewnętrzny dla frontu strony i może zniknąć. Stąd snapshot, nie zależność w runtime.
- **Pola do zapisu:** `repertoireEventId`, `title`, `date`, `duration`, `capacity`, `freeSeats`, `stage.name`, `showEvent.slug`, `repertoireCategories`.

### 2. Cavatina Hall

- **Dlaczego:** największy wolumen i jedno miejsce do geokodowania. Po odfiltrowaniu duplikatów zostaje 196 przyszłych wpisów (115 unikalnych tytułów, bo część wydarzeń ma wiele terminów), co daje bufor na wybór terminów pod demo.
- **Pułapka: duplikaty językowe.** Ok. połowa wpisów to angielskie tłumaczenia tych samych wydarzeń (adres z `/en/events/`, np. „Zeppelinians” i „LED ZEPPELIN SHOW…” z tą samą godziną). Odrzucaj wpisy, których `link` zawiera `/en/`. Dziś: 172 z 368 przyszłych. Wcześniejsza liczba 370 była zawyżona.
- **Zapytanie:** `?per_page=100&page=N&_fields=id,title,link,acf,event_category` (13 stron). Bez `_fields` jedna strona waży ok. 1,1 MB, z `_fields` całość mieści się w ok. 325 KB.
- **Termin:** wyłącznie `acf.event_datetime`. Pole `date` to data publikacji wpisu. Format bywa `2026-11-05 19:00` albo `2026-12-07 17:00:00`, więc parser musi przyjąć oba.
- **Braki:** 5 wpisów bez daty (dziś 1231 z 1236 ma `event_datetime`), część bez kategorii. Cena i współrzędne nie występują w polach, które pobrałem.

### 3. bb2026.pl (2–3 wydarzenia dla narracji pitchu)

- **Wartość:** kontekst „Polska Stolica Kultury 2026” pasuje do tematu „Bielsko-Biała 2030”. Wolumen konkretnych wydarzeń jest za to mały.
- **Stan dziś (22 wydarzenia od 19.09):**
  - 8 jest jednodniowych, ale tylko **4 mają godzinę** inną niż 00:00–23:59.
  - Miejsce ma 5 wydarzeń, ale tylko **jedno łączy godzinę i miejsce**: „Biblioteczna Noc Fantazji i Wyobraźni”, 3.10 od 17:00, Książnica Beskidzka.
  - Pozostałe z `venue`: Bielsko-Biala Gaming Festival 4.0 (2–4.10, Hala pod Dębowcem), XV Giełda Projektów (14–16.10, BCK), „Archiwum wahań” (14–25.10, Galeria Bielska BWA), Święto Drzewa (15.10, Tajemniczy Ogród).
  - `cost` jest puste we wszystkich 22.
- **Rekomendacja:** Biblioteczna Noc jako wydarzenie z godziną. Gaming Festival jako typ „festiwal” z domyślnym dniem (np. sobota 3.10). Reszta odpada, bo „idę o 19:00” nie ma sensu dla zakresów całego października.
- **Zapytanie:** `/wp-json/tribe/events/v1/events?per_page=50&start_date=<dziś>`. `venues` (31 miejsc) daje adresy, ale nie współrzędne.

### 4. Pełna Kultura (rezerwa, P1)

- **Nowe ustalenie:** strona `/item/{slug}.html` ma link `google.com/calendar/render?...dates=20260919T170000/...&text=...&location=...` (sprawdzone na jednej stronie: „Swing na lotnisku”). Daje godzinę bez parsowania polskich nazw miesięcy. Osadzona mapa zawiera adres ulicy (`ul. Cieszyńska 321`), czyli materiał do geokodowania.
- **Ograniczenia:** sprawdziłem jedną stronę wydarzenia. W tym linku koniec równa się początkowi. Pole `location` zawierało nazwę organizatora, a nie lokalu. Wymaga scrapowania HTML (lista: ok. 103 linków `/item/*.html` na stronie głównej), więc koszt jest średni i to zadanie dopiero po zamknięciu P0.
- **Kiedy użyć:** jeśli 3 źródła JSON nie dadzą wydarzeń różnych typów (plener, koncert klubowy, wydarzenie rodzinne). Teatr i Cavatina to głównie kultura wysoka.

## Dlaczego pozostałe odpadają

- **Kalendarium UM:** strona odpowiada `200`, ale w statycznym HTML nie ma listy wydarzeń ani JSON-LD. Nie sprawdzałem, czy ładuje je skrypt.
- **bielsko.info i BCK RSS:** wpisy CMS bez daty wydarzenia (`SOURCES.md`, sekcje bielsko.info i BCK).
- **Biletyna:** `robots.txt` z `ai-train=no` i blokadą widoków wydarzeń. Ryzyko przed miastem przy pitchu.

## Zestaw do demo (8–12 wydarzeń)

| Źródło | Ile | Co wybrać |
|---|---|---|
| Teatr Polski | 4–5 | „Seksmisja” 27.09 (wyprzedana), „Tina” 20.09 (wyprzedana), sesja z dużą pulą wolnych miejsc (np. „Kabaret” 2.10, 260 wolnych) dla kontrastu |
| Cavatina Hall | 4–5 | terminy z najbliższych 14 dni (12 do wyboru po odfiltrowaniu `/en/`), różne tytuły |
| bb2026 | 1–2 | Biblioteczna Noc Fantazji (3.10), Gaming Festival jako „festiwal” |

Data prezentacji nie jest w repo, więc okno „najbliższe dni” trzeba ustalić na dzień demo. Snapshot wykonaj krótko przed feature freeze, żeby wybrane wydarzenia nie były już w przeszłości.

## Mapowanie na `Event` (propozycja, kontrakt zatwierdza Kuba)

| Pole | Teatr Polski | Cavatina | bb2026 | Pełna Kultura |
|---|---|---|---|---|
| Source / ExternalId | `repertoireEventId` | `id` | `id` | slug |
| Name | `title` | `title.rendered` | `title` | tytuł strony |
| StartAt | `date` (UTC → Warszawa) | `acf.event_datetime` | `start_date` | `dates=` z linku kalendarza |
| EndAt | `date` + `duration` | brak | `end_date` | brak |
| VenueName | `stage.name` | stała: Cavatina Hall | `venue` | adres z mapy |
| Location (lat/lon) | słownik lokali | słownik lokali | słownik lokali | słownik / geokodowanie |
| Capacity / FreeSeats | **tak** | brak | brak | brak |

Żadne źródło nie daje współrzędnych. Potrzebny jest ręczny `data/seed/venues.json` (ok. 15–20 lokali), zaczynając od lokali wybranych do demo (Teatr, Zamek, Cavatina, Książnica Beskidzka, Hala pod Dębowcem).

## Zasady wczytywania

1. Jeden skrypt pobiera i normalizuje dane, wynik `events.seed.json` jest commitowany. Skrypt należy do obszaru Data/Neo4j.
2. Tytuły i terminy są realne, popyt (`AttendanceIntent`) syntetyczny. UI oznacza to jako `DEMO DATA / SYMULACJA`.
3. `capacity` i `freeSeats` z Teatru to sygnał realny. Nie łącz go z syntetycznym licznikiem w jednym polu.
4. Wszystkie czasy zapisuj jako ISO 8601 ze strefą `Europe/Warsaw`.

## Czego nie zweryfikowałem

- Regulaminów Teatru, Cavatiny i bb2026 (dane są publiczne, ale w pitchu zaznacz źródło i że to snapshot).
- Pełnej Kultury poza jedną stroną wydarzenia oraz tego, czy lista na stronie głównej obejmuje przyszłe terminy.
- Czy kalendarium UM ładuje wydarzenia skryptem.
- Czy Cavatina ma cenę w polach, których nie pobrałem (`_fields` ograniczyłem do `id,title,link,acf,event_category`).
- Zgodności skryptu ingestu z sekcją 13 `AGENTS.md`. Zapytaj organizatora lub mentora, albo napisz skrypt dopiero w oknie hackathonu.

## Różnice względem `SOURCES_RANKING.md`

- Cavatina: 196 przyszłych wpisów zamiast 370, bo ranking (i moja pierwsza wersja tego pliku) nie odjął angielskich tłumaczeń.
- Teatr: dziś 36 sesji od 19.09 (nie 44), z czego **32 użyteczne** po filtrze `open` + `capacity`.
- bb2026: godzinę ma 4 z 22, a godzinę z miejscem tylko 1 wydarzenie, więc do demo realnie 1–2 (ranking zakładał 2–3).
- Pełna Kultura: awans z „pomijamy” na rezerwę P1 ze względu na link do kalendarza.
