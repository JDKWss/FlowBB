# FlowBB - instrukcje projektu dla agentow

## 1. Cel

Budujemy w 24 godziny dzialajacy prototyp dla tematu HackBB 2026 nr 3: "Bielsko-Biala 2030".

FlowBB laczy trzy moduly w jedna historie:

1. FLOW - mieszkaniec wybiera wydarzenie, klika "Ide" i dostaje propozycje dojazdu/powrotu.
2. CREW - mieszkaniec dolacza do bezpiecznej mikrogrupy zwiazanej z wydarzeniem.
3. PULSE - miasto widzi anonimowe, zagregowane sygnaly popytu transportowego.

Priorytety konkursowe: realna wartosc dla mieszkanca i miasta, mozliwosc wdrozenia, dzialajace demo i czytelna historia. Prostota i niezawodnosc sa wazniejsze niz liczba funkcji.

Decyzja o bazie danych: [docs/adr/001-runtime-persistence.md](docs/adr/001-runtime-persistence.md). Plan pracy zespolu: [docs/MVP_WORK_PLAN.md](docs/MVP_WORK_PLAN.md). Kontrakt danych grafu: [docs/NEO4J_CONTRACT.md](docs/NEO4J_CONTRACT.md).

## 2. Krytyczny scenariusz demo

Demo musi przechodzic caly przeplyw bez recznego poprawiania danych:

