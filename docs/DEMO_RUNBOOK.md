# Runbook demo FlowBB

Instrukcja uruchomienia i przeprowadzenia krytycznego scenariusza demo (AGENTS.md, sekcja 2) oraz plan awaryjny.
**Status: 2026-09-20 (`develop` + #72, ponownie po #58/#59).** Stos uruchamia sie od zera jedna komenda Compose, a `infra/smoke-test.ps1`
konczy sie wynikiem 13 PASS / 0 FAIL / 0 SKIP (szczegoly w sekcji 10, przebiegi na Windows i na Linuksie). Uzytkownik demo `aaaaaaaa-...` nie ma
poczatkowej deklaracji ani czlonkostwa w Crew, wiec pierwszy klik "Ide" pokazuje `82 -> 83`.

## 1. Status krokow scenariusza

| # | Krok | Endpoint / element | Status |
|---|---|---|---|
| 1 | Uzytkownik otwiera wydarzenie w `/client` | `GET /api/events`, `GET /api/events/{id}` | dziala (smoke, lokalny Neo4j) |
| 2 | Klika "Ide" i wybiera srodek transportu | `POST /api/events/{id}/attendance` | dziala; uzytkownik klienta `aaaaaaaa-...` dostaje `isNew: true` na wydarzeniu `1111...` |
| 3 | API zapisuje deklaracje w Neo4j | relacja `IS_GOING_TO` ze snapshotem | dziala (smoke, testy `FlowBB.Infrastructure.Tests`); na Aurze niepotwierdzone |
| 4 | Backend przelicza agregaty | logika PULSE w C# | dziala (smoke: PULSE zgodny z Attendance) |
| 5 | SignalR wysyla `PulseUpdated` | hub `/hubs/pulse` | negocjacja huba w smoke; komunikat sprawdzaja testy `AttendanceSignalRFlowTests` |
| 6 | Dashboard pokazuje licznik bez odswiezania (`82 -> 83`) | `/dashboard`, klient SignalR | backend i seed gotowe; przebieg z dashboardem wymaga weryfikacji wzrokowej |
| 7 | Uzytkownik widzi trase z `IRoutePlanner` | `GET /api/events/{id}/route?userId={userId}` | dziala (smoke: `plannerSource: Demo`, wynik powtarzalny) |
| 8 | Uzytkownik dolacza do mikrogrupy CREW | `GET groups`, `POST/DELETE members` | dziala (smoke: dolaczenie +1, ponowienie bez zmian, opuszczenie 204 x2) |
| 9 | Dashboard pokazuje popyt na mapie heksagonalnej | `GET /api/pulse/hexagons` | dziala (smoke: 4 komorki, wszystkie >= 10 osob, bez `userId`); na Aurze niepotwierdzone |

**Niezweryfikowane:** Neo4j Aura (brak dostepu), pelny przebieg SignalR z dashboardem (komunikat `PulseUpdated`
sprawdzono klientem SignalR z Node i testami integracyjnymi, bez przegladarki) oraz proba scenariusza z timerem.

## 2. Wymagania

- Docker (Compose v2) albo .NET SDK 10 do uruchomienia API lokalnie.
- PowerShell 7 (`pwsh`) do skryptu smoke testu. Na Linuksie: `dotnet tool install --global PowerShell` albo pakiet z repozytorium dystrybucji.
- Wolne miejsce na dysku: obrazy API i Neo4j oraz ich warstwy zajmuja kilka GB. Przy pelnym dysku Neo4j nie startuje (`No space left on device` w `docker logs`, kod wyjscia 70), a API startuje bez niego.
- Dla opcjonalnego realnego routingu drogowego: przygotowany wolumen
  `routing-data` (jednorazowa komenda w kroku 4 ponizej).
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
4. Opcjonalnie przygotuj realne grafy Walking/Bike/Car (ten reczny krok wymaga
   sieci i Overpass): `docker compose -f infra/docker-compose.yml --env-file .env --profile routing-tools run --rm routing-prepare`.
5. Uruchom stos (z lokalnym Neo4j; dla Aury pomin `--profile local-db`):
   `docker compose -f infra/docker-compose.yml --env-file .env --profile local-db up --build -d`.
   API czeka na zdrowy kontener Neo4j (ok. 30 s), a potem wykonuje seed.
   Kontener `routing` jest `unhealthy` bez kroku 4 (brak grafow); to nie blokuje API ani `DemoRoutePlanner`.
6. Sprawdz zdrowie API: `GET http://localhost:8080/health` powinno zwrocic `200 {"status":"Healthy","timestamp":"..."}` (`/health/ready` sprawdza dodatkowo Neo4j). Wewnetrzny `/health` kontenera `routing` ma status `ready` tylko po zaladowaniu wszystkich trzech grafow.
7. Otworz Scalar z OpenAPI (srodowisko Development): `http://localhost:8080/scalar`.
8. Uruchom smoke test (sekcja 5).

API mozna tez uruchomic recznie: `dotnet run --project backend/src/FlowBB.Api --urls http://localhost:8080`.
Glowny host udostepnia `/health`, `/health/ready`, `/hubs/pulse` oraz endpointy Events, Attendance, Crew, PULSE i Routing.
Zatrzymanie i pelny reset lokalnego stanu (wolumeny Neo4j, Seq, routing):
`docker compose -f infra/docker-compose.yml --env-file .env --profile local-db down -v`.

## 4. Docelowy przebieg prezentacji (10 minut)

Dane sa syntetyczne i oznaczone w UI jako `DEMO DATA / SYMULACJA`.
Seed utrzymuje 82 uczestnikow wydarzenia `11111111-...`, ale uzytkownik klienta
`aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` pozostaje poza wszystkimi wydarzeniami i grupami. Jest przeznaczony do pokazania
pierwszej deklaracji `isNew: true`, zmiany licznika `82 -> 83`, trasy oraz dolaczenia do Crew.

| Krok | Co robisz | Oczekiwany rezultat |
|---|---|---|
| 1 | Otworz `/dashboard` na duzym ekranie, wybierz wydarzenie "Koncert na Rynku" | KPI z aktualnym licznikiem, mapa heksagonow (komorki z co najmniej 10 osobami) |
| 2 | Na telefonie (lub mobilnym viewporcie) otworz `/client`, wybierz to samo wydarzenie | Lista i szczegoly wydarzenia |
| 3 | Kliknij "Ide", wybierz srodek transportu | Odpowiedz 200; `isNew: true` |
| 4 | Patrz na dashboard | Licznik zmienia sie o 1 (np. `82 -> 83`) bez odswiezania strony, a modal split odzwierciedla wybrany tryb |
| 5 | Kliknij "Ide" jeszcze raz (ten sam tryb) | Licznik **bez zmian** (idempotencja); zmiana trybu zmienia tylko modal split |
| 6 | Pokaz karte trasy (tam i z powrotem) | Trasa `plannerSource: Demo`, ta sama przy kazdym odswiezeniu |
| 7 | Dolacz do mikrogrupy Crew | Licznik czlonkow +1; ponowne dolaczenie nie zmienia licznika |
| 8 | Wroc na dashboard, pokaz mape | Zagregowany popyt na heksagonach; brak komorek ponizej 10 osob, brak identyfikatorow uzytkownikow |

Na koniec pokazu wykonaj sprzatanie (sekcja 6), zeby kolejne uruchomienie startowalo od tego samego stanu.

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
Domyslnie Attendance i Crew korzystaja z wydarzenia `11111111-...` oraz uzytkownika demo `aaaaaaaa-...`, ktory nie ma
poczatkowej deklaracji ani czlonkostwa w grupie. Skrypt tworzy deklaracje i usuwa ja na koncu, o ile sam ja utworzyl,
wiec mozna go uruchamiac wielokrotnie. Brak grupy do dolaczenia jest FAIL, nie PASS.
Sam skrypt smoke nadal zawiera historyczny fallback do POST przy trasie (sekcja 8).

**Czego skrypt nie sprawdza:** samego komunikatu SignalR (tylko negocjacje huba; komunikat pokrywaja testy integracyjne
`AttendanceSignalRFlowTests`), ani zachowania frontendu. Krok 4 scenariusza sprawdzaj wzrokowo na dashboardzie.

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
| Usluga routingu niedostepna lub graf niezaladowany | Health uslugi nie jest ready albo ASP.NET przekracza timeout | W trybie demo uzyj jawnie skonfigurowanego `DemoRoutePlanner`; nie ukrywaj w ten sposob blednego trybu, nieprawidlowych danych ani rzeczywistego `route_not_found` |
| SignalR nie laczy sie | Licznik nie zmienia sie na zywo | Odswiez dashboard (odpowiedz REST zawiera aktualny licznik); sprawdz CORS i adres API w `.env` |
| Telefon nie widzi API | `/client` bez danych | Uzyj mobilnego viewportu w przegladarce na laptopie; awaryjnie tunel `cloudflared` do API |
| Mapa pusta | Brak komorek na `/api/pulse/hexagons` | Za malo osob w jednej komorce (prog 10): dosiej dane demo lub zmniejsz rozmiar siatki (obecnie 900 m) - to decyzja Core Ownera |
| Test smoke FAIL na demo | Skrypt konczy sie kodem 1 | Nie prezentuj kroku, ktory zawiodl; napraw przed pokazem, backup nagrania jako zapas |

Backup: nagraj przebieg scenariusza (sekcja 4) i zapisz zrzuty ekranu dashboardu przed prezentacja. Po feature freeze wykonaj dwie proby z timerem.

## 8. Znane zalozenia i ograniczenia MVP

- `participantsWithoutReturn` zawsze 0, a lista alertow pusta: logika powrotow nie istnieje.
- Siatka heksagonow: rozmiar 900 m, lokalny rzut metryczny wokol Rynku (nie EPSG:2180); komorki `count < 10` nie sa zwracane.
- `GET /api/pulse/summary` odpytuje wydarzenia po kolei (N+1); przy dziesiatkach wydarzen jest to wystarczajace dla demo.
- Routing: kanoniczny kontrakt to
  `GET /api/events/{eventId}/route?userId={userId}`. Punkt startu i tryb maja
  pochodzic ze snapshotu Attendance, a nie z body zadania. `IRoutePlanner` i
  deterministyczny `DemoRoutePlanner` sa zaimplementowane i podlaczone.
  Dane MZK sa niekompletne i nie stanowia grafu routingu. Proponowany realny
  routing Walking/Bike/Car zostal zaimplementowany jako prywatna usluga
  Python/FastAPI z lokalnymi grafami OSM oraz wewnetrzny klient ASP.NET;
  przegladarka nigdy nie wywoluje FastAPI bezposrednio. Realny planer nie jest
  jeszcze aktywnym `IRoutePlanner`, poniewaz zaakceptowany kontrakt nie ma
  prawdziwego zrodla, dystansu ani geometrii. Szczegoly opisuje
  `docs/ROUTING_SERVICE.md`.
  Geometria i dystans pozostaja wyraznie niezaakceptowana zmiana publicznego
  kontraktu. PublicTransport nie jest obslugiwany przez te usluge drogowa.
- PostgreSQL/PostGIS w `data/gtfs/mzk/` to odseparowany PoC, nie baza aplikacji (patrz `docs/adr/001-runtime-persistence.md`).
- Relacja `IS_GOING_TO` przechowuje `TransportMode`, `OriginLatitude`,
  `OriginLongitude` i `UpdatedAt`. Modal split i mapa PULSE sa wyliczane w C#
  ze snapshotow odczytanych z Neo4j.
- Seed ma 83 syntetycznych uzytkownikow. Uczestnicy wydarzenia o liczbie `N` to uzytkownicy `d100...002` do
  `d100...N+1`, dlatego liczniki pozostaja `82/46/28/64`, a `DEMO_USER_ID` `aaaaaaaa-...` jest wolny do scenariusza
  `82 -> 83`. Ponowne wykonanie seedu usuwa jego deklaracje i czlonkostwo w seedowanych grupach (#72).
- **Historyczna uwaga:** wczesniejszy branch kliencki eksperymentowal z POST
  i punktem startu w body. Nie jest to aktualny kontrakt `develop`.
- `infra/smoke-test.ps1` nadal zawiera zgodnosciowy fallback do historycznego
  POST. Skrypt wymaga osobnego zadania kodowego; ten fallback nie jest kontraktem.

## 9. Kryteria gotowosci demo

- [x] projekt uruchamia sie od czystego srodowiska (sekcja 3; zweryfikowane 2026-09-20),
- [x] Neo4j startuje i przechodzi health check (lokalny kontener),
- [x] seed jest idempotentny (restart API, smoke dwukrotnie),
- [x] Events dziala,
- [x] Attendance dziala idempotentnie,
- [ ] SignalR publikuje aktualizacje (dashboard pokazuje `+1` bez odswiezania),
- [x] DemoRoutePlanner zwraca deterministyczna trase,
- [x] Crew dziala (smoke),
- [x] PULSE nie ujawnia danych dla `count < 10` (smoke i testy),
- [ ] Scalar prezentuje aktualne OpenAPI,
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
