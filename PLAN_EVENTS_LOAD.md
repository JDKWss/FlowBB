# Plan: wczytanie wydarzeń do FlowBB

Stan na 2026-09-19. Plan opisuje drogę od trzech źródeł JSON (`SOURCES_EVENTS.md`) do endpointów `GET /api/events` i `GET /api/events/{eventId}`, czyli punktu 1 krytycznego scenariusza demo z `AGENTS.md`. Nic z tego planu poza `data/seed/ingest-events.mjs` nie jest jeszcze zaimplementowane. Plan jest dopasowany do kontraktu z `contracts/openapi.yaml` (`EventSummary`, `EventDetails`) i do szkieletu backendu z gałęzi `develop` (`backend/src/...`, bez EF Core i bez encji).

## Decyzje (potwierdzone)

| Temat | Decyzja |
|---|---|
| Kto wczytuje seed do bazy | Seeder w API przy starcie (EF Core, `FlowBB.Infrastructure`), idempotentny upsert. Właściciel: Kuba. |
| Jak wybieramy wydarzenia do demo | Ręczna lista `data/seed/pinned.json` z `externalId`, wybrana z pełnego snapshotu. |
| Słownik lokali | Tylko 4–5 lokali dla wybranych wydarzeń. Wydarzenia z innych lokali odpadają z seeda. |
| Regulamin (sekcja 13) | Hackathon trwa, okno kodowania jest otwarte, więc nic nie blokuje etapów z kodem. |
| Lokalizacja w bazie | `Point` na tabeli `venues`, nie na `events`. DTO wydarzenia zwraca `location: { latitude, longitude }` zgodnie z kontraktem. |
| Sceny Teatru Polskiego | Jeden lokal „Teatr Polski” z aliasami „Duża Scena” i „Mała Scena”. To założenie: nie znalazłem na stronach Teatru potwierdzenia, że obie sceny są w budynku przy ul. 1 Maja 1. |
| Data prezentacji | 20.09.2026 (jutro). Godzina nie jest podana. |
| Lokale w słowniku | 5: Teatr Polski, Zamek Książąt Sułkowskich, Cavatina Hall, Książnica Beskidzka, Hala pod Dębowcem. |
| Pole `source` z kontraktu (`Demo`, `City`, `External`) | Teatr Polski i Cavatina Hall → `External`, bb2026 → `City`. `Demo` zostaje dla danych syntetycznych. To domyślne mapowanie, do potwierdzenia przez Kubę. |
| Źródło wydarzenia w bazie | Własna kolumna `provider` (`teatr-polski`, `cavatina-hall`, `bb2026`) jako klucz idempotencji. Nie jest zwracana w API. |

## Kryterium akceptacji

Czysta baza, `docker compose up`, potem `GET /api/events` zwraca zestaw demo z godzinami w strefie Warszawy. Ponowne uruchomienie API nie zmienia liczby wierszy ani ID wydarzeń.

## Dwa kroki, celowo rozdzielone

| Krok | Sieć | Kiedy | Wynik |
|---|---|---|---|
| A. Snapshot | tak | ręcznie, krótko przed feature freeze | `data/seed/events.snapshot.json` (wszystko) i `data/seed/events.seed.json` (tylko `pinned`, z listą `venues` i `venueId` przy każdym wydarzeniu) |
| B. Load | nie | przy każdym starcie API | wiersze `venues` i `events` w PostGIS |

Demo uruchamia tylko krok B. Awaria strony Teatru lub Cavatiny nie może zepsuć prezentacji.

## Etapy

| # | Etap | Pliki | Właściciel | Zależy od |
|---|---|---|---|---|
| 1 | Kontrakt `EventSummary`/`EventDetails` już jest. Brakuje `contracts/fixtures/events.json` z 3–5 wydarzeniami (odblokowuje frontend i walking skeleton do 2 h) | `contracts/fixtures/events.json` | Kuba | — |
| 2 | Słownik lokali: 4–5 wpisów (`id`, `name`, `aliases`, `address`, `lat`, `lon`); ten sam plik zasila tabelę `venues` | `data/seed/venues.json` | Data Lead | — |
| 3 | Rozszerzenie skryptu snapshotu: opcje `--pinned` i `--venues` (przypisanie `venueId` po nazwie lub aliasie), walidacja, dedupe po `(tytuł, startAt)`, klasyfikacja `session`/`festival`, pola wymagane przez kontrakt (`category`, `description` do 1000 znaków, `source`), raport odrzuconych | `data/seed/ingest-events.mjs` | Data Lead | 2 |
| 4 | Wybór `pinned.json` po przejrzeniu `events.snapshot.json` | `data/seed/pinned.json` | zespół | 3, dzień demo 20.09 |
| 5 | Encje `Venue` i `Event` oraz migracja wg sekcji „Schemat bazy danych”  | `backend/src/FlowBB.Domain`, `backend/src/FlowBB.Infrastructure` (pakiety EF Core, Npgsql i NetTopologySuite nie są jeszcze w projektach, więc wymagają zgody Kuby wg sekcji 15) | Data Lead + Kuba | 1 |
| 6 | Seeder: odczyt `events.seed.json` (`System.Text.Json`), upsert lokali, potem wydarzeń po `(Provider, ExternalId)`, `IsActive=false` dla zniknięć, jedna transakcja | `backend/src/FlowBB.Infrastructure` | Kuba | 5 |
| 7 | Dostarczenie pliku do kontenera: wolumen tylko do odczytu + ścieżka w konfiguracji (`Seed:EventsPath`), wpis w `.env.example` | `infra/docker-compose.yml`, `.env.example` | Integration Lead | 6 |
| 8 | Testy i weryfikacja (niżej) | testy .NET | Kuba | 6 |
| 9 | Fixture `events.json` regenerowany z seeda, żeby frontend miał realne dane | `contracts/fixtures/` | Kuba | 4, 6 |

