# Runbook demo FlowBB

Instrukcja uruchomienia i przeprowadzenia krytycznego scenariusza demo (AGENTS.md, sekcja 2) oraz plan awaryjny.
**Status: 2026-09-20 (`develop` + #52).** Stos uruchamia sie od zera jedna komenda Compose, a `infra/smoke-test.ps1`
konczy sie wynikiem 13 PASS / 0 FAIL / 0 SKIP (szczegoly w sekcji 10). **Znany bloker scenariusza prezentacji:**
patrz sekcja 8 (seed nie ma uzytkownika, ktory moze dac `82 -> 83`).

## 1. Status krokow scenariusza

| # | Krok | Endpoint / element | Status |
|---|---|---|---|
| 1 | Uzytkownik otwiera wydarzenie w `/client` | `GET /api/events`, `GET /api/events/{id}` | dziala (smoke, lokalny Neo4j) |
| 2 | Klika "Ide" i wybiera srodek transportu | `POST /api/events/{id}/attendance` | dziala; **z seedem nie da sie pokazac `+1` uzytkownikiem klienta** (sekcja 8) |
| 3 | API zapisuje deklaracje w Neo4j | relacja `IS_GOING_TO` ze snapshotem | dziala (smoke, testy `FlowBB.Infrastructure.Tests`); na Aurze niepotwierdzone |
| 4 | Backend przelicza agregaty | logika PULSE w C# | dziala (smoke: PULSE zgodny z Attendance) |
| 5 | SignalR wysyla `PulseUpdated` | hub `/hubs/pulse` | negocjacja huba w smoke; komunikat sprawdzaja testy `AttendanceSignalRFlowTests` |
| 6 | Dashboard pokazuje licznik bez odswiezania (`82 -> 83`) | `/dashboard`, klient SignalR | backend gotowy; przebieg z dashboardem wymaga weryfikacji wzrokowej i uzytkownika spoza seedu (sekcja 8) |
| 7 | Uzytkownik widzi trase z `IRoutePlanner` | `GET /api/events/{id}/route?userId={userId}` | dziala (smoke: `plannerSource: Demo`, wynik powtarzalny) |
| 8 | Uzytkownik dolacza do mikrogrupy CREW | `GET groups`, `POST/DELETE members` | dziala (smoke: dolaczenie +1, ponowienie bez zmian, opuszczenie 204 x2) |
| 9 | Dashboard pokazuje popyt na mapie heksagonalnej | `GET /api/pulse/hexagons` | dziala (smoke: 4 komorki, wszystkie >= 10 osob, bez `userId`); na Aurze niepotwierdzone |

**Niezweryfikowane:** Neo4j Aura (brak dostepu), pelny przebieg SignalR z dashboardem (komunikat `PulseUpdated`
sprawdzono klientem SignalR z Node i testami integracyjnymi, bez przegladarki) oraz proba scenariusza z timerem.

## 2. Wymagania

- Docker (Compose v2) albo .NET SDK 10 do uruchomienia API lokalnie.
- PowerShell 7 (`pwsh`) do skryptu smoke testu.
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
**Uwaga (sekcja 8):** seed zapisuje wszystkich 82 uzytkownikow, w tym uzytkownika klienta
`aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`, na wydarzenie `11111111-...`, wiec kroki 3-4 nie pokaza `82 -> 83`, dopoki seed
nie dostanie uzytkownika spoza wydarzenia. Do sprawdzenia `+1` na zywo uzyj wydarzenia `33333333-...` (46 uczestnikow) i
uzytkownika `d1000000-0000-0000-0000-000000000082`, ktory na nim nie jest.

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
Domyslnie: Attendance na wydarzeniu `33333333-...` z uzytkownikiem `d1000000-...-000000000082` (nie ma tam deklaracji),
Crew na wydarzeniu `11111111-...` (jedyne z grupami w seedzie). Skrypt tworzy deklaracje i usuwa ja na koncu, o ile sam
ja utworzyl, wiec mozna go uruchamiac wielokrotnie. Brak grupy do dolaczenia jest FAIL, nie PASS.
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
- **Seed a scenariusz `82 -> 83` (blokuje prezentacje):** `flowbb-demo-seed.cypher` zapisuje wszystkich 82 uzytkownikow
  (w tym `aaaaaaaa-...` z klienta) na wydarzenie `11111111-...`, a uzytkownik `dddddddd-...` istnieje tylko w starym
  `database/flowbb-queries.cypher`. Klient klikajacy "Ide" dostaje `isNew: false` i licznik bez zmian.
  Wymaga decyzji Data/Neo4j (nowy uzytkownik demo spoza wydarzenia) i Frontend (`DEMO_USER_ID`); issue #72.
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

## 10. Wynik ostatniej weryfikacji (issue #57)

Data: 2026-09-20. Srodowisko: Windows 11, Docker 29.2.1 (Compose 5.0.2), lokalny Neo4j 5.26.30 Community (`--profile local-db`),
czyste srodowisko (bez woluminow, obrazy zbudowane od zera).

| Sprawdzenie | Wynik |
|---|---|
| `docker compose ... --env-file .env --profile local-db up --build -d` od zera | OK (ok. 20 s po zbudowanych obrazach); API czeka na zdrowy Neo4j |
| Seed przy starcie (`NEO4J_SEED_ON_STARTUP`) | OK po poprawce Compose (wczesniej zmienna nie docierala do kontenera, `GET /api/events` zwracal `[]`) |
| `/health` i `/health/ready` | `Healthy`; kontener `api` `healthy` |
| `infra/smoke-test.ps1` (dwa przebiegi z rzedu) | 13 PASS, 0 FAIL, 0 SKIP, kod 0 |
| Restart API (`docker restart`) | wraca `healthy`, seed idempotentny (`participantsCount` 220 bez zmian) |
| Kontener `routing` bez `routing-prepare` | `unhealthy` (brak grafow), API dziala z `DemoRoutePlanner` |
| Konfiguracja Compose bez profilu `local-db` (Aura) | `docker compose config` OK; brak testu z prawdziwa Aura (brak dostepu) |
| Scenariusz demo z timerem, dwa razy | **niewykonane** (wymaga czlowieka, `/client` i `/dashboard` w przegladarce) |
| `+1` na dashboardzie przez SignalR w przegladarce | **niewykonane**; blokuje je seed (sekcja 8) |

