# Baza Neo4j: schemat i seed

Kontrakt danych: [../docs/NEO4J_CONTRACT.md](../docs/NEO4J_CONTRACT.md). Decyzja o bazie: [../docs/adr/001-runtime-persistence.md](../docs/adr/001-runtime-persistence.md).

| Plik / katalog          | Zawartosc                                                        | Idempotentny                        |
| ----------------------- | ---------------------------------------------------------------- | ----------------------------------- |
| `migrations/*.cypher`   | numerowane migracje constraints i indeksow                       | tak (`IF NOT EXISTS` + znacznik)    |
| `schema.cypher`         | zgodny wstecznie snapshot aktualnej wersji schematu              | tak; nie dodawaj tu nowych migracji |
| `flowbb-queries.cypher` | syntetyczny seed (`DEMO DATA / SYMULACJA`) i zapytania kontrolne | tak (`MERGE` + `SET`)               |

Migracje stosuje sie w kolejnosci numerow. Kazda konczy sie aktualizacja pojedynczego wezla `(:SchemaVersion {Key: 'flowbb'})`. Initializer odczytuje ten znacznik, uruchamia tylko brakujace migracje i odmawia startu, gdy baza ma wersje nowsza niz aplikacja. Skrypt mozna bezpiecznie uruchomic ponownie: constraints, indeksy, wersja i `AppliedAt` nie zmieniaja sie. `schema.cypher` pozostaje tylko dla starszych instrukcji i odpowiada wersji 2.

## Uruchomienie na lokalnym kontenerze

Przy kontenerze `neo4j` z profilu `local-db` (`infra/docker-compose.yml`), z katalogu glownego repozytorium i z wczytanym `.env`:

```bash
docker compose --profile local-db exec -T neo4j \
  cypher-shell -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" < database/migrations/001_constraints.cypher
docker compose --profile local-db exec -T neo4j \
  cypher-shell -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" < database/migrations/002_event_start_at_index.cypher
docker compose --profile local-db exec -T neo4j \
  cypher-shell -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" < database/flowbb-queries.cypher
```

Wynik kontrolny: `SHOW CONSTRAINTS` pokazuje 9 constraintow `UNIQUENESS`, a `MATCH (version:SchemaVersion {Key: 'flowbb'}) RETURN version.Version, version.Name` zwraca `2` i `002_event_start_at_index`. Wynik seedu obejmuje dodatkowy wezel znacznika schematu.

## Backup i restore Neo4j Community

`neo4j-admin database dump` oraz `neo4j-admin database load` wymagaja zatrzymanej bazy. Nie wykonuj ich wewnatrz dzialajacego procesu Neo4j. Ponizszy przyklad zaklada, ze kontener ma podmontowany katalog `/backups`; kontener pomocniczy dziedziczy jego wolumeny, ale nie uruchamia serwera:

```powershell
docker stop <nazwa-kontenera>
docker run --rm --volumes-from <nazwa-kontenera> neo4j:5.26.30-community `
  neo4j-admin database dump neo4j --to-path=/backups --overwrite-destination=true

# load nadpisuje pliki bazy; nadal musi byc zatrzymana
docker run --rm --volumes-from <nazwa-kontenera> neo4j:5.26.30-community `
  neo4j-admin database load neo4j --from-path=/backups --overwrite-destination=true
docker start <nazwa-kontenera>
```

Automatyczny test [test-backup-restore.ps1](test-backup-restore.ps1) sam tworzy jednorazowy kontener i tymczasowy katalog backupu, stosuje migracje oraz seed, wykonuje dump, uruchamia baze tylko po to, aby usunac wszystkie wezly, ponownie ja zatrzymuje i wykonuje load. Po restore porownuje liczby wezlow, relacji oraz `SchemaVersion`, uruchamia wszystkie `FlowBB.Infrastructure.Tests` i ponownie sprawdza snapshot. Na koncu usuwa kontener, anonimowy wolumen i dump.

Skrypt odmawia pracy bez jawnego potwierdzenia bazy jednorazowej. Nie kieruj go do Aury ani bazy aplikacji:

```powershell
$env:FLOWBB_NEO4J_TEST_PASSWORD = '<lokalne-haslo-jednorazowe>'
$env:FLOWBB_NEO4J_TEST_CONFIRM_DISPOSABLE = 'true'
pwsh database/test-backup-restore.ps1
```

