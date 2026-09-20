# Testy smoke (black-box) API FlowBB

Testy xUnit przez HTTP przeciwko **uruchomionemu** stosowi z prawdziwym Neo4j i seedem demo. Uzupelniaja testy
integracyjne z fake'ami portow (te nie wykryja bledow kompozycji, konfiguracji Neo4j ani rozbieznosci adapter/kontrakt).

## Uruchomienie

Bez zmiennej `FLOWBB_SMOKE_BASE_URL` testy sa **pomijane**, wiec zwykle `dotnet test backend/FlowBB.sln` pozostaje zielone.

```powershell
# 1. Stos (patrz docs/DEMO_RUNBOOK.md, sekcja 3). Wlasna nazwa projektu chroni przed kolizja woluminow miedzy worktree.
cd infra
docker compose -p flowbb-smoke up --build -d
cd ..

# 2. Testy (po tym, jak kontener api jest healthy)
$env:FLOWBB_SMOKE_BASE_URL = 'http://localhost:8080'
dotnet test backend/tests/FlowBB.Api.IntegrationTests --filter "FullyQualifiedName~Smoke"
```

## Co pokrywaja

Wszystkie operacje z `contracts/openapi.yaml` (`getHealth`, `getReadiness`, `getEvents`, `getEventById`, `upsertAttendance`,
`deleteAttendance`, `getEventGroups`, `joinGroup`, `leaveGroup`, `getEventRoute`, `getPulseSummary`, `getEventPulse`,
`getPulseHexagons`) oraz dostarczenie `PulseUpdated` przez hub `/hubs/pulse`. Dla kazdej: sciezka szczesliwa, bledy 400/404/409,
idempotencja, prywatnosc PULSE (`participants >= 10`, brak `userId`) i determinizm `DemoRoutePlanner`.

| Plik | Zakres |
|---|---|
| `EventsSmokeTests` | health, readiness, Events (lista, szczegoly, 400, 404) |
| `AttendanceSmokeTests` | zapis, idempotencja, zmiana trybu, wypis, 400/404 |
| `PulseSmokeTests` | KPI, podsumowanie, mapa heksagonow (GeoJSON, prywatnosc), ReturnGap (wydarzenie po 22:00 vs wczesniejsze, suma w `summary`), 400/404 |
| `RoutingSmokeTests` | trasa dla 4 trybow (`Demo`/`RoadRouting`), determinizm, `returnGap` na wydarzeniu poznym i wczesnym, 400/404 |
| `CrewSmokeTests` | lista grup, dolaczenie, ponowienie, pelna grupa (409), opuszczenie |
| `PulseUpdatedSmokeTests` | klient huba dostaje `PulseUpdated` po zapisie i po wypisie, razem z `participantsWithoutReturn` zgodnym z GET |

## Dane i sprzatanie

Testy uzywaja danych seedu (`SmokeSeed`): wydarzenie "Nocny Bieg" (`3333...`) i "Koncert na Rynku" (`1111...`), grupy `2222...`
(wolne miejsca) i `6666...` (pelna) oraz syntetyczny uzytkownik `d1000000-...-082`, ktory nie uczestniczy w wydarzeniu `3333...`
i nie nalezy do zadnej grupy, oraz uzytkownik klienta `aaaaaaaa-...` (`SmokeSeed.DemoUser`), ktory nie uczestniczy w zadnym
wydarzeniu (uzywany na wczesnym wydarzeniu `1111...` do testu trasy bez luki powrotowej).
Wydarzenie `3333...` konczy sie o 23:15, a `1111...` o 21:30, wiec tylko pierwsze ma luke powrotowa (`DemoReturnGapPolicy`).

- Testy nie zaleza od kolejnosci i dzialaja **sekwencyjnie** (wspolna baza, kolekcja `Smoke`).
- Przed i po kazdym tescie uzytkownicy testowi sa wypisywani z wydarzen i grup (operacje sa idempotentne), wiec kolejne
  uruchomienie zaczyna od stanu seedu.
- Jesli uzytkownik testowy nalezalby do seedu, sprzatanie usunelaby dane seedu. Test wykrywa to na starcie
  (porownuje liczniki przed i po resecie) i konczy sie bledem z instrukcja; przywroc seed restartem API
  (`docker restart <projekt>-api-1`) i popraw `SmokeSeed`.

## Relacja z `infra/smoke-test.ps1`

- **`infra/smoke-test.ps1`:** szybka kontrola przed pokazem bez SDK .NET (jeden scenariusz od health do sprzatania).
- **Ten zestaw:** dokladniejszy, z przypadkami brzegowymi (400/404/409, idempotencja, hub SignalR), uruchamiany przez `dotnet test`.
  Uzupelnia skrypt, nie zastepuje go.

Uruchamianie w CI jest opcjonalne i poza zakresem (wymaga uslugi Neo4j w workflow, patrz `TODO(#45)`).
