# Jakosc kodu: analizatory, baseline i progi (issue #101)

AGENTS.md (sekcja 15) wymaga kodu C# bez nowych problemow Critical/Blocker/Major oraz Cognitive Complexity metody
do 10 (twardo 15). Ten dokument opisuje, co jest egzekwowane dzis, jaki byl stan wyjsciowy i czego jeszcze brakuje.

## Co jest wlaczone w repozytorium

- `backend/Directory.Build.props`: `EnableNETAnalyzers=true`, `TreatWarningsAsErrors=false`. Domyslny poziom analizy
  zostaje, wiec `dotnet build` nadal daje **0 ostrzezen**.
- `.editorconfig` (tylko sekcja analizatorow, bez regul formatowania, zeby `dotnet format --verify-no-changes` w CI
  sprawdzal to samo co wczesniej): reguly bez zadnych naruszen w kodzie sa podniesione do `warning`, zeby nowe
  naruszenia nie mogly sie pojawic: `CA1816`, `CA1829`, `CA1845`, `CA1847`, `CA1854`, `CA1860`, `CA1862`, `CA2016`,
  `CA2200`, `CA2201`, `CA2213`, `CA2254`.
- `TreatWarningsAsErrors` zostaje wylaczone. Wlaczenie go przed prezentacja niczego nie poprawia, a ryzykuje
  czerwony `develop`; decyzja po feature freeze.

Pelny raport regul Recommended (nie blokuje builda, do przegladu):

```bash
dotnet build backend/FlowBB.sln --no-incremental -p:AnalysisMode=Recommended
```

## Baseline (2026-09-20, `develop` `1717278`)

Pomiar lokalny z **SonarAnalyzer.CSharp 10.34** i analizatorami .NET w trybie Recommended. SonarAnalyzer zostal dodany
tylko na czas pomiaru i **nie jest zaleznoscia repozytorium** (nowy pakiet wymaga zgody Backend 1). To nie jest
raport SonarCloud, wiec nie zastepuje bramki Sonar z AGENTS.md.

- **Cognitive Complexity (S3776): 0 metod powyzej progu 15** w `src` i `tests`. Cel <= 10 nie jest mierzony osobno
  (domyslny prog S3776 to 15).
- Blocker i Critical: nie znaleziono w tym przebiegu.
- Nizsze wagi w `src` (po deduplikacji): 18x S3236, 14x CA1848, 6x CA1873, 4x CA1859, 3x CA1716, 2x CA1711, 2x S6667,
  2x S2325/CA1822, 2x CA1305, 2x CA1863, 2x S1075 (jedno takze S5332), 1x S8949, 1x S6966.
- W testach: glownie CA1707 (nazwy `Metoda_Warunek_Wynik`, przyjeta konwencja, wylaczona w `.editorconfig`),
  CA1305, S8969, S6580, CA2000.

## Podzial znalezisk na wlascicieli

Poprawki poza obszarem Backend 2 nie sa czescia tego zadania; ponizej propozycja osobnych issues.

| Obszar (wlasciciel) | Znaleziska | Uwagi |
|---|---|---|
| Domain i Application (Backend 1 / Events, Crew: Backend 2) | S3236 w `Crew`, `MeetingPoint`, `RouteStep`, `PulsePoint`, `HexGrid`; S2325 w `AirQualityMeasurement.Unit` i `RouteGeometry.Type`; CA1716/CA1711 (nazwy `Event`, `CreateEventHandler`) | S3236: argument `nameof(...)` przekazywany do metod z atrybutem caller info; drobne. CA1716/CA1711 dotycza nazw z kontraktu, nie zmieniac. |
| Api i `Program.cs` (Backend 1) | CA1848/CA1873 (logowanie bez `LoggerMessage`), S1075/S5332 (domyslny adres uslugi routingu w `Program.cs`), S6966, S8949 | Domyslny `http://routing:8000` to adres wewnetrzny w Compose, nie ruch publiczny. |
| Infrastructure/Neo4j (Data) | CA1859 (typy zwracane), CA1305/CA1863 w `Neo4jEventRepository` | Drobne. |
| Infrastructure/Routing i AirQuality (Backend 1) | S6667: log w `catch` bez wyjatku w `CompositeRoutePlanner` i `RoutingServiceClient` | Prawdopodobnie zamierzone: wyjatek moglby zawierac dane z zapytania (wspolrzedne); nie zmieniac bez decyzji. |

## Progi i zasady

- Nowy kod C#: brak nowych ostrzezen w buildzie, brak Blocker/Critical/Major w zmienionym kodzie, metoda do 10
  (twardo 15), bez `NOSONAR` i `SuppressMessage` bez zgody.
- Regule mozna dodac do listy `warning` w `.editorconfig` dopiero, gdy kod nie ma jej naruszen.
- Sonar nie jest jeszcze uruchamiany w CI.

## Decyzje (2026-09-20, wlasciciel repozytorium delegowal wybor)

1. **SonarAnalyzer.CSharp i SonarCloud: nie teraz.** Pakiet dodalby okolo 60 ostrzezen w `src`, co lamie zasade
   builda bez ostrzezen, a SonarCloud to usluga zewnetrzna z sekretem. Przed prezentacja nie poprawia to demo, a
   ryzykuje czerwony `develop`. Po feature freeze: najpierw SonarAnalyzer.CSharp (bez zewnetrznego konta, regula
   S3776 lokalnie i w CI) razem z naprawa znalezisk z tabeli, potem ewentualnie SonarCloud. Kryterium #101 "analiza
   Sonar w CI" pozostaje wiec niespelnione i jest przeniesione na okres po freeze.
2. **`TreatWarningsAsErrors`:** dopiero po feature freeze i po naprawie znalezisk.
3. **Osobne issues dla znalezisk:** nie tworzymy ich teraz; tabela powyzej jest lista wejsciowa na okres po freeze
   i wlasciciele obszarow biora z niej pozycje, gdy dotykaja danego kodu.
