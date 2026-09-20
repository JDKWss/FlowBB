# Runbook demo FlowBB

Instrukcja uruchomienia i przeprowadzenia krytycznego scenariusza demo (AGENTS.md, sekcja 2) oraz plan awaryjny.
**Status: 2026-09-20 (`develop` po #90 i #91).** Luka powrotowa (ReturnGap) jest liczona przez `DemoReturnGapPolicy`
i pokryta testami `Smoke/` (sekcja 13). Stos uruchamia
sie od zera przez Compose, a `infra/smoke-test.ps1` konczy sie wynikiem
13 PASS / 0 FAIL / 0 SKIP (szczegoly w sekcji 10, przebiegi na Windows i na
Linuksie). Przeplyw klienta Events -> Attendance -> realna trasa drogowa ->
Crew jest podlaczony do lokalnych uslug. Klient korzysta z wolnego uzytkownika
`dddddddd-...`, wiec pierwszy klik "Ide" pokazuje `82 -> 83`; konto
`aaaaaaaa-...` pozostaje wolne dla smoke testu.

## 1. Status krokow scenariusza

| # | Krok | Endpoint / element | Status |
|---|---|---|---|
| 1 | Uzytkownik otwiera wydarzenie w `/client` | `GET /api/events`, `GET /api/events/{id}` | dziala (smoke, lokalny Neo4j) |
| 2 | Klika "Ide" i wybiera srodek transportu | `POST /api/events/{id}/attendance` | dziala; uzytkownik klienta `dddddddd-...` dostaje `isNew: true` na wydarzeniu `1111...` |
| 3 | API zapisuje deklaracje w Neo4j | relacja `IS_GOING_TO` ze snapshotem | dziala (smoke, testy `FlowBB.Infrastructure.Tests`); na Aurze niepotwierdzone |
| 4 | Backend przelicza agregaty | logika PULSE w C#, `DemoReturnGapPolicy` | dziala (smoke: PULSE zgodny z Attendance; ReturnGap: wydarzenie po 22:00 daje `participantsWithoutReturn` i alert `ReturnGap`) |
| 5 | SignalR wysyla `PulseUpdated` | hub `/hubs/pulse` | dziala: klient huba odbiera komunikat razem z `participantsWithoutReturn` w `Smoke/` (`PulseUpdatedSmokeTests`); dodatkowo `AttendanceSignalRFlowTests` |
| 6 | Dashboard pokazuje licznik bez odswiezania (`82 -> 83`) | `/dashboard`, klient SignalR | dziala; lokalny przebieg przegladarkowy potwierdzil `82 -> 83` oraz Walking `16 -> 17` bez odswiezenia |
| 7 | Uzytkownik widzi trase z `IRoutePlanner` | `GET /api/events/{id}/route?userId={userId}` | Walking/Bike/Car zwracaja `RoadRouting`, dystans i GeoJSON; PublicTransport oraz kontrolowany fallback zwracaja `Demo` |
| 8 | Uzytkownik dolacza do mikrogrupy CREW | `GET groups`, `POST/DELETE members` | dziala (smoke: dolaczenie +1, ponowienie bez zmian, opuszczenie 204 x2) |
| 9 | Dashboard pokazuje popyt na mapie heksagonalnej | `GET /api/pulse/hexagons` | dziala (smoke: 4 komorki, wszystkie >= 10 osob, bez `userId`); na Aurze niepotwierdzone |

Na lokalnym Neo4j 5.26 Community (Docker) zweryfikowano Events, idempotentny
i rownolegly zapis Attendance, PULSE, heksagony, Routing oraz Crew. Przeplyw
klienta do MapLibre i Crew przeszedl w prawdziwej przegladarce; Neo4j Aura,
pelny przebieg SignalR z dashboardem zostal potwierdzony lokalnie w
przegladarce; proba prezentacji z timerem pozostaje do wykonania.

## 2. Wymagania

- Docker (Compose v2) albo .NET SDK 10 do uruchomienia API lokalnie.
- PowerShell 7 (`pwsh`) do skryptu smoke testu. Na Linuksie: `dotnet tool install --global PowerShell` albo pakiet z repozytorium dystrybucji.
- Wolne miejsce na dysku: obrazy API i Neo4j oraz ich warstwy zajmuja kilka GB. Przy pelnym dysku Neo4j nie startuje (`No space left on device` w `docker logs`, kod wyjscia 70), a API startuje bez niego.
- Tylko dla opcjonalnego realnego routingu drogowego (profil `real-routing`): dostep do internetu i wolumen `routing-data`
  przygotowany jednorazowo komenda `routing-prepare` (krok 4 ponizej). Domyslne demo tego nie potrzebuje i dziala bez internetu.
- Neo4j: instancja Aura (patrz `backend/README.md`) albo lokalny kontener (profil `local-db`; w `.env`: `NEO4J_URI=neo4j://neo4j:7687`).
- Przegladarka desktopowa dla `/dashboard`, przegladarka w mobilnym viewporcie dla `/client`.
- Zadnych sekretow w repozytorium: hasla i dane polaczenia tylko w `.env` (ignorowany przez git), wzor w `.env.example`.

## 3. Uruchomienie od czystego srodowiska

1. Sklonuj repozytorium i przejdz na `develop`: `git clone https://github.com/JDKWss/FlowBB.git` i `git switch develop`.
2. Skopiuj `.env.example` do `.env` i uzupelnij `NEO4J_URI`, `NEO4J_DATABASE`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`
   (dla lokalnego kontenera dowolne nowe haslo). Jesli port 5341 jest zajety (np. przez lokalny Seq), ustaw `SEQ_HOST_PORT`.
3. `NEO4J_SEED_ON_STARTUP=true` (domyslnie w `.env.example`) sprawia, ze API przy starcie wykonuje constraints i
   `database/flowbb-demo-seed.cypher`. Compose przekazuje te zmienna do kontenera `api`. Seed jest idempotentny, ale
   **kazdy restart API przywraca stan seedu** (deklaracje i czlonkostwa dodane w trakcie demo znikaja).
4. **Tylko dla realnego routingu drogowego** (domyslnie pomin): ustaw w `.env` `ROUTING_MODE=RoadRouting` i przygotuj grafy
   Walking/Bike/Car (jednorazowo, wymaga sieci i Overpass):
   `docker compose -f infra/docker-compose.yml --env-file .env --profile routing-tools run --rm routing-prepare`.
5. Uruchom stos (z lokalnym Neo4j; dla Aury pomin `--profile local-db`):
   `docker compose -f infra/docker-compose.yml --env-file .env --profile local-db up --build -d`.
   API czeka na zdrowy kontener Neo4j (ok. 30 s), a potem wykonuje seed.
   **Domyslny start (`ROUTING_MODE=Demo`) nie tworzy kontenera `routing`**, wiec nic nie jest `unhealthy`, a `/route` zwraca
   `plannerSource: Demo` bez wywolan uslugi drogowej, timeoutow i ostrzezen. Dla `RoadRouting` dodaj drugi profil:
   `--profile local-db --profile real-routing`; przy braku grafow lub niedostepnej uslugi API wraca do `DemoRoutePlanner`
   (log `Road routing unavailable ...`), a bledy logiczne (np. `route_not_found`) nie sa ukrywane.
   **Kilka kopii repozytorium (worktree) na jednej maszynie:** domyslna nazwa projektu Compose to nazwa katalogu `infra`, wiec
   kazda kopia dzieli wolumeny (m.in. haslo Neo4j z pierwszego startu), co konczy sie bledem logowania do Neo4j. Dodaj do kazdej
   komendy `docker compose` wlasna nazwe projektu, np. `-p flowbb-demo` (kontenery to wtedy `flowbb-demo-api-1` itd.), oraz osobny
   `SEQ_HOST_PORT`.
6. Sprawdz zdrowie API: `GET http://localhost:8080/health` powinno zwrocic `200 {"status":"Healthy","timestamp":"..."}` (`/health/ready` sprawdza dodatkowo Neo4j). Wewnetrzny `/health` kontenera `routing` ma status `ready` tylko po zaladowaniu wszystkich trzech grafow.
7. Otworz Scalar z OpenAPI (srodowisko Development): `http://localhost:8080/scalar`.
8. Uruchom klienta: `cd client && npm ci && VITE_API_URL=http://localhost:8080 npm run dev`.
9. Uruchom dashboard: `cd dashboard && npm ci && VITE_API_URL=http://localhost:8080 npm run dev -- --port 5174`.
10. Uruchom smoke test (sekcja 5).

API mozna tez uruchomic recznie: `dotnet run --project backend/src/FlowBB.Api --urls http://localhost:8080`.
Glowny host udostepnia `/health`, `/health/ready`, `/hubs/pulse` oraz endpointy Events, Attendance, Crew, PULSE i Routing.
Zatrzymanie i pelny reset lokalnego stanu (wolumeny Neo4j, Seq, routing); uwzglednij oba profile, jesli uzywales `real-routing`:
`docker compose -f infra/docker-compose.yml --env-file .env --profile local-db --profile real-routing down -v`.

## 4. Docelowy przebieg prezentacji (10 minut)

Dane sa syntetyczne i oznaczone w UI jako `DEMO DATA / SYMULACJA`.
Seed utrzymuje 82 uczestnikow wydarzenia `11111111-...`, ale uzytkownik klienta
`dddddddd-dddd-dddd-dddd-dddddddddddd` pozostaje poza wszystkimi wydarzeniami i grupami. Jest przeznaczony do pokazania
pierwszej deklaracji `isNew: true`, zmiany licznika `82 -> 83`, trasy oraz dolaczenia do Crew.

| Krok | Co robisz | Oczekiwany rezultat |
|---|---|---|
| 1 | Otworz `/dashboard` na duzym ekranie, wybierz wydarzenie "Koncert na Rynku" | KPI z aktualnym licznikiem, mapa heksagonow (komorki z co najmniej 10 osobami) |
| 2 | Na telefonie (lub mobilnym viewporcie) otworz `/client`, wybierz to samo wydarzenie | Lista i szczegoly wydarzenia |
| 3 | Kliknij "Ide", wybierz srodek transportu | Odpowiedz 200; `isNew: true` |
| 4 | Patrz na dashboard | Licznik zmienia sie o 1 (np. `82 -> 83`) bez odswiezania strony, a modal split odzwierciedla wybrany tryb |
| 5 | Kliknij "Ide" jeszcze raz (ten sam tryb) | Licznik **bez zmian** (idempotencja); zmiana trybu zmienia tylko modal split |
| 6 | Pokaz karte trasy (tam i z powrotem) | Dla Walking/Bike/Car: `plannerSource: RoadRouting`, dystans i linia po drogach z backendowego GeoJSON |
| 7 | Dolacz do mikrogrupy Crew | Licznik czlonkow +1; ponowne dolaczenie nie zmienia licznika |
| 8 | Wroc na dashboard, pokaz mape | Zagregowany popyt na heksagonach; brak komorek ponizej 10 osob, brak identyfikatorow uzytkownikow |
| 9 | W dashboardzie wybierz "Nocny Bieg na Blonich" (koniec 23:15) | `participantsWithoutReturn` > 0 (w seedzie 10) i alert `ReturnGap` z poziomem `Warning`; w `/client` trasa `PublicTransport` na tym wydarzeniu ma `returnGap: true` i brak powrotow. **Widok alertu do potwierdzenia wzrokowo w probie z timerem (#57)**; backend sprawdza `Smoke/` |

Na koniec pokazu wykonaj sprzatanie (sekcja 6), zeby kolejne uruchomienie startowalo od tego samego stanu.

### Przeplyw organizatora

1. W dashboardzie wybierz `Add event`.
2. Wypelnij nazwe, opis, miejsce, kategorie i czas.
3. Kliknij punkt na mapie Bielska-Bialej (dla deterministycznego demo:
   okolice `49.8215, 19.0455`) i sprawdz znacznik oraz wspolrzedne.
4. `Create event` wykonuje `POST /api/events`; oczekiwane jest `201 Created`,
   `source: External` i `participantsCount: 0`.
5. Przejdz do PULSE albo odswiez `/client`: wydarzenie pochodzi z
   `GET /api/events`, bez dopisywania fixture'a.

Backend generuje osobne identyfikatory Event i Venue. Neo4j zapisuje oba wezly
oraz `(Event)-[:HOSTED_AT]->(Venue)` w jednej transakcji.

## 5. Automatyczny smoke test

```powershell
pwsh infra/smoke-test.ps1 -BaseUrl http://localhost:8080
```

Skrypt wykonuje kroki scenariusza przez HTTP i konczy sie wynikiem PASS, FAIL albo SKIP dla kazdego z nich:

- **PASS** - krok wykonany i spelnia kryteria (np. ponowny POST nie zwieksza licznika, hex >= 10 osob, brak `userId`).
- **FAIL** - kryterium nie spelnione; kod wyjscia 1.
- **SKIP** - endpoint nie jest jeszcze podpiety w API (404 bez ProblemDetails). To nie blad, ale scenariusz nie jest wtedy w pelni sprawdzony.
  Kod wyjscia 2 oznacza, ze API jest nieosiagalne.

Opcje: `-EventId`, `-UserId`, `-CrewEventId`, `-TransportMode`, `-KeepData` (nie sprzataj po tescie).
Domyslnie smoke test Attendance i Crew korzysta z wydarzenia `11111111-...` oraz uzytkownika `aaaaaaaa-...`, ktory nie ma
poczatkowej deklaracji ani czlonkostwa w grupie. Skrypt tworzy deklaracje i usuwa ja na koncu, o ile sam ja utworzyl,
wiec mozna go uruchamiac wielokrotnie. Brak grupy do dolaczenia jest FAIL, nie PASS.
Trase skrypt pobiera wylacznie przez `GET ...?userId=` (historyczny fallback do POST zostal usuniety w #118).

Dokladniejszy zestaw przypadkow brzegowych (400/404/409, idempotencja, hub SignalR, ReturnGap) to testy `Smoke/` uruchamiane
przez `dotnet test` na tym samym stosie (opis: `backend/tests/FlowBB.Api.IntegrationTests/Smoke/README.md`):

```powershell
$env:FLOWBB_SMOKE_BASE_URL = 'http://localhost:8080'
dotnet test backend/tests/FlowBB.Api.IntegrationTests --filter "FullyQualifiedName~Smoke"
```

Uzupelniaja skrypt, nie zastepuja go. Nie uruchamiaj ich rownolegle z testami `FlowBB.Infrastructure.Tests` na tej samej bazie.

**Czego skrypt nie sprawdza:** samego komunikatu SignalR (tylko negocjacje huba; komunikat, w tym `participantsWithoutReturn`,
pokrywa `PulseUpdatedSmokeTests` w `Smoke/`), ani zachowania frontendu. Krok 4 scenariusza sprawdzaj wzrokowo na dashboardzie.

## 6. Sprzatanie i reset stanu

- Skrypt smoke testu usuwa swoja deklaracje (`DELETE attendance` jest idempotentne).
- Po pokazie usun deklaracje uzytkownika demo: `DELETE /api/events/{eventId}/attendance/{userId}` (204 nawet gdy jej nie ma)
  oraz opusc mikrogrupe: `DELETE /api/groups/{groupId}/members/{userId}`.
- Reset danych demonstracyjnych bez kasowania wolumenow: zrestartuj API z `NEO4J_SEED_ON_STARTUP=true`
  (`docker restart infra-api-1`). Pelny reset: `down -v` (sekcja 3).

## 7. Plan awaryjny

| Awaria | Objaw | Co robisz |
|---|---|---|
| Neo4j niedostepne | `/health` 200, ale `/health/ready` 503, a Attendance/PULSE zwracaja 500 | Przelacz na lokalny kontener (`--profile local-db`) i zaladuj seed ponownie; ostatecznie pokaz nagranie z backupu |
| Brak internetu | Aura lub kafle mapy nieosiagalne | Lokalny kontener Neo4j; `DemoRoutePlanner` dziala offline. Proponowana prywatna usluga FastAPI ma korzystac z wczesniej przygotowanego lokalnego grafu, ale OpenFreeMap wymaga sieci, dopoki kafle/style nie sa osobno cache'owane |
| Usluga routingu niedostepna lub graf niezaladowany | Health uslugi nie jest ready albo ASP.NET przekracza timeout | Przy `ROUTING_DEMO_FALLBACK_ENABLED=true` kompozyt automatycznie zwraca jawne `plannerSource: Demo`; nie obejmuje to blednego trybu, nieprawidlowych danych, uszkodzonej odpowiedzi ani `route_not_found` |
| SignalR nie laczy sie | Licznik nie zmienia sie na zywo | Odswiez dashboard (odpowiedz REST zawiera aktualny licznik); sprawdz CORS i adres API w `.env` |
| Telefon nie widzi API | `/client` bez danych | Uzyj mobilnego viewportu w przegladarce na laptopie; awaryjnie tunel `cloudflared` do API |
| Mapa pusta | Brak komorek na `/api/pulse/hexagons` | Za malo osob w jednej komorce (prog 10): dosiej dane demo lub zmniejsz rozmiar siatki (obecnie 900 m) - to decyzja Core Ownera |
| Test smoke FAIL na demo | Skrypt konczy sie kodem 1 | Nie prezentuj kroku, ktory zawiodl; napraw przed pokazem, backup nagrania jako zapas |

Backup: nagraj przebieg scenariusza (sekcja 4) i zapisz zrzuty ekranu dashboardu przed prezentacja. Po feature freeze wykonaj dwie proby z timerem.

## 8. Znane zalozenia i ograniczenia MVP

- Luka powrotowa to **symulacja** (`DemoReturnGapPolicy`, bez danych rozkladowych MZK): uczestnik z `PublicTransport` nie ma
  dogodnego powrotu, gdy wydarzenie konczy sie o 22:00 lub pozniej w `Europe/Warsaw`; brak `EndAt` oznacza brak luki.
  Wynik trafia do `participantsWithoutReturn` (PULSE, `summary`, `PulseUpdated`), alertu `ReturnGap` (`Warning`) oraz
  `returnGap: true` z pustym `returns` w trasie. W seedzie tylko "Nocny Bieg na Blonich" (koniec 23:15) ma luke (10 osob).
- Siatka heksagonow: rozmiar 900 m, lokalny rzut metryczny wokol Rynku (nie EPSG:2180); komorki `count < 10` nie sa zwracane.
- `GET /api/pulse/summary` odpytuje wydarzenia po kolei (N+1); przy dziesiatkach wydarzen jest to wystarczajace dla demo.
- Routing: kanoniczny kontrakt to
  `GET /api/events/{eventId}/route?userId={userId}`. Punkt startu i tryb maja
  pochodzic ze snapshotu Attendance, a nie z body zadania. `IRoutePlanner` i
  kompozyt plannerow sa zaimplementowane i podlaczone.
  Dane MZK sa niekompletne i nie stanowia grafu routingu. Proponowany realny
  routing Walking/Bike/Car zostal zaimplementowany jako prywatna usluga
  Python/FastAPI z lokalnymi grafami OSM oraz wewnetrzny klient ASP.NET;
  przegladarka nigdy nie wywoluje FastAPI bezposrednio. Publiczny kontrakt ma
  `RoadRouting`, opcjonalny dystans i GeoJSON LineString. PublicTransport nie
  wchodzi do uslugi drogowej i pozostaje deterministycznym Demo. Szczegoly:
  `docs/ROUTING_SERVICE.md`.
- PostgreSQL/PostGIS w `data/gtfs/mzk/` to odseparowany PoC, nie baza aplikacji (patrz `docs/adr/001-runtime-persistence.md`).
- Relacja `IS_GOING_TO` przechowuje `TransportMode`, `OriginLatitude`,
  `OriginLongitude` i `UpdatedAt`. Modal split i mapa PULSE sa wyliczane w C#
  ze snapshotow odczytanych z Neo4j.
- Seed ma 84 syntetycznych uzytkownikow. Uczestnicy wydarzenia o liczbie `N` to uzytkownicy `d100...002` do
  `d100...N+1`, dlatego liczniki pozostaja `82/46/28/64`. Konta `aaaaaaaa-...` (smoke test) oraz
  `dddddddd-...` (klient) sa wolne do scenariusza `82 -> 83`. Ponowne wykonanie seedu usuwa ich deklaracje
  i czlonkostwa w seedowanych grupach.
- **Historyczna uwaga:** wczesniejszy branch kliencki eksperymentowal z POST
  i punktem startu w body. Nie jest to aktualny kontrakt `develop`.

## 9. Kryteria gotowosci demo

- [x] projekt uruchamia sie od czystego srodowiska (sekcja 3; zweryfikowane 2026-09-20),
- [x] Neo4j startuje i przechodzi health check (lokalny kontener),
- [x] seed jest idempotentny (restart API, smoke dwukrotnie),
- [x] Events dziala,
- [x] Attendance dziala idempotentnie,
- [x] SignalR publikuje aktualizacje (dashboard pokazuje `+1` bez odswiezania),
- [x] Walking/Bike/Car zwracaja `RoadRouting`, a kontrolowany fallback i PublicTransport zwracaja `Demo`,
- [x] Crew dziala (smoke),
- [x] ReturnGap: wydarzenie konczace sie po 22:00 daje `participantsWithoutReturn` i alert, a trasa `returnGap: true` (`Smoke/`),
- [x] PULSE nie ujawnia danych dla `count < 10` (smoke i testy),
- [x] Scalar prezentuje aktualne OpenAPI (`/scalar/v1` 200; wygenerowany dokument ma te same operacje co kontrakt poza
  `getReadiness`, ktorego opisuje tylko `contracts/openapi.yaml`; sprawdza to `RuntimeApiDocumentationTests`),
- [x] `dotnet build` i `dotnet test` przechodza, a `infra/smoke-test.ps1` konczy sie bez FAIL i bez SKIP,
- [ ] scenariusz demo zostal przecwiczony dwa razy z timerem.

## 10. Wynik ostatniej weryfikacji (issue #72)

Data: 2026-09-20. Srodowisko: Windows 11, Docker 29.2.1 (Compose 5.0.2), lokalny Neo4j 5.26.30 Community (`--profile local-db`),
czyste srodowisko (bez woluminow, obrazy zbudowane od zera).

| Sprawdzenie | Wynik |
|---|---|
| `docker compose ... --env-file .env --profile local-db up --build -d` od zera | OK (ok. 20 s po zbudowanych obrazach); API czeka na zdrowy Neo4j |
| Seed przy starcie (`NEO4J_SEED_ON_STARTUP`) | `User=83`; uzytkownik `aaaaaaaa-...` ma zero relacji `IS_GOING_TO` i `MEMBER_OF`; wydarzenia maja `82/46/28/64` uczestnikow |
| `/health` i `/health/ready` | `Healthy`; kontener `api` `healthy` |
| `infra/smoke-test.ps1` (dwa przebiegi z rzedu) | 13 PASS, 0 FAIL, 0 SKIP, kod 0 |
| Reczny POST Attendance `aaaa...` na `1111...`, potem DELETE | `isNew: true`, `82 -> 83`; DELETE 204 i powrot do 82 |
| Restart API po pozostawieniu deklaracji | wraca `healthy`; seed usuwa deklaracje uzytkownika demo i przywraca `82/46/28/64` |
| PULSE hexagons dla czterech wydarzen | `1111`: 4 komorki (min. 20), `3333`: 4 (min. 11), `4444`: 0 (28 osob rozproszonych ponizej progu), `5555`: 4 (min. 16); zadna zwrocona komorka nie ma `count < 10` |
| `dotnet build backend/FlowBB.sln` | OK, 0 ostrzezen i 0 bledow |
| `dotnet test backend/FlowBB.sln` z jednorazowym Neo4j | 457 PASS, 0 FAIL, 0 SKIP; wszystkie 53 testy Infrastructure wykonane |
| `dotnet format backend/FlowBB.sln --verify-no-changes` | OK |
| Kontener `routing` bez `routing-prepare` | `unhealthy` (brak grafow), API dziala z `DemoRoutePlanner` |
| Konfiguracja Compose bez profilu `local-db` (Aura) | `docker compose config` OK; brak testu z prawdziwa Aura (brak dostepu) |
| Scenariusz demo z timerem, dwa razy | **niewykonane** (wymaga czlowieka, `/client` i `/dashboard` w przegladarce) |
| `+1` na dashboardzie przez SignalR w przegladarce | **niewykonane wzrokowo**; backend i seed daja `82 -> 83`, komunikat pokrywaja testy integracyjne |

### Powtorzenie na Linuksie po #58 i #59 (issue #57)

Data: 2026-09-20. Srodowisko: Ubuntu 26.04, Docker 29.8.0 (Compose 5.5.1), lokalny Neo4j 5.26.30 Community (`--profile local-db`),
wolumeny Neo4j i Seq utworzone od zera, obrazy `api` zbudowane od zera na kodzie po usunieciu EF Core/Npgsql (#58) i starego repozytorium grafu (#59).
Skrypt smoke uruchomiono przez `pwsh` 7.6.6.

| Sprawdzenie | Wynik |
|---|---|
| `docker compose -f infra/docker-compose.yml --env-file .env --profile local-db up --build -d` | obraz `api` buduje sie i startuje; Neo4j i API `healthy`, `/health` i `/health/ready` 200 |
| Inicjalizator po #59 (`Neo4jDatabaseInitializer` na `IDriver`) | log `Initializing Neo4j schema and idempotent seed data.` bez bledow; `SHOW CONSTRAINTS` zwraca osiem oczekiwanych constraintow; `User=83`, `Event=4`, `Venue=4`, `Crew=2`, `BusinessOwner=1`, `Tag=4`; uczestnicy `82/46/28/64`; uzytkownik `aaaaaaaa-...` bez relacji |
| `infra/smoke-test.ps1` (dwa przebiegi z rzedu) | 13 PASS, 0 FAIL, 0 SKIP, kod 0 |
| `dotnet test backend/tests/FlowBB.Api.IntegrationTests --filter Smoke` z `FLOWBB_SMOKE_BASE_URL` | 59 PASS, 0 FAIL, 0 SKIP (w tym `PulseUpdatedSmokeTests`: klient huba odbiera `PulseUpdated`) |
| `dotnet test backend/FlowBB.sln` z `FLOWBB_NEO4J_TEST_*` | Domain 93, Application 88, Infrastructure 53 (wszystkie wykonane na prawdziwym Neo4j, 0 pominietych), Api.IntegrationTests 236 PASS + 38 pominietych (smoke, ktore uruchomiono osobno linia wyzej); 0 FAIL |
| Testy Infrastructure (prawdziwy Neo4j) i smoke uruchomione **rownolegle** na tej samej instancji | **niestabilne**: `GET /api/pulse/summary` zwraca 500 w 4 z 6 przebiegow. Testy `Neo4jEventRepositoryTests` celowo wstawiaja wydarzenia z nieznana kategoria i bez miejsca, a lista wydarzen rzuca `InvalidOperationException` na uszkodzonym wydarzeniu (`NEO4J_ADAPTER_RECONCILIATION.md`, 3.D). Uruchomione **po kolei** (Infrastructure 53/53, potem `Api.IntegrationTests` 295/295 ze smoke) przechodza; nie uruchamiaj ich rownolegle na jednej bazie |
| Reczny POST Attendance `aaaa...` na `1111...`, potem restart API (`docker restart infra-api-1`) | `isNew: true`, `82 -> 83`; po restarcie API `healthy`, licznik znowu `82` (seed przywraca stan) |
| `dotnet build backend/FlowBB.sln` | OK, 0 ostrzezen i 0 bledow |
| `dotnet format backend/FlowBB.sln --verify-no-changes` | OK |
| API bez kontenera `routing` | `DemoRoutePlanner` zwraca trase (`plannerSource: Demo`); API nie zalezy od `routing` |
| Scalar i OpenAPI | `/scalar/v1` 200, `/openapi/v1.json` 200 z 12 sciezkami; **rozbieznosc:** `/health/ready` jest w `contracts/openapi.yaml`, ale brakuje go w wygenerowanym dokumencie |
| Neo4j Aura | **niezweryfikowane** (brak dostepu); tylko `docker compose config` |
| Analiza Sonar | **nie uruchomiona** (repozytorium nie ma konfiguracji SonarQube); zero ostrzezen kompilatora nie zastepuje analizy |
| Scenariusz demo z timerem, dwa razy | **niewykonane** (wymaga czlowieka, `/client` i `/dashboard` w przegladarce) |
| `+1` na dashboardzie w przegladarce | **niewykonane wzrokowo**; backend, seed i dostarczenie `PulseUpdated` sprawdzone testami |
| Kontener `routing` | w tym przebiegu **nie uruchomiony** (zbudowany obraz usunieto, bo dysk byl pelny); zachowanie `unhealthy` bez `routing-prepare` opisuje przebieg z Windows |

## 11. Lokalna weryfikacja dashboardu i tworzenia wydarzenia

Data: 2026-09-20. Srodowisko: Linux, lokalny Neo4j i API w Compose,
dashboard Vite oraz klient Vite.

- Dashboard wyslal `POST /api/events` i otrzymal `201`.
- Utworzono `FlowBB Demo Event` (`026d66f4-10e0-495f-9159-0f3274f6b315`)
  w punkcie `49.819671085394305, 19.044383747064757`.
- Odpowiedz miala `source: External` i `participantsCount: 0`.
- Bezposrednie zapytanie Neo4j potwierdzilo Event, dedykowany Venue,
  jedna relacje `HOSTED_AT` i brak zapisanego licznika uczestnikow.
- `/client` pokazal nowe wydarzenie; pierwszy POST Attendance dla niego zwrocil
  `isNew: true`, a Walking zwrocil realna trase `RoadRouting`.
- Dla `Koncert na Rynku` dashboard pokazal `82`, odebral `PulseUpdated`, a
  nastepnie bez odswiezania pokazal `83`; Walking zmienil sie `16 -> 17`.
  Po komunikacie dashboard uzgodnil Event PULSE, summary, Events i hexagony
  przez REST.
- Zatrzymanie API pokazalo `Reconnecting`; po uruchomieniu automatyczny
  reconnect przywrocil `Live`, bez awarii widoku.
- Odpowiedzi PULSE nie zawieraly `userId`, punktow startowych ani tras.
  Najmniejsza zwrocona komorka miala 20 uczestnikow.
- Dashboard i klient porownano obok siebie: wspolna czarna baza, neutralne
  karty, biala typografia, mietowy akcent, zaokraglenia i styl OpenFreeMap.

## 12. Profile Compose i tryb tras (issue #90)

Data: 2026-09-20. Srodowisko: Ubuntu 26.04, Docker 29.8.0 (Compose 5.5.1), lokalny Neo4j 5.26.30 Community (`--profile local-db`),
`pwsh` 7.6.6. Obie konfiguracje uruchomiono od czystych wolumenow (`down -v`), obrazy `api` zbudowane na przypietych obrazach .NET
(SDK 10.0.401, aspnet 10.0.12).

| Sprawdzenie | `ROUTING_MODE=Demo` (domyslnie, bez `real-routing`) | `ROUTING_MODE=RoadRouting` + `--profile real-routing` |
|---|---|---|
| `docker compose config` | OK, uslugi: `api`, `seq`, `neo4j` (z `local-db`) | OK, dodatkowo `routing` |
| Kontenery po starcie | `api`, `neo4j` healthy, `seq`; **brak kontenera `routing`, nic `unhealthy`** | `api`, `neo4j`, `routing` healthy (po `routing-prepare`, 5 min 4 s) |
| `/route` Walking, Bike, Car | `plannerSource: Demo` | `RoadRouting` (dystans i geometria), np. Walking 1625,6 m |
| `/route` PublicTransport | `Demo` | `Demo` (PublicTransport nie idzie przez usluge drogowa) |
| `infra/smoke-test.ps1` (dwa przebiegi) | 13 PASS, 0 FAIL, 0 SKIP | 13 PASS, 0 FAIL, 0 SKIP |
| `Smoke/` (`FLOWBB_SMOKE_BASE_URL`) | 68 PASS, 0 FAIL | 68 PASS, 0 FAIL |
| Ostrzezenia i bledy w logu API | 0 (brak `Road routing unavailable` i timeoutow) | 0 |
| Restart API (`docker restart infra-api-1`) | POST `82 -> 83`, po restarcie `82`, API `healthy` | nie powtarzano |

Uwagi:

- **`routing-prepare` jest w osobnym profilu `routing-tools`**, a nie w `real-routing`. Z obiema uslugami w `real-routing` `up`
  uruchamial `routing-prepare`, czyli ponownie pobieral OSM z Overpass i przebudowywal grafy, gdy `routing` je czyta
  (zaobserwowane w tej weryfikacji). To odstepstwo od tresci issue #90.
- **Test bez internetu w trybie domyslnym: niewykonany jako pomiar.** Proba symulacji siecia `internal` nie byla
  wiarygodna (API nie polaczylo sie wtedy z Neo4j), wiec jej nie liczymy. Wiadomo tyle, ze w trybie `Demo` API nie ma zadnego
  wywolania uslugi drogowej (`IRoutePlanner` to `DemoRoutePlanner`, sprawdza to `RoutingModeTests`), a stos nie zawiera kontenera `routing`.
- Neo4j Aura, przegladarka i proba z timerem: bez zmian, niewykonane.

## 13. Wynik weryfikacji ReturnGap i `Smoke/` (issue #91)

Data: 2026-09-20. Srodowisko: Windows 11, Docker 29.2.1 (Compose 5.0.2), lokalny Neo4j 5.26.30 Community (`--profile local-db`),
domyslny `ROUTING_MODE=Demo`, czyste wolumeny, obrazy zbudowane od zera. Kroki 2, 5, 6 i 10 z sekcji 3 wykonano przez skopiowanie
komend z tego dokumentu; jedyne odstepstwa to `-p flowbb-91b` (patrz uwaga o wielu kopiach repozytorium) i `SEQ_HOST_PORT=5350`
(port 5341 zajety przez lokalny Seq).

| Sprawdzenie | Wynik |
|---|---|
| Krok 5: `docker compose ... --profile local-db up --build -d` | OK; kontenery `api`, `neo4j` (healthy) i `seq`; **brak kontenera `routing`**, nic `unhealthy` |
| Krok 6: `/health`, `/health/ready` | `Healthy`, `Healthy` |
| Krok 10: `pwsh infra/smoke-test.ps1 -BaseUrl http://localhost:8080` (dwa przebiegi) | 13 PASS, 0 FAIL, 0 SKIP, kod 0 |
| `Smoke/` (`FLOWBB_SMOKE_BASE_URL`, dwa przebiegi) | 74 PASS, 0 FAIL, 0 SKIP; stan bazy po przebiegach bez zmian (liczniki wydarzen i grup oraz luka powrotowa) |
| ReturnGap w PULSE | `1111` (koniec 21:30): 0, bez alertow; `3333` (koniec 23:15): 10 osob i alert `ReturnGap/Warning`; `4444`, `5555`: 0; `summary`: 10 |
| Luka a tryb transportu | uczestnik `PublicTransport` na `3333` dodaje 1 do luki, zmiana na `Bike` ja zdejmuje (test `Smoke/`) |
| Trasa `PublicTransport` dla `aaaa...` | `1111`: `returnGap: false`, 2 powroty; `3333`: `returnGap: true`, `returns` puste |
| `PulseUpdated` | klient huba dostaje `participantsWithoutReturn` zgodne z `GET /api/pulse/events/{id}` (test `Smoke/`) |
| OpenAPI i Scalar | `/scalar/v1` 200; `/openapi/v1.json`: 12 sciezek i 13 operacji (kontrakt ma 14: brakuje `getReadiness`) |
| Sprawdzenie negatywne straznika `Smoke/` | podmiana uzytkownika demo na uczestnika seedu konczy test bledem z instrukcja, a po restarcie API i przywroceniu seeda 74/74 |
| `dotnet test backend/FlowBB.sln` na Windows | 95 + 127 + 7 + 287 PASS, **1 FAIL**: `AirQualityContract_DefinesPlannedEndpointAndStableEnums` (nie dotyczy #91): test porownuje `contracts/openapi.yaml` z tekstem z `
`, a checkout na Windows ma `CRLF` (`core.autocrlf=true`); CI na Linuksie przechodzi. Naprawa: #131 |
| Neo4j Aura, przegladarka, proba z timerem | bez zmian: niewykonane w tym przebiegu (patrz sekcje 10-12) |
