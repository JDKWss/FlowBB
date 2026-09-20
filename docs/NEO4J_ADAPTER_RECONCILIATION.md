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
| Inicjalizacja przy starcie | `Neo4jDatabaseInitializer` + embedded `flowbb-demo-seed.cypher` | `database/schema.cypher`, `flowbb-queries.cypher` | initializer laduje osadzony `database/schema.cypher` i seed demo przez wspolny `IDriver` |
| Stary graf (`Neo4jFlowBbGraphRepository*`, `Domain/Models/*`, `IFlowBbGraphRepository`) | uzywany przez initializer | usuniety w #16 | **usuniety w #59 i na tej galezi**; initializer korzysta bezposrednio z `IDriver` (patrz 3.B) |
| Testy adapterow | `Neo4jPulseDataReaderTests`, `...RegistrationTests` (wczesniej opt-in `NEO4J_RUN_INTEGRATION_TESTS`, zmienne `NEO4J_*`) | wspolny fixture testow na prawdziwej bazie | **jeden mechanizm**: `Neo4jFixture` i `Neo4jFactAttribute` (`FLOWBB_NEO4J_TEST_*`); testy Pulse zachowuja przypadki `EndAt` z `develop` |

Dlaczego adaptery z tej galezi: wersje z `develop` licza `IsNew` osobnym `OPTIONAL MATCH` przed `MERGE` (dwa rownolegle zapisy tej samej pary moga oba zwrocic `IsNew = true`), a rownolegle `DELETE` tej samej pary zawyzaja `WasDeleted`. Adaptery z tej galezi zamykaja te wyscigi i maja testy na prawdziwym Neo4j, w tym rownolegle (25 zapisow tej samej pary, limit `MaxMembers` przy 12 rownoleglych dolaczeniach).

## 2. Mosty (warstwa zgodnosci)

1. **Nazwy wspolrzednych uzytkownika** (rozwiazane): obowiazuje `DefaultOriginLatitude/DefaultOriginLongitude`, zgodnie z kodem, seedami, `AGENTS.md` i ADR 001. Adapter Attendance i `flowbb-queries.cypher` uzywaja tylko tych nazw, bez zamiennika `Home*`.
2. **Rejestracja DI.** `Neo4jPersistenceExtensions` rejestruje teraz `IAttendanceOriginLookup -> Neo4jAttendanceOriginLookup` i `ICrewRepository -> Neo4jCrewRepository`. `Program.cs` (Core Backend) nie wymaga zmiany.
3. **Dwa mechanizmy schematu** (rozwiazane): initializer zaklada teraz schemat z osadzonego `database/schema.cypher`, wiec constraints i indeks `event_start_at` sa w obu sciezkach identyczne.

Weryfikacja mostow na prawdziwym Neo4j 5.26 Community: 41 testow z tej galezi, 8 testow z `develop` uruchomionych na nowych adapterach (`NEO4J_RUN_INTEGRATION_TESTS=true`), oraz `flowbb-demo-seed.cypher` przepuszczony przez adaptery Events, PULSE i Crew: liczby uczestnikow 82/46/28/64, punkty PULSE rowne liczbie uczestnikow, grupy 4/6 i 6/6, czyli zgodnie z opisem w `database/README.md`.

## 3. Co warto poprawic (kolejnosc zalecana)

**A. Nazwy wspolrzednych: ZROBIONE (`DefaultOrigin*`).**
Kod, seedy, `AGENTS.md` i ADR 001 sa spojne. Migracja danych: `flowbb-queries.cypher` usuwa `HomeLatitude/HomeLongitude` z uzytkownikow po starszym seedzie.

**B. Initializer bez starego grafu: ZROBIONE.**
`Neo4jDatabaseInitializer` uzywa teraz `IDriver` + `Neo4jOptions`: zaklada schemat z osadzonego `database/schema.cypher` (w tym indeks `event_start_at`) i stosuje ten sam seed. Stary stos (`Neo4jFlowBbGraphRepository*`, `Domain/Models/*`, `IFlowBbGraphRepository`) zostal usuniety; nic poza initializerem z niego nie korzystalo. Test na prawdziwej bazie: `Neo4jDatabaseInitializerTests`. Znika tez duplikat schematu z 2.3 i `DefaultOrigin*` w starym grafie (punkt A zostaje dla seedu demonstracyjnego i adaptera Attendance).

**C. Jeden seed na baze (Data/Neo4j).**
`flowbb-queries.cypher` (4 uzytkownikow, reczny) i `flowbb-demo-seed.cypher` (84 uzytkownikow, uruchamiany przy starcie) uzywaja tych samych identyfikatorow wydarzen (`1111...`, `3333...`, `4444...`) z roznymi miejscami. Zastosowane po kolei w jednej bazie daja wydarzenia z dwoma relacjami `HOSTED_AT`, a lista wydarzen zwraca je dwa razy (potwierdzone na prawdziwej bazie). Adapter tego celowo nie maskuje, bo kontrakt mowi o dokladnie jednym miejscu. Zalecenie: zostawic jeden seed (demonstracyjny) albo dac im rozlaczne identyfikatory, a przed zmiana seedu czyscic baze (`MATCH (n) DETACH DELETE n`).

**D. Drobne porzadki.**
- ZROBIONE: `Neo4jPersistenceExtensions` tworzy `IDriver` przez `Neo4jDriverFactory` (jedno miejsce), tak samo initializer i fixture testow.
- ZROBIONE: testy z `develop` (`Neo4jPulseDataReaderTests`) uzywaja `Neo4jFixture` i `FLOWBB_NEO4J_TEST_*` zamiast `NEO4J_*` (bazy aplikacji); usuniety `Neo4jIntegrationFactAttribute` i opt-in `NEO4J_RUN_INTEGRATION_TESTS`. Harness testowy (`Neo4jFixture`, `Neo4jFactAttribute`, `Neo4jTestEnvironment`) jest zakresem issue #43.
- `Neo4jPulseDataReaderTests` i `Neo4jPulseSnapshotAdapterTests` pokrywaja sie czesciowo (punkty per wydarzenie, pusta lista); po uzgodnieniu mozna zostawic jeden.
- Wydarzenie z niepoprawnymi polami (np. nieznana `Category`) powoduje `InvalidOperationException` z Id, a nie pominiecie wiersza, wiec jedno uszkodzone wydarzenie psuje cala liste. Community nie ma constraintow istnienia; decyzja, czy wolimy pomijanie z logiem, nalezy do Core Backend.
