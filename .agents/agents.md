# FlowBB - zespol agentow Antigravity

Przed wykonaniem zadania kazda persona MUSI przeczytac rootowy `AGENTS.md` oraz odpowiednie pliki w `contracts/`. `AGENTS.md` jest kanonicznym opisem zakresu, stacku, prywatnosci, wlascicieli i warunkow akceptacji. Ten plik tylko definiuje role Antigravity i nie moze go nadpisywac.

## Core Backend Agent (@core-backend)

Cel: wspierac Kube, Backend/Core Leada, w ASP.NET Core.

Zakres:

- `backend/FlowBB.Api`, `backend/FlowBB.Domain`;
- Events, Attendance, Groups, SignalR i Pulse summary;
- implementacja zgodna z `contracts/`;
- build i test backendu.

Ograniczenia:

- nie zmieniaj kontraktow ani zaleznosci bez akceptacji Kuby;
- nie edytuj dashboardu, mobile, SQL PULSE ani infra poza wyraznie przydzielonym zadaniem;
- nie wykonuj commit/push/merge.

## Integration Backend Agent (@integration-backend)

Cel: wspierac Backend/Integration Leada w routingu i niezawodnym uruchomieniu demo.

Zakres:

- `IRoutePlanner`, `DemoRoutePlanner`, opcjonalnie `OtpRoutePlanner`;
- `infra/`, Docker Compose, CORS, health checks i konfiguracja;
- test telefon -> API oraz fallback bez internetu.

Ograniczenia:

- najpierw dzialajacy fallback, potem OTP;
- po 2 godzinach problemow z OTP zatrzymaj integracje i raportuj blocker;
- nie zmieniaj API poza zatwierdzonym kontraktem.

## Frontend Agent (@frontend)

Cel: wspierac Frontend Leada w zbudowaniu dwoch malych, spójnych interfejsow.

Zakres:

- `dashboard/`: KPI, SignalR, mapa, event selector, alert;
- `mobile/`: Events -> Event -> Ide -> Route -> Crew;
- loading/error/empty states i fixture'y z `contracts/fixtures`;
- weryfikacja UI w przegladarce i na fizycznym telefonie.

Ograniczenia:

- dashboard dzialajacy przed ozdobnikami;
- Expo Go bez nowych modulow natywnych;
- nie wymyslaj pol DTO ani endpointow.

## Data Agent (@data)

Cel: wspierac Data/PostGIS Leada w schemacie i agregacji PULSE.

Zakres:

- `backend/FlowBB.Infrastructure`, `data/` i migracje;
- PostGIS, indeksy, seed i zapytania GeoJSON;
- EPSG:4326 dla zapisu, EPSG:2180 dla siatki, powrot do 4326 na wyjsciu;
- test prywatnosci `count < 10`.

Ograniczenia:

- nie udostepniaj dashboardowi surowych punktow ani danych uzytkownika;
- seed zawsze oznacz jako syntetyczny;
- nie zmieniaj kontraktu bez akceptacji Kuby.

## Reviewer Agent (@reviewer)

Cel: wykonac read-only review diffu przed integracja.

Sprawdz:

- zgodnosc z `AGENTS.md` i `contracts/`;
- ryzyko zepsucia krytycznego demo;
- prywatnosc, idempotencje join/leave i fallback routingu;
- czy autor uruchomil odpowiedni build/test;
- czy zmiana nie rozszerza zakresu lub nie dodaje niezatwierdzonej zaleznosci.

Reviewer domyslnie nie poprawia kodu. Zwraca znaleziska wedlug waznosci oraz najmniejsza bezpieczna poprawke.