Etapy 1–2 można zacząć od razu i równolegle. Etap 3 dotyczy tylko `data/seed/`, więc nie koliduje z backendem.

## Schemat bazy danych

Wystarczą dwie tabele: `venues` i `events`. Osobnych tabel na źródła, kategorie ani przebiegi ingestu nie ma. Pola `participantsCount`, `crewAvailable` i `availableTransportModes` z kontraktu są wyliczane w API, więc nie mają kolumn. Wydarzeń jest kilkadziesiąt, a raport ingestu i pełny snapshot są w plikach JSON w repo.

```sql
CREATE EXTENSION IF NOT EXISTS postgis;

CREATE TABLE venues (
    id        uuid PRIMARY KEY,
    name      text NOT NULL UNIQUE,
    aliases   text[] NOT NULL DEFAULT '{}',   -- nazwy ze źródeł, np. 'Duża Scena'
    address   text,
    location  geometry(Point, 4326) NOT NULL
);

CREATE TABLE events (
    id           uuid PRIMARY KEY,            -- z SHA-256 z "{provider}:{external_id}"
    provider     text NOT NULL,               -- 'teatr-polski' | 'cavatina-hall' | 'bb2026'
    external_id  text NOT NULL,
    source       text NOT NULL CHECK (source IN ('Demo', 'City', 'External')),
    name         text NOT NULL CHECK (char_length(name) BETWEEN 1 AND 160),
    description  text NOT NULL DEFAULT '' CHECK (char_length(description) <= 1000),
    category     text NOT NULL CHECK (category IN ('Culture', 'Sport', 'Education', 'Community', 'Other')),
    kind         text NOT NULL CHECK (kind IN ('session', 'festival')),
    starts_at    timestamptz NOT NULL,
    ends_at      timestamptz,
    venue_id     uuid NOT NULL REFERENCES venues (id),
    url          text,
    capacity     integer CHECK (capacity > 0),
    free_seats   integer,
    fetched_at   timestamptz NOT NULL,        -- kiedy snapshot pobrał capacity/free_seats
    is_active    boolean NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (provider, external_id),
    CHECK (ends_at IS NULL OR ends_at >= starts_at),
    CHECK ((capacity IS NULL AND free_seats IS NULL)
        OR (capacity IS NOT NULL AND free_seats BETWEEN 0 AND capacity))
);

CREATE INDEX ix_events_active_start ON events (starts_at) WHERE is_active;
CREATE INDEX ix_events_venue ON events (venue_id);
```

**Skąd bierze się każda kolumna (`events.seed.json`).**

| Kolumna | Źródło |
|---|---|
| `provider`, `external_id`, `name`, `url` | `source`, `externalId`, `title`, `url` |
| `source` (enum kontraktu) | mapowanie z `provider`: `teatr-polski`, `cavatina-hall` → `External`; `bb2026` → `City` |
| `category` | reguła w kroku A: Teatr i Cavatina → `Culture`; bb2026 → `Culture`, chyba że znaczniki wskażą inaczej (do ustalenia) |
| `description` | oczyszczony z HTML tekst, obcięty do 1000 znaków; Teatr nie ma opisu (pusty ciąg) |
| `kind` | `multiDay` lub `allDay` → `festival`, w przeciwnym razie `session` |
| `starts_at`, `ends_at` | `startAt`, `endAt`, konwertowane do UTC przy zapisie |
| `venue_id` | `venueId` przypisane w kroku A po nazwie lub aliasie lokalu |
| `capacity`, `free_seats` | tylko Teatr, dla pozostałych źródeł `NULL` |
| `fetched_at` | `fetchedAt` z nagłówka pliku |

