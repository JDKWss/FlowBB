# FlowBB - instrukcje projektu dla agentow

## 1. Cel

Budujemy w 24 godziny dzialajacy prototyp dla tematu HackBB 2026 nr 3: "Bielsko-Biala 2030".

FlowBB laczy trzy moduly w jedna historie:

1. FLOW - mieszkaniec wybiera wydarzenie, klika "Ide" i dostaje propozycje dojazdu/powrotu.
2. CREW - mieszkaniec dolacza do bezpiecznej mikrogrupy zwiazanej z wydarzeniem.
3. PULSE - miasto widzi anonimowe, zagregowane sygnaly popytu transportowego.

Priorytety konkursowe: realna wartosc dla mieszkanca i miasta, mozliwosc wdrozenia, dzialajace demo i czytelna historia. Prostota i niezawodnosc sa wazniejsze niz liczba funkcji.

## 2. Krytyczny scenariusz demo

Demo musi przechodzic caly przeplyw bez recznego poprawiania danych:

1. Uzytkownik otwiera wydarzenie w aplikacji klienckiej w przegladarce.
2. Klika "Ide" i wybiera srodek transportu.
3. API zapisuje `AttendanceIntent` w PostgreSQL.
4. Backend przelicza agregaty.
5. SignalR wysyla `PulseUpdated`.
6. Dashboard bez odswiezania pokazuje zmiane licznika, np. `82 -> 83`.
7. Uzytkownik widzi trase z `IRoutePlanner`.
8. Uzytkownik dolacza do mikrogrupy CREW.
9. Dashboard pokazuje zagregowany popyt na mapie heksagonalnej.

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

- OpenTripPlanner z GTFS + OSM.
- Mapa w aplikacji klienckiej.
- Lepsze dopasowanie grup.
- Dodatkowe wydarzenia i filtry.

### Poza zakresem hackathonu

- Neo4j, LLM/AI w produkcie, rekomendacje ML.
- Pelne logowanie, OAuth, platnosci i zakup biletow.
- Chat i wiadomosci 1:1, push notifications.
- Osobna aplikacja natywna; `/client` pozostaje aplikacja webowa.
- GIOS jako zaleznosc krytyczna.

## 4. Zamrozony stack

- Backend: .NET 10, ASP.NET Core Minimal API, EF Core 10, Npgsql, NetTopologySuite, SignalR.
- Baza: PostgreSQL + PostGIS w Dockerze.
- Client: React, Vite, TypeScript; mobile-first aplikacja webowa.
- Dashboard: React, Vite, TypeScript.
- Routing: `IRoutePlanner` z `DemoRoutePlanner` jako zawsze dzialajacym fallbackiem; OTP 2 jako P1.
- Kontenery: Docker Compose.
- Demo: `/client` w mobilnym rozmiarze viewportu przegladarki, `/dashboard` w przegladarce desktopowej; cloudflared tylko jako awaryjny tunel do API.

Nie dodawaj produkcyjnej zaleznosci, frameworka, bazy ani zewnetrznej uslugi bez zgody Backend/Core Leada.

## 5. Struktura repozytorium

```text
flowbb/
|-- AGENTS.md
|-- CLAUDE.md
|-- README.md
|-- contracts/
|   |-- openapi.yaml
|   `-- fixtures/
|-- backend/
|   |-- FlowBB.Api/
|   |-- FlowBB.Domain/
|   `-- FlowBB.Infrastructure/
|-- client/
|-- dashboard/
|-- infra/
|   |-- docker-compose.yml
|   `-- otp/
`-- data/
    |-- seed/
    `-- gtfs/
