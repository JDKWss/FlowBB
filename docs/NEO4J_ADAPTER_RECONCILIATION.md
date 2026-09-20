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
| Stary graf (`Neo4jFlowBbGraphRepository*`, `Domain/Models/*`, `IFlowBbGraphRepository`) | uzywany przez initializer | usuniety w #16 | **usuniety w #59**; initializer korzysta bezposrednio z `IDriver` (patrz 3.B) |
| Testy adapterow | `Neo4jPulseDataReaderTests`, `...RegistrationTests` (wczesniej opt-in `NEO4J_RUN_INTEGRATION_TESTS`, zmienne `NEO4J_*`) | 41 testow (zmienne `FLOWBB_NEO4J_TEST_*`) | **jeden mechanizm**: wszystkie testy na `Neo4jFixture` i `Neo4jFactAttribute` (`FLOWBB_NEO4J_TEST_*`); testy Pulse z `develop` przeniesione, `Neo4jPulseSnapshotAdapterTests` z tej galezi zostaje |

Dlaczego adaptery z tej galezi: wersje z `develop` licza `IsNew` osobnym `OPTIONAL MATCH` przed `MERGE` (dwa rownolegle zapisy tej samej pary moga oba zwrocic `IsNew = true`), a rownolegle `DELETE` tej samej pary zawyzaja `WasDeleted`. Adaptery z tej galezi zamykaja te wyscigi i maja testy na prawdziwym Neo4j, w tym rownolegle (25 zapisow tej samej pary, limit `MaxMembers` przy 12 rownoleglych dolaczeniach).

## 2. Mosty (warstwa zgodnosci)

1. **Nazwy wspolrzednych uzytkownika** (rozwiazane): obowiazuje `DefaultOriginLatitude/DefaultOriginLongitude`, zgodnie z kodem i seedem demonstracyjnym na `develop`. Adapter Attendance i `flowbb-queries.cypher` uzywaja tylko tych nazw, bez zamiennika `Home*`. Zostaje do poprawy dokumentacja: `AGENTS.md` sekcja 8 i ADR 001 nadal mowia `HomeLatitude/HomeLongitude` (Core Backend).
2. **Rejestracja DI.** `Neo4jPersistenceExtensions` rejestruje teraz `IAttendanceOriginLookup -> Neo4jAttendanceOriginLookup` i `ICrewRepository -> Neo4jCrewRepository`. `Program.cs` (Core Backend) nie wymaga zmiany.
3. **Dwa mechanizmy schematu.** `Neo4jDatabaseInitializer` zaklada constraints z osmiu `SchemaQueries`; `database/schema.cypher` zaklada te same i dodatkowo indeks `event_start_at`. Initializer go nie zaklada. Bez indeksu lista wydarzen dziala poprawnie, tylko bez optymalizacji filtra po `StartAt`.

Weryfikacja mostow na prawdziwym Neo4j 5.26 Community: 41 testow z tej galezi, 8 testow z `develop` uruchomionych na nowych adapterach (`NEO4J_RUN_INTEGRATION_TESTS=true`), oraz `flowbb-demo-seed.cypher` przepuszczony przez adaptery Events, PULSE i Crew: liczby uczestnikow 82/46/28/64, punkty PULSE rowne liczbie uczestnikow, grupy 4/6 i 6/6, czyli zgodnie z opisem w `database/README.md`.

## 3. Co warto poprawic (kolejnosc zalecana)

**A. Nazwy wspolrzednych: ZROBIONE po stronie kodu i seedow (`DefaultOrigin*`).**
Do poprawy zostaje wylacznie dokumentacja, ktora jest poza obszarem Data/Neo4j: `AGENTS.md` sekcja 8 i ADR 001 (oraz opis issue #6). Migracja danych: `flowbb-queries.cypher` usuwa `HomeLatitude/HomeLongitude` z uzytkownikow po starszym seedzie.

**B. Zdjac initializer ze starego grafu, potem usunac stary stos: ZROBIONE w #59.**
`Neo4jDatabaseInitializer` korzysta teraz z `Neo4jDriverFactory` (`IDriver` + `Neo4jOptions`); usuniete zostaly `Neo4jFlowBbGraphRepository*`, `Domain/Models/*` i `Domain/Repositories/IFlowBbGraphRepository.cs`. Constraints (osiem `SchemaQueries`) i seed dzialaja jak dotychczas. Nie zrobiono zalecanego dawniej ladowania schematu z embedded `database/schema.cypher` (zmienialoby schemat o indeks `event_start_at`, a #59 nie zmienia zachowania), wiec duplikat schematu z 3.3 nadal istnieje.

**C. Jeden seed na baze (Data/Neo4j).**
`flowbb-queries.cypher` (4 uzytkownikow, reczny) i `flowbb-demo-seed.cypher` (83 uzytkownikow, uruchamiany przy starcie) uzywaja tych samych identyfikatorow wydarzen (`1111...`, `3333...`, `4444...`) z roznymi miejscami. Zastosowane po kolei w jednej bazie daja wydarzenia z dwoma relacjami `HOSTED_AT`, a lista wydarzen zwraca je dwa razy (potwierdzone na prawdziwej bazie). Adapter tego celowo nie maskuje, bo kontrakt mowi o dokladnie jednym miejscu. Zalecenie: zostawic jeden seed (demonstracyjny) albo dac im rozlaczne identyfikatory, a przed zmiana seedu czyscic baze (`MATCH (n) DETACH DELETE n`).

**D. Drobne porzadki.**
- `Neo4jDriverFactory` (z tej galezi) dubluje tworzenie `IDriver` w `Neo4jPersistenceExtensions`; uzywaja go fixture testow i (od #59) `Neo4jDatabaseInitializer`. Mozna zastapic go w rozszerzeniu.
- ZROBIONE: testy z `develop` (`Neo4jPulseDataReaderTests`) uzywaja `Neo4jFixture` i `FLOWBB_NEO4J_TEST_*` zamiast `NEO4J_*` (bazy aplikacji); usuniety `Neo4jIntegrationFactAttribute` i opt-in `NEO4J_RUN_INTEGRATION_TESTS`. Harness testowy (`Neo4jFixture`, `Neo4jFactAttribute`, `Neo4jTestEnvironment`) jest zakresem issue #43.
- `Neo4jPulseDataReaderTests` i `Neo4jPulseSnapshotAdapterTests` pokrywaja sie czesciowo (punkty per wydarzenie, pusta lista); po uzgodnieniu mozna zostawic jeden.
- Wydarzenie z niepoprawnymi polami (np. nieznana `Category`) powoduje `InvalidOperationException` z Id, a nie pominiecie wiersza, wiec jedno uszkodzone wydarzenie psuje cala liste. Community nie ma constraintow istnienia; decyzja, czy wolimy pomijanie z logiem, nalezy do Core Backend.