Pola `status`, `allDay` i `multiDay` nie mają kolumn: skrypt filtruje `status == "open"` przed zapisem, a dwie pozostałe flagi zamieniają się w `kind`.

**Uzasadnienie wyborów.**
- **`Point` na `venues`, nie na `events`.** Przy 4–5 lokalach jedna pinezka do poprawienia zamiast kopii przy każdym wydarzeniu. DTO składa `location: { latitude, longitude }` z punktu lokalu, więc kontrakt się nie zmienia. To odstępstwo od pierwszej wersji planu, przyjęte w decyzjach powyżej.
- **`aliases text[]` zamiast tabeli aliasów.** Słownik ma kilka wierszy.
- **`fetched_at` przy `free_seats`.** To wartość z chwili snapshotu, nie dane na żywo. UI ma ją pokazać jako „stan z …”.
- **Bez indeksu GiST na `venues.location`.** Kilka wierszy. Agregacje przestrzenne PULSE liczą się z punktów `AttendanceIntent`, nie z lokali.
- **Bez `CHECK` na `source`.** Dodanie Pełnej Kultury w P1 nie wymaga migracji.
- **Bez `content_hash`.** Przy tej skali wystarczy upsert po `(provider, external_id)`.
- **`is_active` zamiast `DELETE`**, spójnie z `ON DELETE RESTRICT` w tabelach zależnych.

**Tabele zależne (własność Kuby, tu tylko klucz obcy).** `attendance_intents`, `groups` i `group_members` odwołują się do `events.id` z `ON DELETE RESTRICT`. `attendance_intents` ma unikalność `(user_id, event_id)`, co daje idempotentne `POST /attendance`.

## Szczegóły techniczne

**Krok A (Node, bez zależności).**
- Wejście: 3 źródła, wyjście: dwa pliki JSON (snapshot pełny i seed po `pinned`).
- Odrzucenie z powodem w raporcie: brak tytułu lub `startAt`, `endAt < startAt`, Teatr bez `status == "open"` lub bez `capacity`, lokal spoza słownika, wpisy `/en/` z Cavatiny.
- `kind = "festival"` dla `multiDay` lub `allDay`, w przeciwnym razie `"session"`. Dla wydarzeń demo preferujemy `session`.
- Pola wymagane przez kontrakt: `category`, `description` (bez HTML, po `…` obcięty do 1000 znaków) i `source`. Opis Cavatiny (`content.rendered`) jest duży, więc pobieramy go osobno tylko dla wydarzeń z `pinned.json`. W próbie 5 wpisów 3 miały pusty opis, a w bb2026 9 z 22 opisów przekracza 1000 znaków.
- Dedupe po `(znormalizowany tytuł, startAt)`. Sprawdziłem na danych z 2026-09-19: w Cavatinie to łączy jedno realne powtórzenie („Pokaz artystyczny Cavatina Hall”, 13.04.2027). Nie dedupujemy po samym `startAt`, bo równoległe wydarzenia mają ten sam termin (np. „BODYART” i koncert Ewy Bem 14.10).
- Wynik zależy wyłącznie od danych wejściowych i plików `pinned.json`/`venues.json`, a nie od czasu uruchomienia.