```

Nie tworz dodatkowych projektow `.csproj`, warstw ani mikroserwisow bez konkretnej potrzeby P0.

## 6. Wlasciciele i granice pracy

### Backend/Core Lead - wlasciciel: Kuba

Kuba specjalizuje sie w C# i ASP.NET Core. Odpowiada za:

- architekture lekkiego backendu i kontrakty API;
- `Event`, `AttendanceIntent`, `Group`, `GroupMember`;
- endpointy Events, Attendance i Groups;
- SignalR `PulseHub` i zdarzenie `PulseUpdated`;
- integracje calego walking skeletonu;
- akceptacje zmian w `contracts/` i nowych zaleznosci;
- pilnowanie, aby `main` byl demonstracyjny i uruchamialny.

Kuba nie bierze na siebie budowania obu interfejsow. Pomaga frontendowi kontraktami, fixture'ami i klientem SignalR, ale nie przejmuje calego dashboardu.

### Backend/Integration Lead - wlasciciel: drugi programista C#

Odpowiada za:

- `IRoutePlanner`, `DemoRoutePlanner`, a nastepnie opcjonalnie `OtpRoutePlanner`;
- pobranie i walidacje GTFS/OSM oraz limit 2 godzin na uruchomienie OTP;
- Docker Compose, konfiguracje srodowiskowa, CORS i serwowanie buildu dashboardu z API;
- test polaczenia przegladarki `/client` -> API oraz awaryjny cloudflared;
- niezawodny fallback, gdy zewnetrzna usluga nie dziala.

### Frontend Lead - wlasciciel: programista frontend

Odpowiada za:

- `/client`: Events -> Event -> Ide -> Route -> Crew;
- `/dashboard`: KPI + SignalR + mapa + wybor wydarzenia + alerty transportowe i luki powrotowej;
- wspolny, spójny wyglad klienta i dashboardu;
- stany loading/error/empty potrzebne w demo;
- prace na fixture'ach od poczatku, bez czekania na gotowe API.

Najpierw dzialajacy dashboard i prosty `/client`, potem animacje i dopracowanie.

### Data/PostGIS Lead - wlasciciel: programista bazy danych

Odpowiada za:

- schemat, migracje, indeksy i typy przestrzenne;
- seed kontrolowany i powtarzalny;
- agregacje PULSE i GeoJSON;
- `ST_HexagonGrid` w EPSG:2180 oraz wynik GeoJSON w EPSG:4326;
- ukrywanie komorek z `count < 10`;
- gęsty seed w 3-4 obszarach lub rozmiar heksagonu 800-1000 m, aby mapa demo nie byla pusta;
- wspolprace z Backend/Core Leadem przy endpointach PULSE.

## 7. Kontrakty sa zrodlem prawdy

- `contracts/openapi.yaml` i `contracts/fixtures/` definiuja endpointy, DTO, enumy i przykladowe odpowiedzi.
- Tylko Backend/Core Lead akceptuje zmiane kontraktu.
- Agent nie zmienia nazw pol, sciezek ani enumow tylko po to, aby ulatwic lokalna implementacje.
- Gdy kontrakt jest niekompletny, zatrzymaj prace i opisz brak oraz najmniejsza proponowana zmiane.
- Frontend importuje lub odwzorowuje typy z kontraktu; nie tworzy drugiego, rozbieznego modelu domeny.

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

- Lokalizacje zapisuj jako `geometry(Point, 4326)`.
- Siatke heksagonalna licz po transformacji do EPSG:2180; GeoJSON zwracaj w EPSG:4326.
- Dashboard nie dostaje `userId`, surowych punktow ani indywidualnych tras. Dostaje tylko agregaty.
- Nie zwracaj komorki, gdy `count < 10`.
- Routing zawsze przechodzi przez `IRoutePlanner`; kod domenowy nie zalezy bezposrednio od OTP.
- `DemoRoutePlanner` musi dzialac bez internetu i pozostaje dostepny nawet po dodaniu OTP.
- Operacje join/leave maja byc bezpieczne przy ponowieniu i nie moga podwajac licznikow.
- Daty przesylaj jako ISO 8601; strefe demo ustal jawnie dla Bielska-Bialej.
- Sekretow, hasel i kluczy nie zapisuj w repo. Aktualizuj `.env.example`, nigdy `.env`.
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

- Backend: `dotnet build` oraz `dotnet test`, gdy projekt testowy istnieje.
- Dashboard: `npm run lint` i `npm run build`.
- Client: `npm run lint` i `npm run build`.
- Infra: `docker compose config` i test health endpointu API.
- SQL/PULSE: test, ze komorka 9-osobowa jest ukryta, a 10-osobowa jest zwracana.
- Walking skeleton: przegladarka `/client` -> API -> PostgreSQL -> SignalR -> dashboard `+1`.

Nie instaluj globalnych narzedzi ani nie aktualizuj lockfile bez potrzeby zadania.

## 11. Git i integracja

- `main` ma zawsze dzialac.
- Zalecane galezie: `feature/core-api`, `feature/routing-infra`, `feature/client-dashboard`, `feature/data-pulse`.
- Jeden czlowiek/agent pracuje w jednym worktree. Nie uruchamiaj dwoch piszacych agentow w tym samym katalogu.
- Commit ma obejmowac jedna logiczna zmiane i przejsc lokalna weryfikacje.
- Czlowiek czyta diff przed commitem i merge'em.
- Integracja odbywa sie czesto; nie trzymaj osmiu godzin zmian tylko lokalnie.

## 12. Kolejnosc realizacji i bramki

1. Do 45 min: repo, struktura, kontrakt, fixture'y, `/client` w mobilnym viewporcie przegladarki, PostGIS, API i dashboard uruchomione w przegladarce desktopowej.
2. Do 2 h: walking skeleton `Ide -> DB -> SignalR -> +1`.
3. Nastepnie rownolegle: PULSE, CREW, karta trasy i dopracowanie obu interfejsow.
4. OTP ma limit 2 godzin; po nim wracamy do `DemoRoutePlanner`.
5. Najpozniej 3,5 godziny przed prezentacja: feature freeze.
6. Po freeze: tylko bugfixy, backup demo, pitch i dwie proby z timerem.

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
- Wdrożeniowość: ASP.NET Core, PostgreSQL/PostGIS, GTFS i otwarte standardy.
- Innowacyjność: deklaracja „Idę” zamieniana w prognozę zapotrzebowania transportowego.
- Zaawansowanie kodu: SignalR, PostGIS, GeoJSON, routing z fallbackiem i aplikacja kliencka.
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
3. EF Core, SignalR, Npgsql i NetTopologySuite.
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
