# Runbook demo FlowBB

Instrukcja uruchomienia i przeprowadzenia krytycznego scenariusza demo (AGENTS.md, sekcja 2) oraz plan awaryjny.
Dokument powstaje razem z issue #22. **Stan na dzien utworzenia:** nie wszystkie kroki da sie jeszcze wykonac, bo brakuje modulow
opisanych w `docs/MVP_WORK_PLAN.md`. Kazdy krok ponizej ma oznaczony status; sekcje ze statusem "PO #N" trzeba zweryfikowac,
gdy dana czesc trafi na `develop`.

## 1. Status krokow scenariusza

| # | Krok | Endpoint / element | Status |
|---|---|---|---|
| 1 | Uzytkownik otwiera wydarzenie w `/client` | `GET /api/events`, `GET /api/events/{id}` | PO #2, #9, #11 (Events) i #16 (adapter) |
| 2 | Klika "Ide" i wybiera srodek transportu | `POST /api/events/{id}/attendance` | endpoint gotowy (#14); zapis do Neo4j PO #17 |
| 3 | API zapisuje deklaracje w Neo4j | relacja `IS_GOING_TO` | PO #17 (adapter Attendance) |
| 4 | Backend przelicza agregaty | logika PULSE (#5) | gotowe; dane z Neo4j PO #15 |
| 5 | SignalR wysyla `PulseUpdated` | hub `/hubs/pulse` | gotowe (#19); wlaczenie w `Program.cs` PO #21 |
| 6 | Dashboard pokazuje licznik bez odswiezania (`82 -> 83`) | `/dashboard`, klient SignalR | frontend; backend gotowy, podpiecie PO #21 |
| 7 | Uzytkownik widzi trase z `IRoutePlanner` | `GET`/`POST /api/events/{id}/route` | PO #4, #13 |
| 8 | Uzytkownik dolacza do mikrogrupy CREW | `GET groups`, `POST/DELETE members` | PO #3, #12 i #18 |
| 9 | Dashboard pokazuje popyt na mapie heksagonalnej | `GET /api/pulse/hexagons` | endpoint gotowy (#20); dane PO #15 |

Zweryfikowane do tej pory: endpointy Attendance i PULSE, hub SignalR oraz przeplyw "POST -> komunikat SignalR" (testy integracyjne
na fake'u persystencji). **Niezweryfikowane:** polaczenie z prawdziwym Neo4j, Events, Crew, trasa i pelny przebieg z frontendem.

## 2. Wymagania

- Docker (Compose v2) albo .NET SDK 10 do uruchomienia API lokalnie.
- PowerShell 7 (`pwsh`) do skryptu smoke testu.
- Neo4j: instancja Aura (plik z danymi w `backend/`, patrz `backend/README.md`) albo lokalny kontener (profil `local-db`, PO #21).
- Przegladarka desktopowa dla `/dashboard`, przegladarka w mobilnym viewporcie dla `/client`.
- Zadnych sekretow w repozytorium: hasla i dane polaczenia tylko w `.env` (ignorowany przez git), wzor w `.env.example` (PO #21).

## 3. Uruchomienie od czystego srodowiska

Kroki oznaczone (PO #21) wymagaja plikow z issue #21 (Dockerfile, Compose, `.env.example`).

1. Sklonuj repozytorium i przejdz na `develop`: `git clone https://github.com/JDKWss/FlowBB.git` i `git switch develop`.
2. (PO #21) Skopiuj `.env.example` do `.env` i uzupelnij `NEO4J_URI`, `NEO4J_DATABASE`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`.
3. Zaladuj schemat i seed do Neo4j: uruchom kolejno bloki z `database/flowbb-queries.cypher` w Neo4j Query (kazdy blok osobno,
   od pierwszego slowa do srednikow, jak opisuje naglowek pliku). Skrypt jest idempotentny; ponowne uruchomienie nie duplikuje danych.
4. (PO #21) Uruchom stos: `docker compose -f infra/docker-compose.yml up --build` (dodaj `--profile local-db` dla lokalnego Neo4j).
5. Sprawdz zdrowie API: `GET http://localhost:8080/health` powinno zwrocic `200 {"status":"ok"}`.
6. Otworz Scalar z OpenAPI (srodowisko Development): `http://localhost:8080/scalar`.
7. Uruchom smoke test (sekcja 5).

Do czasu #21 API mozna uruchomic recznie: `dotnet run --project backend/src/FlowBB.Api --urls http://localhost:8080`.
Dzis odpowiada wtedy tylko `/health`.

## 4. Przebieg prezentacji (10 minut)

Dane sa syntetyczne i oznaczone w UI jako `DEMO DATA / SYMULACJA`. Uzytkownik demo: `dddddddd-dddd-dddd-dddd-dddddddddddd`
(nie deklaruje udzialu w wydarzeniu `11111111-1111-1111-1111-111111111111` przed pokazem).

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

Opcje: `-EventId`, `-UserId`, `-TransportMode`, `-KeepData` (nie sprzataj po tescie).
Skrypt tworzy deklaracje tylko dla uzytkownika demo i usuwa ja na koncu, o ile sam ja utworzyl.

**Weryfikacja skryptu:** uruchomiony na tymczasowym hoscie z prawdziwymi endpointami Attendance, PULSE i hubem SignalR (dane w pamieci
zamiast Neo4j): 10 PASS, 0 FAIL, 3 SKIP (Events, trasa, Crew). Kroki Events, trasa i Crew nie byly weryfikowane, bo tych endpointow
nie ma jeszcze na `develop`; ich asercje trzeba potwierdzic, gdy sie pojawia.

**Czego skrypt nie sprawdza:** samego komunikatu SignalR (tylko negocjacje huba; komunikat pokrywaja testy integracyjne
`AttendanceSignalRFlowTests`), ani zachowania frontendu. Krok 4 scenariusza sprawdzaj wzrokowo na dashboardzie.

## 6. Sprzatanie i reset stanu

- Skrypt smoke testu usuwa swoja deklaracje (`DELETE attendance` jest idempotentne).
- Po pokazie usun deklaracje uzytkownika demo: `DELETE /api/events/{eventId}/attendance/{userId}` (204 nawet gdy jej nie ma)
  oraz opusc mikrogrupe: `DELETE /api/groups/{groupId}/members/{userId}`.
- Pelny reset danych: ponownie uruchom `database/flowbb-queries.cypher` (idempotentny `MERGE`). Uwaga: skrypt nie usuwa deklaracji dodanych recznie.

## 7. Plan awaryjny

| Awaria | Objaw | Co robisz |
|---|---|---|
| Neo4j niedostepne | `/health` 200, ale Attendance/PULSE zwracaja 500 | Przelacz na lokalny kontener (`--profile local-db`, PO #21) i zaladuj seed ponownie; ostatecznie pokaz nagranie z backupu |
| Brak internetu | Aura nieosiagalna | Lokalny kontener Neo4j; `DemoRoutePlanner` dziala bez sieci i bez OTP |
| SignalR nie laczy sie | Licznik nie zmienia sie na zywo | Odswiez dashboard (odpowiedz REST zawiera aktualny licznik); sprawdz CORS i adres API w `.env` |
| Telefon nie widzi API | `/client` bez danych | Uzyj mobilnego viewportu w przegladarce na laptopie; awaryjnie tunel `cloudflared` do API |
| Mapa pusta | Brak komorek na `/api/pulse/hexagons` | Za malo osob w jednej komorce (prog 10): dosiej dane demo lub zmniejsz rozmiar siatki (obecnie 900 m) - to decyzja Core Ownera |
| Test smoke FAIL na demo | Skrypt konczy sie kodem 1 | Nie prezentuj kroku, ktory zawiodl; napraw przed pokazem, backup nagrania jako zapas |

Backup: nagraj przebieg scenariusza (sekcja 4) i zapisz zrzuty ekranu dashboardu przed prezentacja. Po feature freeze wykonaj dwie proby z timerem.

## 8. Znane zalozenia i ograniczenia MVP

- `participantsWithoutReturn` zawsze 0, a lista alertow pusta: logika powrotow nie istnieje.
- Siatka heksagonow: rozmiar 900 m, lokalny rzut metryczny wokol Rynku (nie EPSG:2180); komorki `count < 10` nie sa zwracane.
- `GET /api/pulse/summary` odpytuje wydarzenia po kolei (N+1); przy dziesiatkach wydarzen jest to wystarczajace dla demo.
- Routing: deterministyczny `DemoRoutePlanner`. Dane MZK sa niekompletne (brak pelnych trips, kolejnosci przystankow, powiazania kursow
  i wspolrzednych) i nie stanowia systemu routingu.
- PostgreSQL/PostGIS w `data/gtfs/mzk/` to odseparowany PoC, nie baza aplikacji (patrz `docs/adr/001-runtime-persistence.md`).
- **Otwarta decyzja:** czy relacja `IS_GOING_TO` przechowuje `TransportMode` i `UpdatedAt` (wymagane przez OpenAPI, ale wykluczone w obecnej
  wersji `docs/NEO4J_CONTRACT.md`). Do jej rozstrzygniecia modal split i mapa PULSE nie beda mialy danych z Neo4j.
- Kontrakt trasy: `develop` ma `GET ...?userId=`, a `develop-client` `POST` z `origin`. Skrypt smoke testu probuje obu wariantow.

## 9. Kryteria gotowosci demo

- [ ] projekt uruchamia sie od czystego srodowiska (sekcja 3),
- [ ] Neo4j startuje i przechodzi health check,
- [ ] seed jest idempotentny,
- [ ] Events dziala,
- [ ] Attendance dziala idempotentnie,
- [ ] SignalR publikuje aktualizacje (dashboard pokazuje `+1` bez odswiezania),
- [ ] DemoRoutePlanner zwraca deterministyczna trase,
- [ ] Crew dziala (jesli nalezy do zatwierdzonego MVP),
- [ ] PULSE nie ujawnia danych dla `count < 10`,
- [ ] Scalar prezentuje aktualne OpenAPI,
- [ ] `dotnet build` i `dotnet test` przechodza, a `infra/smoke-test.ps1` konczy sie bez FAIL i bez SKIP,
- [ ] scenariusz demo zostal przecwiczony dwa razy z timerem.