1. Uzytkownik otwiera wydarzenie w aplikacji klienckiej w przegladarce.
2. Klika "Ide" i wybiera srodek transportu.
3. API zapisuje w Neo4j relacje `IS_GOING_TO` (uzytkownik -> wydarzenie) ze snapshotem `TransportMode`, punktu startu i `UpdatedAt`.
4. Backend C# przelicza agregaty na podstawie zapisanych danych.
5. SignalR wysyla `PulseUpdated`.
6. Dashboard bez odswiezania pokazuje zmiane licznika, np. `82 -> 83`.
7. Uzytkownik widzi trase z `IRoutePlanner` (w MVP: deterministyczny `DemoRoutePlanner`).
8. Uzytkownik dolacza do mikrogrupy CREW.
9. Dashboard pokazuje zagregowany popyt na mapie heksagonalnej (agregacja w backendzie C#).

Jesli zmiana nie wspiera tego scenariusza, nie jest P0.

## 3. Zakres

### P0 - musi dzialac

- Lista i szczegoly seedowanych wydarzen.
- `POST /api/events/{eventId}/attendance` z idempotencja dla pary user-event.
- Aktualizacja PULSE przez SignalR.
- Jednostronicowy dashboard: KPI, wybor wydarzenia, alert luki powrotowej, mapa heksagonow.
- Prosta karta trasy tam i z powrotem z `DemoRoutePlanner`.
- Lista mikrogrup oraz dolaczenie/opuszczenie grupy.
- Seed demonstracyjny oznaczony w UI jako `DEMO DATA / SYMULACJA`.

### P1 - tylko po zamknieciu P0

- OpenTripPlanner z GTFS + OSM (wymaga kompletnego modelu tras, ktorego obecne dane MZK nie zawieraja).
- Mapa w aplikacji klienckiej.
- Lepsze dopasowanie grup.
- Dodatkowe wydarzenia i filtry.

### Poza zakresem hackathonu

- Druga baza runtime, EF Core i PostgreSQL/PostGIS jako baza aplikacji.
- LLM/AI w produkcie, rekomendacje ML.
- Pelne logowanie, OAuth, platnosci i zakup biletow.
- Chat i wiadomosci 1:1, push notifications.
- Osobna aplikacja natywna; `/client` pozostaje aplikacja webowa.
- GIOS jako zaleznosc krytyczna.
- Funkcje spolecznosciowe grafu niepotrzebne w scenariuszu demo (znajomi, obserwowanie lokali, tagi uzytkownikow, wlasciciele biznesowi), nawet jesli istnieja w kodzie lub seedzie Neo4j.

## 4. Zamrozony stack

- Backend: .NET 10, ASP.NET Core Minimal API, SignalR, Serilog (+ Seq), OpenAPI + Scalar.
- Baza runtime: Neo4j, sterownik `Neo4j.Driver`. Polaczenie przez zmienne `NEO4J_URI`, `NEO4J_DATABASE`, `NEO4J_USERNAME`, `NEO4J_PASSWORD`.
- Client: React, Vite, TypeScript; mobile-first aplikacja webowa.
- Dashboard: React, Vite, TypeScript.
- Routing: `IRoutePlanner` z `DemoRoutePlanner` jako zawsze dzialajacym fallbackiem; proponowany realny routing Walking/Bike/Car dziala w prywatnej usludze Python/FastAPI wywolywanej przez adapter Infrastructure. PublicTransport pozostaje osobnym problemem (OTP 2 jako ewentualne P1).
- Kontenery: Docker Compose.
- Demo: `/client` w mobilnym rozmiarze viewportu przegladarki, `/dashboard` w przegladarce desktopowej; cloudflared tylko jako awaryjny tunel do API.

Pakiety EF Core i Npgsql, ktore nadal sa w `FlowBB.Infrastructure.csproj`, sa pozostaloscia po wczesniejszym planie i nie naleza do stacku. Ich usuniecie to osobny maly task porzadkowy, wykonywany po potwierdzeniu, ze kod runtime ich nie uzywa (patrz `docs/MVP_WORK_PLAN.md`); do tego czasu nie korzystaj z nich w nowym kodzie.

Nie dodawaj produkcyjnej zaleznosci, frameworka, bazy ani zewnetrznej uslugi bez zgody Backend/Core Leada.

## 5. Struktura repozytorium

```text
flowbb/
|-- AGENTS.md
|-- CLAUDE.md
|-- START_HERE.md
|-- README.md
|-- .agents/
|   `-- agents.md
|-- contracts/
|   |-- openapi.yaml
|   `-- fixtures/
|-- backend/
|   |-- FlowBB.sln
|   |-- src/
|   |   |-- FlowBB.Domain/
|   |   |-- FlowBB.Application/
|   |   |-- FlowBB.Infrastructure/     (Neo4j/ - adaptery i repozytoria)
|   |   `-- FlowBB.Api/
|   `-- tests/
|       |-- FlowBB.Domain.Tests/
|       `-- FlowBB.Api.IntegrationTests/
|-- client/
|-- dashboard/
|-- database/
|   `-- flowbb-queries.cypher          (schemat i seed Neo4j)
|-- docs/
|   |-- adr/
|   |-- frontend.md
|   |-- MVP_WORK_PLAN.md
|   `-- NEO4J_CONTRACT.md
|-- infra/                             (docker-compose.yml, otp/ - do utworzenia)
`-- data/
    |-- seed/                          (snapshot wydarzen)
    `-- gtfs/mzk/                      (odseparowany PoC MZK, patrz sekcja 8)
```

Interfejsy wymagane przez Application trafiaja do `Application/Abstractions/`, a konkretne adaptery Neo4j do `Infrastructure/Neo4j/`. Domain nie zawiera repozytoriow ani zaleznosci od infrastruktury.

Nie tworz dodatkowych projektow `.csproj`, warstw ani mikroserwisow bez konkretnej potrzeby P0.

## 6. Wlasciciele i granice pracy

| Rola | Osoba | Odpowiedzialnosc |
|---|---|---|
| Core Backend Owner | Kuba | integracja backendu, `Program.cs`, SignalR, Attendance Application/API, PULSE API, `DemoRoutePlanner`, Docker Compose calej aplikacji, kontrakty, przeglad zmian |
| Backend Events | programista modulu Events | domena, Application i endpointy Events oraz implementacja `IEventLookup` |
| Data/Neo4j Owner | programista bazy danych | usluga Neo4j do Docker Compose, schemat, constraints, seed, Cypher, implementacje repozytoriow `Infrastructure/Neo4j` |
| Frontend | programista frontend | aplikacja kliencka, widoki, dashboard, klient REST i SignalR |

Rola integracyjna (routing i infrastruktura calej aplikacji) nalezy do Core Backend Ownera. Szczegoly podzialu: `docs/MVP_WORK_PLAN.md`.

### Core Backend Owner - wlasciciel: Kuba

Kuba specjalizuje sie w C# i ASP.NET Core. Odpowiada za:

- architekture lekkiego backendu i kontrakty API;
- akceptacje zmian w `contracts/` i nowych zaleznosci;
- integracje backendu, w tym `Program.cs` i walking skeleton;
- Attendance: warstwa Application i endpointy `POST`/`DELETE` Attendance;
- SignalR `PulseHub`, zdarzenie `PulseUpdated`, PULSE API i agregacje PULSE po stronie C#;
- `IRoutePlanner` i `DemoRoutePlanner` (routing MVP), w tym CORS i health check;
- Docker Compose na poziomie calej aplikacji oraz `.env.example` (wspolnie z Data/Neo4j w czesci Neo4j);
- Crew (domena i endpointy) do czasu wskazania innego wlasciciela;
- przeglad zmian innych obszarow;
- pilnowanie, aby `main` byl demonstracyjny i uruchamialny.

Kuba nie bierze na siebie budowania obu interfejsow. Pomaga frontendowi kontraktami, fixture'ami i klientem SignalR, ale nie przejmuje calego dashboardu.

### Backend Events

Odpowiada za:

- domene, warstwe Application i endpointy Events (`GET /api/events`, `GET /api/events/{eventId}`);
- implementacje `IEventLookup`, z ktorej korzysta Attendance;
- testy kontraktowe Events (na wlasnym branchu, do czasu gdy przechodza).

### Data/Neo4j Owner

Odpowiada za:

- przygotowanie uslugi Neo4j do Docker Compose (wlaczenie do calego Compose robi Core Backend Owner);
- schemat Neo4j, constraints i indeksy;
- seed kontrolowany, powtarzalny i oznaczony jako syntetyczny;
- zapytania Cypher oraz implementacje repozytoriow w `Infrastructure/Neo4j`;
- gesty seed uzytkownikow ze wspolrzednymi domowymi w 3-4 obszarach, aby mapa demo nie byla pusta;
- konsultacje konfiguracji Neo4j (`NEO4J_*`, wersja, wolumeny) i wspolprace z Core Backend Ownerem przy odczytach potrzebnych PULSE.

Data/Neo4j Owner nie odpowiada za logike routingu, API ani agregacje PULSE.

### Frontend - wlasciciel: programista frontend

Odpowiada za:

- `/client`: Events -> Event -> Ide -> Route -> Crew;
- `/dashboard`: KPI + SignalR + mapa + wybor wydarzenia + alerty transportowe i luki powrotowej;
- wspolny, spojny wyglad klienta i dashboardu;
- stany loading/error/empty potrzebne w demo;
- prace na fixture'ach od poczatku, bez czekania na gotowe API.

Najpierw dzialajacy dashboard i prosty `/client`, potem animacje i dopracowanie.

### Agenci AI

Claude i Codex wspieraja wlasciciela danego obszaru, ale nie przejmuja odpowiedzialnosci innej osoby bez wyraznego polecenia. W szczegolnosci:

- agent pracujacy nad Attendance nie implementuje Events;
- agent backendowy nie projektuje samodzielnie schematu Neo4j;
- agent nie dodaje EF Core ani drugiej bazy;
- agent nie zmienia kontraktu OpenAPI bez uzgodnienia z Core Backend.

## 7. Kontrakty sa zrodlem prawdy

- `contracts/openapi.yaml` i `contracts/fixtures/` definiuja endpointy, DTO, enumy i przykladowe odpowiedzi.
- Tylko Core Backend akceptuje zmiane kontraktu.
- Agent nie zmienia nazw pol, sciezek ani enumow tylko po to, aby ulatwic lokalna implementacje.
- Gdy kontrakt jest niekompletny, zatrzymaj prace i opisz brak oraz najmniejsza proponowana zmiane.
- Frontend importuje lub odwzorowuje typy z kontraktu; nie tworzy drugiego, rozbieznego modelu domeny.
- Kontrakt danych w Neo4j (wezly, relacje, pola, wlasciciele) opisuje `docs/NEO4J_CONTRACT.md`. Nie zmienia on kontraktu API.

### Dokumentacja frontendu

- Dla zadan w `/client` lub `/dashboard` przeczytanie `docs/frontend.md` jest obowiazkowe; dokument uzupelnia `AGENTS.md`.
- Przed dodaniem zaleznosci frontendowej sprawdz odpowiedni `package.json` i `docs/frontend.md`.
- `contracts/openapi.yaml` pozostaje zrodlem prawdy dla kontraktow API i ksztaltow DTO.

Minimalne endpointy:

```text
GET    /api/events
GET    /api/events/{eventId}
POST   /api/events/{eventId}/attendance
DELETE /api/events/{eventId}/attendance/{userId}
GET    /api/events/{eventId}/groups
POST   /api/groups/{groupId}/members
DELETE /api/groups/{groupId}/members/{userId}
GET    /api/events/{eventId}/route?userId={userId}
GET    /api/pulse/summary
GET    /api/pulse/events/{eventId}
GET    /api/pulse/hexagons?eventId={eventId}
HUB    /hubs/pulse
EVENT  PulseUpdated
```

## 8. Reguly techniczne

### Prywatnosc i agregaty PULSE

- Dashboard nie dostaje `userId`, surowych punktow ani indywidualnych tras. Dostaje tylko agregaty.
- Publiczne API nie zwraca dokladnej lokalizacji uzytkownika.
- Nie zwracaj komorki, gdy `count < 10`.
- Agregacja PULSE (licznik, modal split, siatka heksagonalna) jest wykonywana w backendzie C# na podstawie wspolrzednych pobranych wewnetrznie z Neo4j. Licznikow nie przechowuje sie jako niezaleznych pol; wylicza sie je z relacji `IS_GOING_TO`.
- Siatke heksagonalna licz w ukladzie metrycznym (EPSG:2180 lub rownowaznym); GeoJSON zwracaj w EPSG:4326. Rozmiar heksagonu 800-1000 m, aby mapa demo nie byla pusta.

### Persystencja: Neo4j

- Neo4j jest jedyna baza runtime aplikacji w MVP. Przechowuje dane Events, Attendance, Crew oraz dane zrodlowe do agregacji PULSE. Nie wprowadzamy drugiej bazy runtime dla tych samych funkcji.
- Uzytkownik ma wewnetrzne, demonstracyjne `DefaultOriginLatitude` i `DefaultOriginLongitude`. Relacja `IS_GOING_TO` przechowuje snapshot: `TransportMode`, `OriginLatitude`, `OriginLongitude`, `UpdatedAt`. Wspolrzedne sa danymi wewnetrznymi i nie opuszczaja backendu.
- Na granicy Application/API identyfikatory sa typu `Guid`. Adapter Neo4j moze przechowywac je jako string i odpowiada za konwersje.
- Idempotencja Attendance: `MERGE` relacji dla pary user-event i unikalne constraints wezlow. Zapis i odczyt danych do komunikatu `PulseUpdated` wykonuj w jednej transakcji; wiadomosc publikuj dopiero po jej zatwierdzeniu.
- Repozytoria i zapytania Cypher naleza do `Infrastructure/Neo4j`. Nie tworz generycznego `Repository<TEntity>`.
- Schemat, constraints i seed: `database/flowbb-queries.cypher` (wlasciciel: Data/Neo4j).

### Persystencja: PostgreSQL/PostGIS (odseparowany PoC)

- Kod i materialy PostGIS w `data/gtfs/mzk/` to odseparowany PoC importu i analizy rozkladow MZK. Nie jest baza aplikacji.
- PostGIS nie przechowuje Attendance ani Events aplikacji i nie wymaga synchronizacji z Neo4j.
- Nie prezentuj PostGIS agentom ani w dokumentacji jako obowiazujacej bazy runtime. PoC zostaje w repozytorium, ale nie jest zaleznoscia backendu.

### Dane MZK i routing

- Obecne dane MZK (`data/gtfs/mzk/parsed/`) to odjazdy z przystankow. Nie zawieraja jeszcze pelnych kursow (trips), kolejnosci przystankow, kompletnego powiazania kursow ani wspolrzednych wszystkich przystankow.
- Nie opisuj ich jako kompletnego systemu routingu. MVP uzywa deterministycznego `DemoRoutePlanner` jako rozwiazania zastepczego. Dane MZK moga pozniej wzbogacac informacje transportowe.
- Proponowany realny routing drogowy jest prywatna usluga Python/FastAPI w tym samym Docker Compose. Tylko ASP.NET komunikuje sie z nia przez wewnetrzny REST; przegladarka nigdy nie wywoluje jej bezposrednio. Szczegoly: `docs/ROUTING_SERVICE.md` i proponowany ADR 002.

### Ogolne

- Routing zawsze przechodzi przez `IRoutePlanner`; Domain i Application nie zaleza od FastAPI, biblioteki grafowej ani OTP.
- `DemoRoutePlanner` musi dzialac bez internetu i pozostaje dostepny jako kontrolowany fallback. Bledow logicznych, takich jak nieprawidlowy tryb lub brak trasy, nie wolno ukrywac jako danych demo.
- Operacje join/leave maja byc bezpieczne przy ponowieniu i nie moga podwajac licznikow.
- Daty przesylaj jako ISO 8601; strefe demo ustal jawnie dla Bielska-Bialej.
- Sekretow, hasel i kluczy nie zapisuj w repo (w tym `NEO4J_PASSWORD`). Aktualizuj `.env.example`, nigdy `.env`.
- Dane demonstracyjne zawsze oznaczaj jako syntetyczne.

## 9. Sposob pracy agentow

Przed edycja:

1. Przeczytaj ten plik, kontrakt i pliki w obszarze zadania.
2. Sprawdz `git status`; nie nadpisuj cudzych zmian.
3. Podaj krotki plan, pliki do zmiany i kryterium akceptacji.
4. Jesli zadanie przekracza jeden modul lub wymaga zmiany kontraktu, popros wlasciciela o decyzje.

Podczas pracy:

- Realizuj jedno male zadanie naraz.
- Edytuj tylko przypisany folder oraz uzgodnione pliki wspolne.
- Nie wykonuj `git commit`, `git push`, merge ani rebase bez wyraznego polecenia czlowieka.
- Nie uruchamiaj destrukcyjnych komend ani masowych zmian formatowania.
- Nie zmieniaj architektury przy okazji naprawy lokalnego bledu.
- Preferuj najprostsza implementacje spelniajaca kontrakt i demo.

Po pracy:

1. Uruchom odpowiednie testy i build.
2. Pokaz liste zmienionych plikow.
3. Podaj wynik polecen weryfikacyjnych.
4. Wymien pozostale ryzyka, TODO i zalozenia.
5. Nie deklaruj sukcesu, jesli build lub scenariusz akceptacyjny nie zostal uruchomiony.

## 10. Minimalna weryfikacja

- Backend: `dotnet build backend/FlowBB.sln` oraz `dotnet test backend/FlowBB.sln`, gdy projekt testowy istnieje.
- Dashboard: `npm run lint` i `npm run build`.
- Client: `npm run lint` i `npm run build`.
- Infra: `docker compose config` i test health endpointu API.
- PULSE: test, ze komorka 9-osobowa jest ukryta, a 10-osobowa jest zwracana.
- Neo4j: testy adapterow na prawdziwej instancji Neo4j; nie zastepuj ich atrapa bazy, gdy sprawdzasz idempotencje i constraints.
- Walking skeleton: przegladarka `/client` -> API -> Neo4j -> SignalR -> dashboard `+1`.

Nie instaluj globalnych narzedzi ani nie aktualizuj lockfile bez potrzeby zadania.

## 11. Git i integracja

- `main` ma zawsze dzialac. Integracja odbywa sie przez `develop`; `develop` ma byc zielony (build i testy przechodza).
- Jeden czlowiek/agent pracuje w jednym worktree i na jednym branchu. Nie uruchamiaj dwoch piszacych agentow w tym samym katalogu.
- Nowy worktree: `git worktree add -b <branch> <katalog-obok-repo> origin/develop`. Nie usuwaj cudzych worktree ani branchy i nie uzywaj `--force`.
- Zalecane galezie: `feature/<obszar>-<temat>`, np. `feature/attendance-pulse`, `feature/routing-mzk`, `chore/<temat>` dla dokumentacji.
- Czerwone testy (np. testy kontraktowe Events) zostaja na branchu wlasciciela obszaru, nie trafiaja osobno na `develop`.
- Commit ma obejmowac jedna logiczna zmiane i przejsc lokalna weryfikacje.
- Czlowiek czyta diff przed commitem i merge'em.
- Integracja odbywa sie czesto; nie trzymaj osmiu godzin zmian tylko lokalnie.

## 12. Kolejnosc realizacji i bramki

Szczegolowe bramki, zaleznosci i kryteria akceptacji: [docs/MVP_WORK_PLAN.md](docs/MVP_WORK_PLAN.md).

1. Dokumentacja i architektura spojne, `develop` zielony.
2. Crew Domain scalone po przejsciu testow.
3. Schemat Neo4j ma pola wymagane przez Events i Attendance.
4. Events dziala i udostepnia stabilny kontrakt (`IEventLookup`) dla Attendance.
5. Attendance jest idempotentne i integruje sie z SignalR (walking skeleton `Ide -> Neo4j -> SignalR -> +1`).
6. Frontend obsluguje dashboard oraz aktualizacje `count + 1`.
7. Routing MVP korzysta z `DemoRoutePlanner`; realny routing drogowy wymaga osobnego spike'a uslugi FastAPI. PublicTransport/OTP pozostaje osobnym P1, a po przekroczeniu limitu prac wracamy do `DemoRoutePlanner`.
8. PULSE spelnia regule prywatnosci `count >= 10`.
9. Najpozniej 3,5 godziny przed prezentacja: feature freeze.
10. Po freeze: tylko bugfixy, backup demo, pitch i dwie proby z timerem.

## 13. Ograniczenie regulaminowe

Kod aplikacji powstaje w oficjalnym oknie hackathonu. Przed startem wolno przygotowac srodowisko, dokumentacje, kontrakty i dane tylko w zakresie dozwolonym przez regulamin/mentora. W razie watpliwosci pytamy organizatora przed generowaniem kodu produktu.

## 14. Kryteria oceny HackBB 2026

Każdą funkcję i decyzję techniczną oceniaj według poniższych kryteriów:

| Kryterium | Waga |
|---|---:|
| Wartość biznesowa | 2.0 |
| Możliwości wdrożeniowe | 2.0 |
| Innowacyjność | 1.5 |
| Zaawansowanie kodu | 1.5 |
| Kreatywność | 1.0 |
| Łatwość użytkowania | 1.0 |
| UX/UI | 0.5 |
| Prezentacja projektu | 0.5 |

### Kolejność priorytetów

1. Rozwiązanie realnej potrzeby mieszkańca i miasta.
2. Działający prototyp możliwy do dalszego wdrożenia.
3. Technicznie przekonujące FLOW → CREW → PULSE.
4. Innowacyjne wykorzystanie danych o przyszłym popycie.
5. Intuicyjny scenariusz użytkownika.
6. Wygląd i dodatkowe funkcje.

### Zasada podejmowania decyzji

Przed dodaniem funkcji odpowiedz:

- Jaką potrzebę użytkownika lub miasta rozwiązuje?
- Które kryterium konkursowe wzmacnia?
- Czy będzie widoczna podczas 10-minutowej prezentacji?
- Czy zdążymy ją ukończyć i przetestować?
- Czy nie zagraża działaniu scenariusza P0?

Jeżeli funkcja nie wzmacnia demonstracyjnie żadnego kryterium albo zagraża P0,
nie implementuj jej przed zakończeniem podstawowego scenariusza.

### Najważniejsze elementy punktowane w FlowBB

- Wartość biznesowa: mieszkaniec łatwiej dociera na wydarzenie, a miasto poznaje przyszły popyt.
- Wdrożeniowość: ASP.NET Core, Neo4j, dane MZK/GTFS jako kierunek rozwoju i otwarte standardy.
- Innowacyjność: deklaracja „Idę” zamieniana w prognozę zapotrzebowania transportowego.
- Zaawansowanie kodu: SignalR, Neo4j, agregacja przestrzenna i GeoJSON, routing z fallbackiem i aplikacja kliencka.
- Kreatywność: połączenie FLOW, CREW i PULSE w jeden obieg danych.
- Łatwość użytkowania: jeden prosty przebieg od wydarzenia do trasy i grupy.
- UX/UI: czytelny mobilny widok `/client` i efektowny dashboard heksagonalny.
- Prezentacja: kliknięcie „Idę” w mobilnym viewporcie `/client` powoduje zmianę licznika na żywo.

## 15. Jakosc kodu i SonarQube

Kod C# musi przechodzic analize Sonar bez nowych problemow o waznosci
Critical, Blocker lub Major w zmienionym kodzie.

### Cognitive Complexity

- Docelowa Cognitive Complexity metody: maksymalnie 10.
- Bezwzgledny limit metody: 15 zgodnie z Sonar S3776.
- Wlasciwosci i accessory: maksymalnie 3.
- Metoda z wynikiem 11-15 wymaga sprawdzenia mozliwosci uproszczenia.
- Nie obchodz limitu przez tworzenie wielu bezsensownych metod jednozdaniowych.
- Preferuj guard clauses, early return i plaskie instrukcje zamiast zagniezdzonych ifow.
- Maksymalnie 2-3 poziomy zagniezdzenia.

### Dlugosc i odpowiedzialnosc metod

- Docelowo metoda powinna miec do 30-40 linii kodu.
- Nie przekraczaj 50 linii bez konkretnego uzasadnienia.
- Kazda metoda powinna realizowac jedna jasno nazwana odpowiedzialnosc.
- Dlugie mapowanie DTO, konfiguracja DI, migracje i wygenerowany kod moga byc
  wyjatkiem, jezeli rozbicie pogorszyloby czytelnosc.
- Nie tworz klas typu Manager lub Helper skupiajacych niepowiazane operacje.
- Nie dodawaj abstrakcji, interfejsu ani wzorca projektowego bez realnej potrzeby.

### Reakcja na SonarQube

- Najpierw sprawdz, czy zalecenie Sonara jest poprawne w kontekscie kodu.
- Naprawiaj nowe problemy w kodzie zmienianym w ramach zadania.
- Nie wykonuj szerokiego refaktoringu starego kodu poza zakresem zadania.
- Nie dodawaj `NOSONAR`, `SuppressMessage` ani wylaczenia reguly bez zgody czlowieka.
- Jesli wynik Sonara jest prawdopodobnym false positive, nie ukrywaj go.
  Opisz powod, ryzyko i proponowane rozwiazanie.
- Poprawka nie moze zmieniac kontraktu API ani zachowania biznesowego bez zgody.

### Ponowne wykorzystanie istniejacych rozwiazan

Przed napisaniem nowego mechanizmu sprawdz kolejno:

1. Istniejacy kod w repozytorium.
2. Biblioteki standardowe .NET i ASP.NET Core.
3. Neo4j.Driver, SignalR, Serilog i Scalar/OpenAPI (zatwierdzone zaleznosci backendu).
4. Juz zatwierdzone zaleznosci projektu.
5. Dopiero potem stabilny i utrzymywany pakiet NuGet.

Nie implementuj od zera standardowych mechanizmow, takich jak:
serializacja, retry, obsluga GeoJSON, walidacja UUID, obsluga HTTP,
logowanie, mapowanie geometrii lub klient SignalR, jezeli zapewnia je
framework albo zatwierdzona biblioteka.

Nie dodawaj nowego NuGeta bez zgody Backend/Core Leada. Przed dodaniem podaj:

- nazwe i wersje pakietu;
- problem, ktory rozwiazuje;
- dlaczego BCL lub obecne zaleznosci nie wystarczaja;
- licencje i aktywnosc projektu;
- liczbe oraz ryzyko zaleznosci przechodnich;
- prostsza alternatywe bez nowego pakietu.

Wlasny kod wybierz, gdy logika jest specyficzna dla domeny FlowBB albo jest
prostsza i bezpieczniejsza niz wprowadzanie dodatkowej zaleznosci.

### Weryfikacja

Po kazdej zmianie backendu uruchom:

- `dotnet build`
- `dotnet test`, jezeli istnieja testy
- analize Sonar dla zmienionego kodu

W raporcie koncowym podaj nowe ostrzezenia Sonara albo potwierdz ich brak.
