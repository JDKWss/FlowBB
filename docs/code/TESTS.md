# Mapa testow

## Projekty

| Projekt | Zakres | Zaleznosci zewnetrzne |
|---|---|---|
| `FlowBB.Domain.Tests` | walidacja i reguly modeli domenowych | brak |
| `FlowBB.Application.Tests` | handlery na fake'ach portow | brak |
| `FlowBB.Api.IntegrationTests` | endpointy przez `WebApplicationFactory`, hub SignalR, adapter Neo4j | Neo4j **tylko** dla `[Neo4jFact]` |

## Zmierzone wyniki

Uruchomienie `dotnet test backend/FlowBB.sln`, stan na 2026-09-20.

### Wynik wspolny (jedyny, ktory cos znaczy)

Branch `integration/feature-backend`: `develop` po cofnieciu `a77ae09`, z doklejonymi
wszystkimi siedmioma branchami Feature w kolejnosci stosu.

| Projekt | Testy |
|---|---|
| `FlowBB.Domain.Tests` | 93 |
| `FlowBB.Application.Tests` | 88 |
| `FlowBB.Api.IntegrationTests` | 132 |
| **Razem** | **313 zielonych, 0 pominietych, 0 ostrzezen** |

### Snapshoty pojedynczych branchy

| Branch | Domain | Application | Integration | Baza |
|---|---|---|---|---|
| `origin/develop` (`a77ae09`) | 5 | 41 | 51 (+7 pominietych) | - |
| `feature/events-api` | 37 | 57 | 29 | stary `develop` |
| `feature/crew-application-api` | 72 | 47 | 37 | stary `develop` |
| `feature/routing-api` | 53 | 39 | 54 | stary `develop` |

**Tych liczb nie wolno sumowac.** To trzy osobne snapshoty roznych branchy: kazdy wyszedl
z innego stanu `develop`, a branche stojace na wspolnym stosie commitow zawieraja czesc tych
samych testow. Suma 123 + 156 + 146 sugerowalaby pokrycie, ktorego nie ma - rzeczywista liczba
po zlozeniu wszystkiego razem to 313.

## Testy wymagajace prawdziwego Neo4j

`backend/tests/FlowBB.Api.IntegrationTests/Neo4j/` zawiera testy oznaczone `[Neo4jFact]`.
Atrybut (`Neo4jFactAttribute` w `GraphTestData.cs`) ustawia `Skip`, dopoki nie ma
zmiennej srodowiskowej `FLOWBB_NEO4J_TESTS=1`:

```bash
FLOWBB_NEO4J_TESTS=1 \
NEO4J_URI=... NEO4J_DATABASE=... NEO4J_USERNAME=... NEO4J_PASSWORD=... \
dotnet test backend/FlowBB.sln
```

Domyslnie sa to te **7 pominietych** testow w tabeli snapshotow. Na branchu integracyjnym
nie ma ich wcale, bo przyszly razem z commitem `a77ae09`, ktory jest tam cofniety - to znaczy,
ze **na branchu integracyjnym nie ma dzis ani jednego testu dotykajacego prawdziwej bazy**.
Zielony wynik `dotnet test`
na czystym srodowisku **nie oznacza**, ze warstwa Neo4j dziala - oznacza tylko, ze nie zostala
sprawdzona. `AGENTS.md` sekcja 10 wymaga uruchomienia ich na prawdziwej instancji przed
uznaniem idempotencji i constraintow za zweryfikowane.

`GraphTestData` tworzy dane z losowymi identyfikatorami i sprzata po sobie (`IAsyncDisposable`),
a uzytkownicy testowi dostaja email `test-...@example.invalid` i jawny placeholder
zamiast hasha hasla.

## Podwojne dublery, czyli czego testy nie dowodza

Wszystkie testy endpointow uzywaja fake'ow portow (`FakeAttendanceRepository`,
`FakePulseDataReader`, `FakeEventRepository`, `FakeCrewRepository`, `FakeEventLookup`).
To jest poprawne dla testu warstwy HTTP, ale ma konsekwencje:

- **idempotencja Attendance jest dzis sprawdzona wobec fake'a, a nie wobec `MERGE` w Neo4j.**
  Prawdziwy dowod idempotencji wymaga testu na instancji bazy (issue #17);
- atomowosc `ICrewRepository.TryJoinAsync` i policzenie licznika w tej samej transakcji
  co zapis to wlasciwosci adaptera, ktorych fake z definicji nie moze potwierdzic;
- `AttendanceSignalRFlowTests` dowodzi, ze po POST leci `PulseUpdated` z poprawnym cialem -
  ale nie tego, ze dane trafily do bazy.

## Co jest przetestowane dobrze

| Obszar | Gdzie |
|---|---|
| Prog prywatnosci PULSE (9 ukryte, 10 zwrocone) | `FlowBB.Application.Tests/Pulse/` |
| Geometria siatki heksagonalnej | `Pulse/HexGridTests.cs` |
| Walidacja modeli domenowych | `FlowBB.Domain.Tests/**` |
| Przeplyw POST attendance -> komunikat SignalR | `Endpoints/Events/AttendanceSignalRFlowTests.cs` |
| Zgodnosc Events z kontraktem | `Endpoints/Events/EventsContractTests.cs` (branch) |
| Determinizm `DemoRoutePlanner` | `Routing/DemoRoutePlannerTests.cs` (branch) |
| Start aplikacji i `/health` | `Startup/StartupTests.cs`, `HealthEndpointTests.cs` |

## Smoke test end-to-end

`infra/smoke-test.ps1` (PowerShell 7) przechodzi scenariusz demo przez HTTP:

```powershell
pwsh infra/smoke-test.ps1 -BaseUrl http://localhost:8080
```

Wyniki na krok: PASS / FAIL / SKIP. **SKIP oznacza, ze endpoint nie jest podpiety w `Program.cs`** -
dzis dotyczy to wszystkich endpointow biznesowych. Kod wyjscia 1 to FAIL, 2 to nieosiagalne API.
Szczegoly i plan awaryjny: [DEMO_RUNBOOK.md](../DEMO_RUNBOOK.md).

## Analiza statyczna

Repozytorium **nie ma skonfigurowanego SonarQube**: brak `sonar-project.properties`,
brak `dotnet-sonarscanner` w `dotnet-tools.json` (manifest jest pusty) i brak pipeline'u CI.
Analizy nie da sie wiec uruchomic lokalnie bez serwera Sonara i nie zostala uruchomiona.

Zastepczo, recznie wzgledem regul z `AGENTS.md` sekcja 15, na branchu integracyjnym:

| Miara | Wynik |
|---|---|
| Metody >= 50 linii | 0 |
| Najdluzsza metoda | konstruktor `Events/Event` - 49 linii (plaskie guard clauses) |
| Szacowana najwyzsza Cognitive Complexity | ok. 6 (`GetEventRouteHandler.HandleAsync`) |
| Maksymalne zagniezdzenie | 2 poziomy (`CrewsEndpoints.GetEventGroupsAsync`) |

To jest **oszacowanie z lektury kodu, nie wynik Sonara**. Przed mergem ktos z dostepem do
serwera Sonara musi podac faktyczny wynik dla zmienionego kodu.
