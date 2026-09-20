# Adaptery Neo4j: uzgodnienie dwoch rownoleglych implementacji

Wlasciciel: Data/Neo4j. Stan po scaleniu `origin/develop` (commit `dfa63d0`) do `feature/neo4j-persistence`.

Rownolegle powstaly dwie wersje warstwy Neo4j: wersja z `develop` (issue #7 i #15, Nabiz) oraz wersja z tej galezi (issue #6, #8, #15-#18). Scalenie zostawia dzialajacy kod obu stron. Ten dokument opisuje, co jest tymczasowym mostem i co warto docelowo poprawic. Nie zmienia kontraktu API.

## 1. Co jest w scalonym kodzie

| Obszar | Wersja z `develop` | Wersja z tej galezi | Po scaleniu |
|---|---|---|---|
| `IAttendanceRepository` | `Neo4jAttendanceRepository` (jedna klasa razem z `IAttendanceOriginLookup`) | `Neo4jAttendanceRepository` + osobny `Neo4jAttendanceOriginLookup` | **wersja z tej galezi** |
| `IEventRepository` | `Neo4jEventRepository` | `Neo4jEventRepository` | **wersja z tej galezi** |
| `IPulseDataReader` | `Neo4jPulseDataReader` | `Neo4jPulseDataReader` | **wersja z tej galezi** |
| `ICrewRepository` | brak | `Neo4jCrewRepository` | z tej galezi |
| Rejestracja DI | `Neo4jPersistenceExtensions`, `Neo4jPulseDataReaderExtensions` | brak | z `develop`, dopasowana do nowych klas (osobny `OriginLookup`, dodany `Crew`) |
| Inicjalizacja przy starcie | `Neo4jDatabaseInitializer` + embedded `flowbb-demo-seed.cypher` | `database/schema.cypher`, `flowbb-queries.cypher` | oba; initializer bez zmian |
| Stary graf (`Neo4jFlowBbGraphRepository*`, `Domain/Models/*`, `IFlowBbGraphRepository`) | uzywany przez initializer | usuniety w #16 | **przywrocony z `develop`** (patrz 3.B) |
| Testy adapterow | `Neo4jPulseDataReaderTests`, `...RegistrationTests` (opt-in `NEO4J_RUN_INTEGRATION_TESTS`, zmienne `NEO4J_*`) | 41 testow (zmienne `FLOWBB_TEST_NEO4J_*`) | oba zestawy w jednym projekcie; testy Pulse z tej galezi w `Neo4jPulseSnapshotAdapterTests` |

Dlaczego adaptery z tej galezi: wersje z `develop` licza `IsNew` osobnym `OPTIONAL MATCH` przed `MERGE` (dwa rownolegle zapisy tej samej pary moga oba zwrocic `IsNew = true`), a rownolegle `DELETE` tej samej pary zawyzaja `WasDeleted`. Adaptery z tej galezi zamykaja te wyscigi i maja testy na prawdziwym Neo4j, w tym rownolegle (25 zapisow tej samej pary, limit `MaxMembers` przy 12 rownoleglych dolaczeniach).

## 2. Mosty (warstwa zgodnosci)

1. **Nazwy wspolrzednych uzytkownika** (rozwiazane): obowiazuje `DefaultOriginLatitude/DefaultOriginLongitude`, zgodnie z kodem i seedem demonstracyjnym na `develop`. Adapter Attendance i `flowbb-queries.cypher` uzywaja tylko tych nazw, bez zamiennika `Home*`. Zostaje do poprawy dokumentacja: `AGENTS.md` sekcja 8 i ADR 001 nadal mowia `HomeLatitude/HomeLongitude` (Core Backend).
2. **Rejestracja DI.** `Neo4jPersistenceExtensions` rejestruje teraz `IAttendanceOriginLookup -> Neo4jAttendanceOriginLookup` i `ICrewRepository -> Neo4jCrewRepository`. `Program.cs` (Core Backend) nie wymaga zmiany.
3. **Dwa mechanizmy schematu.** `Neo4jFlowBbGraphRepository.EnsureSchemaAsync` (initializer) zaklada constraints z osmiu `SchemaQueries`; `database/schema.cypher` zaklada te same i dodatkowo indeks `event_start_at`. Initializer go nie zaklada. Bez indeksu lista wydarzen dziala poprawnie, tylko bez optymalizacji filtra po `StartAt`.

Weryfikacja mostow na prawdziwym Neo4j 5.26 Community: 41 testow z tej galezi, 8 testow z `develop` uruchomionych na nowych adapterach (`NEO4J_RUN_INTEGRATION_TESTS=true`), oraz `flowbb-demo-seed.cypher` przepuszczony przez adaptery Events, PULSE i Crew: liczby uczestnikow 82/46/28/64, punkty PULSE rowne liczbie uczestnikow, grupy 4/6 i 6/6, czyli zgodnie z opisem w `database/README.md`.

## 3. Co warto poprawic (kolejnosc zalecana)

**A. Nazwy wspolrzednych: ZROBIONE po stronie kodu i seedow (`DefaultOrigin*`).**
Do poprawy zostaje wylacznie dokumentacja, ktora jest poza obszarem Data/Neo4j: `AGENTS.md` sekcja 8 i ADR 001 (oraz opis issue #6). Migracja danych: `flowbb-queries.cypher` usuwa `HomeLatitude/HomeLongitude` z uzytkownikow po starszym seedzie.

**B. Zdjac initializer ze starego grafu, potem usunac stary stos (Data/Neo4j).**
`Neo4jDatabaseInitializer` korzysta z `Neo4jFlowBbGraphRepository.FromEnvironment()`, `EnsureSchemaAsync()` i `ApplySeedAsync()`. Nalezy go przepisac na `IDriver` + `Neo4jOptions`: schemat z embedded `database/schema.cypher`, seed jak dotychczas. Dopiero wtedy mozna usunac `Neo4jFlowBbGraphRepository*`, `Domain/Models/*` i `Domain/Repositories/IFlowBbGraphRepository.cs` (nic poza initializerem z nich nie korzysta; sprawdzone grepem). Ten sam krok usuwa duplikat schematu z 3.3 i zamiennik z 3.A. W #16 zostalo to zrobione, a scalenie z `develop` to przywrocilo.

**C. Jeden seed na baze (Data/Neo4j).**
`flowbb-queries.cypher` (4 uzytkownikow, reczny) i `flowbb-demo-seed.cypher` (82 uzytkownikow, uruchamiany przy starcie) uzywaja tych samych identyfikatorow wydarzen (`1111...`, `3333...`, `4444...`) z roznymi miejscami. Zastosowane po kolei w jednej bazie daja wydarzenia z dwoma relacjami `HOSTED_AT`, a lista wydarzen zwraca je dwa razy (potwierdzone na prawdziwej bazie). Adapter tego celowo nie maskuje, bo kontrakt mowi o dokladnie jednym miejscu. Zalecenie: zostawic jeden seed (demonstracyjny) albo dac im rozlaczne identyfikatory, a przed zmiana seedu czyscic baze (`MATCH (n) DETACH DELETE n`).

**D. Drobne porzadki.**
- `Neo4jDriverFactory` (z tej galezi) dubluje tworzenie `IDriver` w `Neo4jPersistenceExtensions`; uzywa go tylko fixture testow. Mozna zastapic go w rozszerzeniu albo usunac.
- Testy z `develop` (`Neo4jPulseDataReaderTests`) czytaja `NEO4J_*` (baze aplikacji, np. Aura z `.env`) i zapisuja oraz usuwaja dane; testy z tej galezi uzywaja `FLOWBB_TEST_NEO4J_*`, zeby tego uniknac. Warto przeniesc oba zestawy na jeden mechanizm (`Neo4jFactAttribute` + `Neo4jFixture`).
- `Neo4jPulseDataReaderTests` i `Neo4jPulseSnapshotAdapterTests` pokrywaja sie czesciowo (punkty per wydarzenie, pusta lista); po uzgodnieniu mozna zostawic jeden.
- Wydarzenie z niepoprawnymi polami (np. nieznana `Category`) powoduje `InvalidOperationException` z Id, a nie pominiecie wiersza, wiec jedno uszkodzone wydarzenie psuje cala liste. Community nie ma constraintow istnienia; decyzja, czy wolimy pomijanie z logiem, nalezy do Core Backend.
