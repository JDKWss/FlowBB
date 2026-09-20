# Runbook demo FlowBB

Instrukcja uruchomienia i przeprowadzenia krytycznego scenariusza demo (AGENTS.md, sekcja 2) oraz plan awaryjny.
**Status: 2026-09-20 (`develop`).** Nie wszystkie kroki da sie jeszcze wykonac.
Kod modulu moze istniec i miec testy, ale dopoki nie jest zarejestrowany oraz
zmapowany w `Program.cs`, nie jest dostepny w uruchomionym API.

## 1. Status krokow scenariusza

| # | Krok | Endpoint / element | Status |
|---|---|---|---|
| 1 | Uzytkownik otwiera wydarzenie w `/client` | `GET /api/events`, `GET /api/events/{id}` | endpointy, adapter Neo4j i mapowanie sa gotowe |
| 2 | Klika "Ide" i wybiera srodek transportu | `POST /api/events/{id}/attendance` | endpoint, adapter Neo4j i mapowanie sa gotowe |
| 3 | API zapisuje deklaracje w Neo4j | relacja `IS_GOING_TO` ze snapshotem | zapis pelnego snapshotu zostal zweryfikowany na Aura |
| 4 | Backend przelicza agregaty | logika PULSE w C# | handlery i `IPulseDataReader` sa zarejestrowane |
| 5 | SignalR wysyla `PulseUpdated` | hub `/hubs/pulse` | hub i publikacja po zatwierdzeniu Attendance sa podlaczone |
| 6 | Dashboard pokazuje licznik bez odswiezania (`82 -> 83`) | `/dashboard`, klient SignalR | backend jest gotowy; pelny przebieg z dashboardem wymaga weryfikacji wzrokowej |
| 7 | Uzytkownik widzi trase z `IRoutePlanner` | `GET /api/events/{id}/route?userId={userId}` | endpoint i `DemoRoutePlanner` dzialaja ze snapshotem Neo4j |
| 8 | Uzytkownik dolacza do mikrogrupy CREW | `GET groups`, `POST/DELETE members` | kontrakt i ogolny model grafu istnieja; brak modulu Application/API |
| 9 | Dashboard pokazuje popyt na mapie heksagonalnej | `GET /api/pulse/hexagons` | endpoint i agregacja na danych Aura zostaly zweryfikowane |

Na prawdziwej instancji Aura zweryfikowano Events, idempotentny i rownolegly
zapis Attendance, PULSE, heksagony oraz Routing. **Niezweryfikowane:** Crew
i pelny przebieg SignalR z frontendem.

## 2. Wymagania

- Docker (Compose v2) albo .NET SDK 10 do uruchomienia API lokalnie.
- PowerShell 7 (`pwsh`) do skryptu smoke testu.
- Neo4j: instancja Aura (patrz `backend/README.md`) albo lokalny kontener (profil `local-db`).
- Przegladarka desktopowa dla `/dashboard`, przegladarka w mobilnym viewporcie dla `/client`.
- Zadnych sekretow w repozytorium: hasla i dane polaczenia tylko w `.env` (ignorowany przez git), wzor w `.env.example`.

## 3. Uruchomienie od czystego srodowiska

1. Sklonuj repozytorium i przejdz na `develop`: `git clone https://github.com/JDKWss/FlowBB.git` i `git switch develop`.
2. Skopiuj `.env.example` do `.env` i uzupelnij `NEO4J_URI`, `NEO4J_DATABASE`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`.
3. Ustaw `NEO4J_SEED_ON_STARTUP=true`, aby backend przed startem automatycznie
   wykonal constraints i `database/flowbb-demo-seed.cypher`. Lokalne profile
   `dotnet run` maja te opcje wlaczona. Seed jest idempotentny.
4. Uruchom stos: `docker compose -f infra/docker-compose.yml up --build` (dodaj `--profile local-db` dla lokalnego Neo4j).
5. Sprawdz zdrowie API: `GET http://localhost:8080/health` powinno zwrocic `200 {"status":"ok"}`.
6. Otworz Scalar z OpenAPI (srodowisko Development): `http://localhost:8080/scalar`.
7. Uruchom smoke test (sekcja 5).

API mozna tez uruchomic recznie: `dotnet run --project backend/src/FlowBB.Api --urls http://localhost:8080`.
Glowny host udostepnia `/health`, `/hubs/pulse` oraz endpointy Events,
Attendance, PULSE i Routing.

## 4. Docelowy przebieg prezentacji (10 minut)

Dane sa syntetyczne i oznaczone w UI jako `DEMO DATA / SYMULACJA`. Uzytkownik demo: `dddddddd-dddd-dddd-dddd-dddddddddddd`
(nie deklaruje udzialu w wydarzeniu `11111111-1111-1111-1111-111111111111` przed pokazem).
Ten scenariusz jest celem P0; tabela statusowa w sekcji 1 wskazuje kroki,
ktorych aktualny glowny host jeszcze nie obsluguje.

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

Integracje Events, Attendance, PULSE i Routing zweryfikowano bezposrednio
na glownym hoscie polaczonym z Aura. Sam skrypt smoke nadal zawiera historyczny
fallback opisany w sekcji 8.

**Czego skrypt nie sprawdza:** samego komunikatu SignalR (tylko negocjacje huba; komunikat pokrywaja testy integracyjne
`AttendanceSignalRFlowTests`), ani zachowania frontendu. Krok 4 scenariusza sprawdzaj wzrokowo na dashboardzie.

## 6. Sprzatanie i reset stanu

- Skrypt smoke testu usuwa swoja deklaracje (`DELETE attendance` jest idempotentne).
- Po pokazie usun deklaracje uzytkownika demo: `DELETE /api/events/{eventId}/attendance/{userId}` (204 nawet gdy jej nie ma)
  oraz opusc mikrogrupe: `DELETE /api/groups/{groupId}/members/{userId}`.
- Pelny reset danych demonstracyjnych: uruchom backend z
  `NEO4J_SEED_ON_STARTUP=true`.

## 7. Plan awaryjny

| Awaria | Objaw | Co robisz |
|---|---|---|
| Neo4j niedostepne | `/health` 200, ale Attendance/PULSE zwracaja 500 | Przelacz na lokalny kontener (`--profile local-db`) i zaladuj seed ponownie; ostatecznie pokaz nagranie z backupu |
| Brak internetu | Aura nieosiagalna | Lokalny kontener Neo4j; po wdrozeniu routing ma korzystac z offline'owego `DemoRoutePlanner`, bez OTP |
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
  Dane MZK sa niekompletne i nie stanowia grafu routingu.
- PostgreSQL/PostGIS w `data/gtfs/mzk/` to odseparowany PoC, nie baza aplikacji (patrz `docs/adr/001-runtime-persistence.md`).
- Relacja `IS_GOING_TO` przechowuje `TransportMode`, `OriginLatitude`,
  `OriginLongitude` i `UpdatedAt`. Modal split i mapa PULSE sa wyliczane w C#
  ze snapshotow odczytanych z Neo4j.
- **Historyczna uwaga:** wczesniejszy branch kliencki eksperymentowal z POST
  i punktem startu w body. Nie jest to aktualny kontrakt `develop`.
- `infra/smoke-test.ps1` nadal zawiera zgodnosciowy fallback do historycznego
  POST. Skrypt wymaga osobnego zadania kodowego; ten fallback nie jest kontraktem.

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