Wynik z 2026-09-20 na Neo4j `5.26.30-community`: przed dumpem, po restore i po testach adapterow uzyskano identyczny snapshot `100` wezlow, `411` relacji, wersja schematu `2` (`002_event_start_at_index`). Testy adapterow: `72 passed`, `0 failed`, `0 skipped`.

## Reczna weryfikacja na jednorazowej instancji Neo4j Aura

> **Status: procedura nie zostala wykonana w ramach #114, poniewaz nie udostepniono danych dostepowych do bezpiecznej, pustej instancji Aura.**

`Neo4jTestSafetyGuard` celowo blokuje testy adapterow na hostach Aura. Nie omijaj go i nie ustawiaj `FLOWBB_NEO4J_TEST_*` na baze Aura. Ponizsza procedura jest wylacznie recznym smoke testem na jednorazowej, pustej instancji, ktora po weryfikacji mozna usunac.

1. Utworz pusta instancje Aura. Dane dostepowe ustaw tylko w lokalnych zmiennych `NEO4J_URI`, `NEO4J_DATABASE`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`; nie wklejaj ich do logow, plikow ani PR. URI z konsoli Aura ma zwykle schemat `neo4j+s://`.
2. Z lokalnego komputera zastosuj kolejno migracje i seed:

   ```bash
   cypher-shell -a "$NEO4J_URI" -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" -d "$NEO4J_DATABASE" -f database/migrations/001_constraints.cypher
   cypher-shell -a "$NEO4J_URI" -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" -d "$NEO4J_DATABASE" -f database/migrations/002_event_start_at_index.cypher
   cypher-shell -a "$NEO4J_URI" -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" -d "$NEO4J_DATABASE" -f database/flowbb-demo-seed.cypher
   ```

3. W Aura Query sprawdz `MATCH (version:SchemaVersion {Key: 'flowbb'}) RETURN version.Version, version.Name`; oczekiwany wynik to wersja `2`. Sprawdz tez `SHOW CONSTRAINTS` i `SHOW INDEXES`.
4. Uruchom API z tymi samymi zmiennymi `NEO4J_*` i `NEO4J_SEED_ON_STARTUP=true`. Ponowne zastosowanie migracji oraz seedu potwierdza idempotencje.
5. Gdy `/health` odpowiada, uruchom `pwsh infra/smoke-test.ps1` i zachowaj tylko wynik PASS/FAIL, bez konfiguracji polaczenia.
6. Po tescie usun jednorazowa instancje Aura i wyczysc lokalne zmienne srodowiskowe.

## Ograniczenia edycji Community

- Schemat uzywa wylacznie constraintow unikalnosci i indeksow zakresu. Constraint istnienia (`IS NOT NULL`) i klucz wezla wymagaja edycji Enterprise (sprawdzone na Neo4j 5.26 Community: `Property existence constraint requires Neo4j Enterprise Edition`), wiec nie sa czescia schematu.
- Konsekwencja: baza przyjmie wezel `Event` bez `Name`. Kompletnosc pol wymaganych pilnuja adaptery w `Infrastructure/Neo4j`, a nie schemat. Unikalnosc `EventId`, `UserId`, `VenueId`, `CrewId` jest wymuszana przez baze (duplikat konczy sie bledem).
- Edycja Community obsluguje jedna baze uzytkownika, o nazwie `neo4j` (`CREATE DATABASE` jest tam nieobslugiwane). Gdy `NEO4J_DATABASE` nie jest ustawione, backend uzywa wlasnie `neo4j`; dla Aury podaj nazwe bazy z konsoli.
- Unikalnosci relacji `IS_GOING_TO` nie wymusza constraint: ma ja gwarantowac `MERGE` na parze wezlow. Do potwierdzenia testem rownoleglych zapisow na prawdziwej instancji w issue #17.
- Aura jest usluga zarzadzana, zwykle wymaga szyfrowanego `neo4j+s://`, nie udostepnia powloki kontenera i moze wybrac inny fizyczny operator planu wraz ze wzrostem statystyk. Uzyte DDL (`IF NOT EXISTS`, uniqueness constraints i range index) jest wspolne dla Community i Aura, ale procedury Aura powyzej nie wykonano.

## Przeglad indeksow PULSE, Attendance i Crew (#114)