**Krok B (C#).**
- Stabilne ID wydarzenia: `Guid` zbudowany z pierwszych 16 bajtów SHA-256 z `"{provider}:{externalId}"`. Lokal dostaje ID tą samą metodą z `"venue:{id}"` (`id` ze `venues.json`). Nie używamy MD5 ani SHA-1, bo Sonar oznacza je jako słabe algorytmy (S4790).
- Upsert: najpierw lokale po `Id`, potem wydarzenia. Dla wydarzenia szukamy po `(Provider, ExternalId)`, aktualizujemy pola pochodzące ze źródła i nic więcej. Loader nigdy nie dotyka `AttendanceIntent`, grup ani agregatów.
- Brak `DELETE`. Wydarzenia nieobecne w seedzie dostają `IsActive = false`, bo usunięcie zerwałoby klucze obce.
- Lokalizacja to `Point` z `NetTopologySuite` na `Venue`, z listy `venues` w seedzie. Geometrię obsługuje EF Core z Npgsql, bez własnego mapowania.
- Czasy: `startAt` z ISO 8601 z offsetem trafia do `timestamptz`. Według dokumentacji Npgsql kolumna `timestamptz` przyjmuje `DateTimeOffset` tylko z offsetem 0, więc loader wywołuje `ToUniversalTime()` przed zapisem. Strefa Warszawy dopiero przy wyświetlaniu, jawnie.
- Kod zgodny z sekcją 15: Cognitive Complexity ≤ 10, metody do 40 linii, guard clauses, bez nowych paczek NuGet, bez `NOSONAR`.

**Dane realne a syntetyczne.**
- Tytuły, terminy, `Capacity` i `FreeSeats` z Teatru są realne. Popyt (`AttendanceIntent`) w seedzie jest syntetyczny i oznaczony jako `DEMO DATA / SYMULACJA`. Jedno pole nigdy nie miesza obu.
- Punkt lokalu z seeda służy jako cel trasy w `IRoutePlanner`. Heksagony PULSE liczą się z lokalizacji deklarujących „Idę” (osobny seed Data Leada), a nie z lokalu wydarzenia.

## Weryfikacja

| Test | Oczekiwanie |
|---|---|
| Seed uruchomiony 2 razy na czystej bazie | ta sama liczba wierszy `Event` i te same `Id` |
| Zmiana `title` w seedzie i restart | jeden wiersz zaktualizowany, brak duplikatu |
| Usunięcie rekordu z seeda i restart | wiersz zostaje z `IsActive = false`, `AttendanceIntent` nienaruszony |
| Rekord bez współrzędnych lokalu | krok A odrzuca go i wypisuje nazwę lokalu |
| Wydarzenie Teatru o 17:00 UTC | w API `19:00+02:00` (lato), a po 25.10 `+01:00` |
| Zapis `startAt` z offsetem `+02:00` | zapis przechodzi bez wyjątku Npgsql, w bazie UTC |
| Wstawienie `free_seats > capacity` lub `ends_at < starts_at` | baza odrzuca wiersz (`CHECK`) |
| Każdy rekord `events.seed.json` | spełnia ograniczenia `EventSummary`: `name` ≤ 160, `description` ≤ 1000, `category` i `source` z enumów kontraktu |
| Wydarzenie z `venue_id` spoza `venues` | baza odrzuca wiersz (klucz obcy) |
| `dotnet build`, `dotnet test`, Sonar | bez nowych problemów Critical/Blocker/Major |
| `docker compose config` i health API | bez błędów |
| Krok A z wyłączonym internetem | nic nie zapisuje i kończy się błędem (bez `--partial`), API startuje z poprzedniego seeda |

## Ryzyka i założenia

- **Kontrakt.** `EventSummary` nie ma pól `capacity`, `freeSeats` ani `url`, więc dane Teatru o miejscach są w bazie, ale niewidoczne w API. Ich wystawienie (np. do PULSE) to zmiana kontraktu, którą akceptuje tylko Kuba. Do P0 nie jest potrzebna.
- **Czas.** Prezentacja jest 20.09, więc snapshot i `pinned.json` trzeba zamknąć dziś, przed feature freeze (3,5 h przed prezentacją). Wydarzenia z 20.09 mogą już być w przeszłości w chwili pokazu (np. „Tina” 20.09 o 16:00, wyprzedana), zależnie od godziny prezentacji. Loader ostrzega, gdy wydarzenie z seeda zaczyna się przed `teraz + 2 h`.
- **Kandydaci do `pinned.json`** (z danych z 19.09, do potwierdzenia świeżym snapshotem): „Seksmisja” 27.09 (402 miejsca, 0 wolnych), 29.09 (1 wolne), 30.09 (2 wolne) oraz „Kabaret” 2.10 (260 wolnych) dla kontrastu. Wszystkie są po dniu demo.
- **Endpoint Teatru** jest wewnętrzny dla ich frontu i może zniknąć. Dlatego snapshot jest commitowany, a demo nie zależy od sieci.
- **Lokale.** Jeden wiersz dla Dużej i Małej Sceny to założenie bez potwierdzenia w źródłach. Jeśli okaże się błędne, wystarczy rozdzielić wiersz w `venues.json`. Współrzędne każdego lokalu ustala człowiek, na razie bez wartości w repo.
- **Kategorie.** Źródła nie mapują się na enum `category` z kontraktu bez reguły. Cavatina ma tylko numery ID kategorii, więc dla Teatru i Cavatiny stała `Culture` wystarcza do P0.
- **Kto ma współrzędne.** Odpowiedź „tylko 4–5 lokali” określa zakres, ale nie osobę, która je wpisuje. Zakładam Data Leada, z weryfikacją pinezek na mapie.

## Do uzupełnienia

1. Godzina prezentacji 20.09 (decyduje, czy wydarzenia z tego dnia wchodzą do `pinned.json`).
2. Ścieżka pliku seeda w kontenerze (etap 7, Integration Lead). Domyślnie wolumen tylko do odczytu z `data/seed` i konfiguracja `Seed:EventsPath`.
3. Osoba wpisująca współrzędne 5 lokali. Zakładam Data Leada, z weryfikacją pinezek na mapie.
4. Potwierdzenie Kuby: mapowanie `source` (bb2026 → `City`) i zgoda na pakiety EF Core, Npgsql i NetTopologySuite w `FlowBB.Infrastructure`.
5. Reguła `category` dla wydarzeń bb2026 (domyślnie `Culture`).
