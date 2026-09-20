# Baza Neo4j: schemat i seed

Kontrakt danych: [../docs/NEO4J_CONTRACT.md](../docs/NEO4J_CONTRACT.md). Decyzja o bazie: [../docs/adr/001-runtime-persistence.md](../docs/adr/001-runtime-persistence.md).

| Plik | Zawartosc | Idempotentny |
|---|---|---|
| `schema.cypher` | constraints unikalnosci i indeksy | tak (`IF NOT EXISTS`) |
| `flowbb-queries.cypher` | syntetyczny seed (`DEMO DATA / SYMULACJA`) i zapytania kontrolne | tak (`MERGE` + `SET`) |

Kolejnosc: najpierw `schema.cypher`, potem seed. Oba pliki mozna uruchamiac wielokrotnie; drugie uruchomienie nie zmienia liczby wezlow (20) ani relacji (41).

## Uruchomienie na lokalnym kontenerze

Przy kontenerze `neo4j` z profilu `local-db` (`infra/docker-compose.yml`), z katalogu glownego repozytorium i z wczytanym `.env`:

```bash
docker compose --profile local-db exec -T neo4j \
  cypher-shell -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" < database/schema.cypher
docker compose --profile local-db exec -T neo4j \
  cypher-shell -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" < database/flowbb-queries.cypher
```

Wynik kontrolny: `SHOW CONSTRAINTS` pokazuje 8 constraintow `UNIQUENESS`, a `MATCH (n) RETURN count(n)` po seedzie zwraca 20.

## Uruchomienie na Neo4j Aura

Instrukcja ponizej **nie byla sprawdzana na Aurze** (weryfikacja: lokalny Neo4j 5.26 Community). Aura nie daje dostepu do powloki kontenera, wiec sa dwie drogi:

1. **Konsola Aura (Query)**: otworz plik, wklej i uruchom kazdy blok od pierwszego slowa do srednika. Bloki sa niezalezne, wiec w razie bledu mozna wznowic od dowolnego miejsca.
2. **`cypher-shell` z lokalnego komputera**:

   ```bash
   cypher-shell -a "$NEO4J_URI" -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" -d "$NEO4J_DATABASE" -f database/schema.cypher
   ```

   `NEO4J_URI` ma schemat `neo4j+s://` (wartosc z konsoli Aura). Hasla nie zapisujemy w repozytorium.

## Ograniczenia edycji Community

- Schemat uzywa wylacznie constraintow unikalnosci i indeksow zakresu. Constraint istnienia (`IS NOT NULL`) i klucz wezla wymagaja edycji Enterprise (sprawdzone na Neo4j 5.26 Community: `Property existence constraint requires Neo4j Enterprise Edition`), wiec nie sa czescia schematu.
- Konsekwencja: baza przyjmie wezel `Event` bez `Name`. Kompletnosc pol wymaganych pilnuja adaptery w `Infrastructure/Neo4j`, a nie schemat. Unikalnosc `EventId`, `UserId`, `VenueId`, `CrewId` jest wymuszana przez baze (duplikat konczy sie bledem).
- Edycja Community obsluguje jedna baze uzytkownika, o nazwie `neo4j`. Dla lokalnego kontenera ustaw `NEO4J_DATABASE=neo4j`. Kod ma domyslnie `flowbb`, wiec bez tej zmiennej polaczenie z Community sie nie uda.
- Unikalnosci relacji `IS_GOING_TO` nie wymusza constraint: ma ja gwarantowac `MERGE` na parze wezlow. Do potwierdzenia testem rownoleglych zapisow na prawdziwej instancji w issue #17.
- Aury nie sprawdzano: schemat, seed i testy zweryfikowano wylacznie na lokalnym Neo4j 5.26 Community.

## Migracja z poprzedniego seedu

Seed przenosi dane ze starego modelu: ustawia `HomeLatitude`/`HomeLongitude` i usuwa `DefaultOriginLatitude`/`DefaultOriginLongitude` oraz `DemoData`. Ponowne uruchomienie na bazie po starszym seedzie nie wymaga czyszczenia danych. Snapshot `IS_GOING_TO` (`TransportMode`, `OriginLatitude`, `OriginLongitude`, `UpdatedAt`) i `MEMBER_OF.JoinedAt` sa uzupelniane na istniejacych relacjach.

## Testy adapterow na prawdziwym Neo4j

Testy w `backend/tests/FlowBB.Infrastructure.Tests` lacza sie z prawdziwa instancja, ustawiana zmiennymi `FLOWBB_TEST_NEO4J_URI`, `FLOWBB_TEST_NEO4J_PASSWORD` (oraz opcjonalnie `..._USERNAME` i `..._DATABASE`, domyslnie `neo4j`). Sa to celowo inne zmienne niz `NEO4J_*`, zeby testy nie trafily przypadkiem w baze aplikacji: **wskazuj tylko jednorazowa instancje**, bo testy zapisuja i usuwaja dane. Fixture stosuje prawdziwy `schema.cypher`, a dane testowe usuwa po przebiegu.

