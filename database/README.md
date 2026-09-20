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

Aura nie daje dostepu do powloki kontenera, wiec sa dwie drogi:

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
- Zachowania na Aura nie sprawdzano. Schemat jest zgodny z Aura, bo nie uzywa constraintow Enterprise.

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