Plany `EXPLAIN` i `PROFILE` sprawdzono na Neo4j 5.26.30 Community. Odczyt PULSE pojedynczego wydarzenia po `EventId` korzysta z `NodeUniqueIndexSeek` na `event_id_unique`, a potem przechodzi relacje `IS_GOING_TO` przez `Expand(All)`. Zbiorczy odczyt summary celowo obejmuje wszystkie wydarzenia: plan uzywa `NodeByLabelScan` dla `Event`, a nastepnie `OptionalExpand(All)` po `IS_GOING_TO`.

Attendance szuka `User.UserId` i `Event.EventId` przez constraint-backed `NodeUniqueIndexSeek`; pozniejsze `MERGE` dotyczy juz znalezionej pary wezlow. Odczyt agregatu wydarzenia zaczyna sie od `event_id_unique`. Crew analogicznie korzysta z `crew_id_unique`, `user_id_unique` i `event_id_unique` dla dolaczenia, opuszczenia oraz listy grup wydarzenia, a czlonkow przechodzi przez relacje `MEMBER_OF`.

Nie dodano nowego indeksu biznesowego. Zbiorczy odczyt PULSE nie ma selektywnego predykatu, a przejscia Attendance/Crew sa po relacjach od wezlow znalezionych przez unikalne identyfikatory. Indeksy na `TransportMode`, wspolrzednych relacji albo `MEMBER_OF` nie ograniczylyby liczby odczytow. Dodano jedynie constraint `schema_version_key_unique`, ktory gwarantuje pojedynczy znacznik wersji dla klucza `flowbb`.

## Migracja z poprzedniego seedu

Seed przenosi dane ze starszych wersji: ustawia `DefaultOriginLatitude`/`DefaultOriginLongitude` i usuwa `HomeLatitude`/`HomeLongitude` oraz `DemoData`. Ponowne uruchomienie na bazie po starszym seedzie nie wymaga czyszczenia danych. Snapshot `IS_GOING_TO` (`TransportMode`, `OriginLatitude`, `OriginLongitude`, `UpdatedAt`) i `MEMBER_OF.JoinedAt` sa uzupelniane na istniejacych relacjach.

## Testy adapterow na prawdziwym Neo4j

Testy w `backend/tests/FlowBB.Infrastructure.Tests` lacza sie z prawdziwa instancja, ustawiana zmiennymi `FLOWBB_NEO4J_TEST_URI`, `FLOWBB_NEO4J_TEST_PASSWORD` (oraz opcjonalnie `..._USERNAME` i `..._DATABASE`, domyslnie `neo4j`). Sa to celowo inne zmienne niz `NEO4J_*`, zeby testy nie trafily przypadkiem w baze aplikacji. Testy zapisuja i usuwaja dane, dlatego wymagaja tez jawnego `FLOWBB_NEO4J_TEST_CONFIRM_DISPOSABLE=true`. Fixture odmawia pracy z Neo4j Aura nawet przy takim potwierdzeniu. Wskazuj wylacznie jednorazowa instancje; fixture stosuje te same osadzone migracje co aplikacja, a dane testowe usuwa po przebiegu.

```bash
docker run -d --name flowbb-neo4j-test -p 127.0.0.1:17687:7687 \
  -e NEO4J_AUTH=neo4j/<haslo> neo4j:5.26.30-community
export FLOWBB_NEO4J_TEST_URI=neo4j://127.0.0.1:17687 FLOWBB_NEO4J_TEST_PASSWORD=<haslo>
export FLOWBB_NEO4J_TEST_CONFIRM_DISPOSABLE=true
dotnet test backend/FlowBB.sln
```

Bez URI i hasla testy adapterow sa **pomijane (Skipped)**, a nie zaliczane. Gdy URI i haslo sa ustawione, ale brakuje potwierdzenia jednorazowej bazy, testy koncza sie bledem przed utworzeniem polaczenia i pierwszym zapisem. Zielony `dotnet test` bez bazy nie dowodzi, ze adapter dziala: sprawdz w wyniku, ze testy `FlowBB.Infrastructure.Tests` nie sa pominiete.

W CI robi to `.github/workflows/neo4j-integration.yml`: usluga Neo4j 5.26 Community tworzona na czas przebiegu, `FLOWBB_NEO4J_TEST_CONFIRM_DISPOSABLE=true` tylko w tym jobie oraz krok, ktory konczy job bledem, gdy jakikolwiek test zostal pominiety (Skipped).

### Test obciazeniowy Attendance i PULSE