```bash
docker run -d --name flowbb-neo4j-test -p 127.0.0.1:17687:7687 \
  -e NEO4J_AUTH=neo4j/<haslo> neo4j:5.26.30-community
export FLOWBB_TEST_NEO4J_URI=neo4j://127.0.0.1:17687 FLOWBB_TEST_NEO4J_PASSWORD=<haslo>
dotnet test backend/FlowBB.sln
```

Bez tych zmiennych testy adapterow sa **pomijane (Skipped)**, a nie zaliczane. Zielony `dotnet test` bez bazy nie dowodzi, ze adapter dziala: sprawdz w wyniku, ze testy `FlowBB.Infrastructure.Tests` nie sa pominiete.

## Seed demonstracyjny i inicjalizacja przy starcie API (`flowbb-demo-seed.cypher`)

Drugi, wiekszy seed (issue #7) jest osadzany w assembly `FlowBB.Infrastructure` i uruchamiany przez `Neo4jDatabaseInitializer`. `flowbb-queries.cypher` (4 uzytkownikow) zostaje reczna, mala wersja do Neo4j Query. Seed demonstracyjny uzywa nazw `DefaultOriginLatitude`/`DefaultOriginLongitude`, a adaptery czytaja obie nazwy, wiec oba seedy dzialaja z tymi samymi adapterami. Roznice i zalecane poprawki: [../docs/NEO4J_ADAPTER_RECONCILIATION.md](../docs/NEO4J_ADAPTER_RECONCILIATION.md).

`flowbb-demo-seed.cypher` zawiera idempotentne dane syntetyczne zgodne z mockiem `client/src/mocks/data.ts`. Seed nie wymaga kontenera ani wolumenu i jest osadzany w assembly Infrastructure podczas buildu.

### Automatyczne uruchomienie

Backend wykonuje constraints i seed przed wystartowaniem serwera, gdy ustawiono:

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
- 82 syntetycznych użytkowników, w tym `DEMO_USER_ID` klienta, oraz liczniki uczestników `82`, `46`, `28`, `64` odwzorowane relacjami `IS_GOING_TO`;
- 2 grupy CREW z tymi samymi identyfikatorami, limitami, tagami, punktami spotkania i liczbą członków `4` oraz `6`;
- dodatkowe swobodne tagi, miejsca i syntetyczny organizator.

Każdy `Event` ma kanoniczne pola `Category` i `Source`, mapowane 1:1 na enumy backendu. Węzły `Tag` połączone przez `HAS_TAG` są dodatkowymi zainteresowaniami i nie zastępują tych pól. Każda relacja `IS_GOING_TO` ma snapshot `TransportMode`, `OriginLatitude`, `OriginLongitude` i `UpdatedAt`, z którego backend wylicza agregaty PULSE. Pole `DemoData` nie występuje w modelu.

Użytkownicy mają nazwy `Uzytkownik XXX`, adresy email w zarezerwowanej domenie `.invalid` i placeholder `PasswordHash`, który nie umożliwia logowania. Nie ma prawdziwych danych osobowych.

### Uruchomienie ręczne i kontrola

W Aura Query można nadal wkleić kolejne ponumerowane bloki z pliku. Backend automatycznie wykonuje tylko bloki przed znacznikiem `__FLOWBB_SEED_END__`; zapytania 16-21 są kontrolne.

Oczekiwane wyniki:

- liczba węzłów seedu: `User=82`, `Event=4`, `Venue=4`, `BusinessOwner=1`, `Tag=4`, `Crew=2`;
- uczestnicy wydarzeń: `82`, `46`, `28`, `64`;
- kontrola `HOSTED_AT`: zero wierszy;
- `InvalidCoordinates=0`;
- `InvalidEvents=0` i `InvalidAttendanceSnapshots=0`.

### PULSE

PULSE nie jest osobną bazą ani zapisanym licznikiem. API odczytuje snapshoty relacji `IS_GOING_TO`, a istniejąca logika Application wylicza liczniki, podział środków transportu i komórki mapy. Publiczna mapa zwraca wyłącznie zagregowane komórki z `count >= 10`; identyfikatory i dokładne punkty użytkowników nie opuszczają backendu.

### Testy integracyjne Neo4j

Testy `FlowBB.Infrastructure.Tests` tworzą izolowane dane bezpośrednim Cypherem na skonfigurowanej instancji Neo4j i usuwają je po każdym teście. Są opt-in, aby zwykły `dotnet test` nie wymagał dostępu do bazy:

```powershell
$env:NEO4J_RUN_INTEGRATION_TESTS="true"
dotnet test backend/tests/FlowBB.Infrastructure.Tests/FlowBB.Infrastructure.Tests.csproj
```

Połączenie jest pobierane z tych samych zmiennych `NEO4J_*` lub ignorowanego pliku konfiguracyjnego co backend. Testy nie zapisują ani nie wypisują sekretów oraz dokładnych współrzędnych w logach.