`Neo4jPulseLoadTests` uruchamia na prawdziwym Neo4j 89 syntetycznych uzytkownikow i 4 syntetyczne wydarzenia. Wykonuje 89 rownoleglych zapisow Attendance, 40 odczytow PULSE (`summary` i `hexagons`) podczas zapisow, a nastepnie ponawia wszystkie 89 zapisow. Maksymalna rownoleglosc zapisow wynosi 12, odczytow 8. Test sprawdza, ze pierwsze zapisy sa nowe, powtorzenia nie tworza dodatkowych relacji, a koncowy licznik wzrasta dokladnie o 89.

Jedna komorka wydarzenia zawiera 20 osob, a odseparowana komorka 9 osob. Wynik musi zawierac tylko komorke 20-osobowa, co potwierdza zachowanie progu `count >= 10` pod obciazeniem. Test nie wypisuje identyfikatorow ani wspolrzednych.

Uruchomienie tylko tego scenariusza:

```powershell
dotnet test backend/tests/FlowBB.Infrastructure.Tests/FlowBB.Infrastructure.Tests.csproj `
  --filter FullyQualifiedName~Neo4jPulseLoadTests --logger "console;verbosity=detailed"
```

Progi regresji sa celowo konserwatywne dla lokalnego kontenera: przepustowosc co najmniej `5 ops/s`, p95 zapisu najwyzej `5000 ms`, p95 odczytu najwyzej `3000 ms`. Pomiar z 2026-09-20: AMD Ryzen 7 7735HS (8 rdzeni/16 watkow), 31,2 GB RAM, Docker 29.2.1, .NET SDK 10.0.400, Neo4j 5.26.30 Community:

| Metryka | Wynik |
|---|---:|
| Przepustowosc laczna | 220,5 ops/s |
| Zapis Attendance, mediana | 24,7 ms |
| Zapis Attendance, p95 | 380,9 ms |
| Odczyt PULSE, mediana | 40,3 ms |
| Odczyt PULSE, p95 | 232,2 ms |

Wynik miesci sie w progach; test nie wykryl problemu wymagajacego osobnego issue. Liczby sa punktem odniesienia dla tego sprzetu, a nie SLA produkcyjnym.

### Reset bazy testowej

Testy same sprzataja swoje dane (`TestRunId`), ale po przerwanym przebiegu moga zostac wezly. Najprosciej wyrzucic jednorazowy kontener razem z danymi:

```bash
docker rm -f flowbb-neo4j-test
docker run -d --name flowbb-neo4j-test -p 127.0.0.1:17687:7687 \n  -e NEO4J_AUTH=neo4j/<haslo> neo4j:5.26.30-community
```

Zeby wyczyscic dane bez zatrzymywania kontenera (**tylko jednorazowa baza testowa**, nigdy baza aplikacji ani Aura):

```bash
docker exec flowbb-neo4j-test cypher-shell -u neo4j -p <haslo> "MATCH (n) DETACH DELETE n"
```

Schemat zostaje (constraints, indeksy i znacznik wersji), a fixture i tak stosuje migracje przy kazdym przebiegu.

## Seed demonstracyjny i inicjalizacja przy starcie API (`flowbb-demo-seed.cypher`)

Drugi, wiekszy seed (issue #7) jest osadzany w assembly `FlowBB.Infrastructure` i uruchamiany przez `Neo4jDatabaseInitializer`. `flowbb-queries.cypher` (4 uzytkownikow) zostaje reczna, mala wersja do Neo4j Query. Oba seedy uzywaja tych samych nazw `DefaultOriginLatitude`/`DefaultOriginLongitude`, wiec dzialaja z tymi samymi adapterami. Roznice i zalecane poprawki: [../docs/NEO4J_ADAPTER_RECONCILIATION.md](../docs/NEO4J_ADAPTER_RECONCILIATION.md).

`flowbb-demo-seed.cypher` zawiera idempotentne dane syntetyczne zgodne z mockiem `client/src/mocks/data.ts`. Seed nie wymaga kontenera ani wolumenu i jest osadzany w assembly Infrastructure podczas buildu.

### Automatyczne uruchomienie

Backend wykonuje brakujace migracje i seed przed wystartowaniem serwera, gdy ustawiono:

```text
NEO4J_SEED_ON_STARTUP=true
```

Lokalne profile `dotnet run` (`http` i `https`) mają tę opcję włączoną. W innym środowisku, np. po wdrożeniu backendu, ustaw zmienną samodzielnie. Brak wartości lub `false` pomija inicjalizację, dzięki czemu zwykłe testy i środowiska bez Neo4j nie łączą się z Aurą.

```powershell
dotnet run --project backend/src/FlowBB.Api
```

Startup czeka na zakończenie inicjalizacji. Błąd połączenia albo błąd Cypher zatrzymuje uruchomienie, zamiast wystartować z niepełnymi danymi. Wszystkie zapytania seedujące są wykonywane w jednej transakcji; błąd wycofuje cały seed. Kolejne uruchomienie aktualizuje te same węzły i nie dubluje relacji.

### Dane zgodne z frontendem

- 4 wydarzenia z `client/src/mocks/data.ts` wraz z tymi samymi identyfikatorami, nazwami, terminami, miejscami i współrzędnymi;
- 84 syntetycznych użytkowników; konta demo `aaaaaaaa-...` (smoke test) i `dddddddd-...` (klient) nie mają początkowej deklaracji ani członkostwa w Crew, a liczniki uczestników `82`, `46`, `28`, `64` są odwzorowane relacjami `IS_GOING_TO` pozostałych użytkowników;
- 2 grupy CREW z tymi samymi identyfikatorami, limitami, tagami, punktami spotkania i liczbą członków `4` oraz `6`;
- dodatkowe swobodne tagi, miejsca i syntetyczny organizator.

Każdy `Event` ma kanoniczne pola `Category` i `Source`, mapowane 1:1 na enumy backendu. Węzły `Tag` połączone przez `HAS_TAG` są dodatkowymi zainteresowaniami i nie zastępują tych pól. Każda relacja `IS_GOING_TO` ma snapshot `TransportMode`, `OriginLatitude`, `OriginLongitude` i `UpdatedAt`, z którego backend wylicza agregaty PULSE. Pole `DemoData` nie występuje w modelu.

Użytkownicy mają nazwy `Uzytkownik XXX`, adresy email w zarezerwowanej domenie `.invalid` i placeholder `PasswordHash`, który nie umożliwia logowania. Nie ma prawdziwych danych osobowych.

### Uruchomienie ręczne i kontrola

W Aura Query można nadal wkleić kolejne ponumerowane bloki z pliku. Backend automatycznie wykonuje tylko bloki przed znacznikiem `__FLOWBB_SEED_END__`; zapytania 16-22 są kontrolne.

Oczekiwane wyniki:

- liczba węzłów seedu: `User=84`, `Event=4`, `Venue=4`, `BusinessOwner=1`, `Tag=4`, `Crew=2`;
- uczestnicy wydarzeń: `82`, `46`, `28`, `64`;
- użytkownicy demo `aaaaaaaa-...` i `dddddddd-...`: zero relacji `IS_GOING_TO` i `MEMBER_OF`;
- kontrola `HOSTED_AT`: zero wierszy;
- `InvalidCoordinates=0`;
- `InvalidEvents=0` i `InvalidAttendanceSnapshots=0`.

### PULSE

PULSE nie jest osobną bazą ani zapisanym licznikiem. API odczytuje snapshoty relacji `IS_GOING_TO`, a istniejąca logika Application wylicza liczniki, podział środków transportu i komórki mapy. Publiczna mapa zwraca wyłącznie zagregowane komórki z `count >= 10`; identyfikatory i dokładne punkty użytkowników nie opuszczają backendu.

### Testy integracyjne Neo4j

Testy `FlowBB.Infrastructure.Tests` lacza sie z jednorazowa instancja Neo4j ustawiana zmiennymi `FLOWBB_NEO4J_TEST_URI`, `FLOWBB_NEO4J_TEST_PASSWORD` i `FLOWBB_NEO4J_TEST_CONFIRM_DISPOSABLE=true` (opcjonalnie `..._USERNAME`, `..._DATABASE`, domyslnie `neo4j`). Sa to celowo inne zmienne niz `NEO4J_*`, zeby testy nie trafily w baze aplikacji. Hosty Aura sa zawsze odrzucane. Bez URI i hasla testy sa pomijane (`Skipped`), a zwykly `dotnet test` nie wymaga bazy; konfiguracja polaczenia bez potwierdzenia konczy sie bledem przed pierwszym zapisem. Szczegoly i przyklad uruchomienia: sekcja „Testy adapterow na prawdziwym Neo4j" wyzej. Testy tworza izolowane dane i usuwaja je po sobie, nie wypisuja sekretow ani dokladnych wspolrzednych.
