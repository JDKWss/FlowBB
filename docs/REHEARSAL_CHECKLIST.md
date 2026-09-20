# Checklista prob demo z timerem (issue #57)

Dokument roboczy do odhaczania podczas dwoch pelnych przebiegow scenariusza z `docs/DEMO_RUNBOOK.md` (sekcja 4).
Wyniki koncowe (czasy, problemy) przepisz do runbooka jako nowa sekcja, a kazda rozbieznosc zapisz jako osobne issue.
Dane w demo sa syntetyczne (`DEMO DATA / SYMULACJA`).

**Obsada:** jedna osoba prowadzi (klika), druga mierzy czas i zapisuje uwagi. Wszystkie cztery role sa w probie
(kryterium #57), ale wystarczy jeden komputer.

## 0. Przed pierwsza proba (jednorazowo, ok. 15 min)

- [ ] `git switch develop && git pull`, sprawdz, ze ostatni Backend CI i Neo4j Integration na `develop` sa zielone.
- [ ] `.env` z `.env.example` (`NEO4J_*`, `NEO4J_SEED_ON_STARTUP=true`; przy zajetym porcie 5341 ustaw `SEQ_HOST_PORT`).
- [ ] Komendy kopiuj **prosto z runbooka** (kryterium: „komendy dzialaja po skopiowaniu"); kazda, ktora nie dziala, to rozbieznosc.
- [ ] Czyste srodowisko: `docker compose -f infra/docker-compose.yml --env-file .env --profile local-db down -v`
- [ ] Start: `docker compose -f infra/docker-compose.yml --env-file .env --profile local-db up --build -d`
- [ ] `GET http://localhost:8080/health` -> 200, `GET http://localhost:8080/health/ready` -> 200.
- [ ] `pwsh infra/smoke-test.ps1 -BaseUrl http://localhost:8080` -> **13 PASS / 0 FAIL / 0 SKIP**.
- [ ] Smoke C# (opcjonalnie, kryterium testowe #57): `FLOWBB_SMOKE_BASE_URL=http://localhost:8080 dotnet test backend/FlowBB.sln --filter "FullyQualifiedName~Smoke"`.
      Nie uruchamiaj go rownolegle z testami Neo4j na tej samej bazie.
- [ ] `cd client && npm ci && VITE_API_URL=http://localhost:8080 npm run dev` (zwykle port 5173).
- [ ] `cd dashboard && npm ci && VITE_API_URL=http://localhost:8080 npm run dev -- --port 5174`.
- [ ] Przegladarka: dashboard na duzym oknie, `/client` w mobilnym viewporcie (np. 390x844, DevTools).
- [ ] Nagrywanie ekranu i narzedzie do zrzutow gotowe (np. OBS albo wbudowane w system).
- [ ] Stoper (telefon lub `Stopwatch`), tabela wynikow (sekcja 4) otwarta obok.

## 1. Stan wyjsciowy przed KAZDA proba

Seed przywraca stan przy restarcie API, wiec miedzy probami zrob:

- [ ] `docker restart infra-api-1` (nazwa kontenera zalezy od `-p`; sprawdz `docker ps`), poczekaj na `healthy`.
- [ ] Dashboard po odswiezeniu pokazuje dla „Koncert na Rynku" licznik **82** (a w `/client` uzytkownik `dddddddd-...` nie ma
      deklaracji ani grupy).
- [ ] Nikt inny nie ma otwartego dashboardu na innym stanie (dwa okna moga mylic obserwacje SignalR).

## 2. Scenariusz z timerem (uruchom stoper przy pierwszym kroku)

Kolumny „Czas" wypelnij przy kazdym kroku (stoper narastajaco). Cel calego przebiegu: **do 10 minut**, z zapasem.

| # | Krok | Oczekiwany rezultat | OK? | Czas |
|---|---|---|---|---|
| 1 | Dashboard: wybierz „Koncert na Rynku" | KPI, licznik **82**, mapa heksagonow, oznaczenie `DEMO DATA / SYMULACJA` | ☐ | |
| 2 | `/client`: otworz to samo wydarzenie | Lista i szczegoly, karta jakosci powietrza (pkt 3 ponizej) | ☐ | |
| 3 | Kliknij „Ide", wybierz **Walking** | Odpowiedz 200, `isNew: true` | ☐ | |
| 4 | Patrz na dashboard **bez odswiezania** | Licznik **82 -> 83**, modal split Walking `16 -> 17` | ☐ | |
| 5 | Kliknij „Ide" jeszcze raz (ten sam tryb) | Licznik **bez zmian** (idempotencja) | ☐ | |
| 6 | Karta trasy (tam i z powrotem) | Walking: `RoadRouting`, dystans i linia po drogach; brak bledu | ☐ | |
| 7 | Zmien tryb na **PublicTransport** | Trasa z oznaczeniem `Demo`; modal split sie zmienia, licznik bez zmian | ☐ | |
| 8 | Dolacz do mikrogrupy Crew | Licznik czlonkow +1; ponowne dolaczenie bez zmiany | ☐ | |
| 9 | Dashboard: mapa PULSE | Komorki heksagonalne >= 10 osob, brak `userId` i dokladnych punktow | ☐ | |
| 10 | Dashboard: „Nocny Bieg na Blonich" | `participantsWithoutReturn` **10**, alert `ReturnGap` (Warning); w `/client` trasa PublicTransport ma `returnGap` i brak powrotow. **Widok alertu ocen wzrokowo** (nigdy nie byl potwierdzony w przegladarce) | ☐ | |
| 11 | Karta jakosci powietrza (Client i Dashboard) | Stan `Fresh`/`Stale` z podpisem zrodla GIOS albo `Fallback` z „dane demonstracyjne FlowBB"; brak bledu widoku | ☐ | |
| 12 | (Opcja) Dashboard: `Add event` -> klik na mapie -> `Create event` | 201, nowe wydarzenie widoczne w `/client` | ☐ | |

Stopklatka: **czas calkowity** wpisz w tabeli z sekcji 4.

## 3. Dodatkowe sprawdzenia (poza scenariuszem, ok. 5 min, tylko w pierwszej probie)

- [ ] **Reconnect SignalR:** zatrzymaj API (`docker stop <api>`), dashboard pokazuje `Reconnecting`; uruchom API, dashboard wraca do `Live`
      bez recznego odswiezenia i licznik jest aktualny.
- [ ] **Brak internetu:** wylacz siec; API i dane dzialaja (`DemoRoutePlanner`), a jedynie kafle mapy moga zniknac (znane ograniczenie).
- [ ] **Karta jakosci powietrza bez GIOS:** przy odcietej sieci karta pokazuje `Fallback`, reszta widoku dziala.
- [ ] **RoadRouting z prawdziwymi grafami** (opcja, wymaga sieci przy przygotowaniu, ok. 2 min):
      `--profile routing-tools run --rm routing-prepare`, w `.env` `ROUTING_MODE=RoadRouting`, `up` z `--profile real-routing`;
      trasy Walking/Bike/Car maja `plannerSource: RoadRouting`, a zatrzymanie kontenera `routing` daje kontrolowany fallback `Demo`.
- [ ] **Atrybucja OSM** przy trasach drogowych w kliencie (#128, jesli juz wdrozone).
- [ ] Na `/client` i `/dashboard` brak `userId`, wspolrzednych domowych i tras w widocznym UI i w zakladce Network (zakladka PULSE).

## 4. Wyniki (uzupelnij i przepisz do `docs/DEMO_RUNBOOK.md`)

| | Proba 1 | Proba 2 |
|---|---|---|
| Data, godzina | | |
| Sprzet (komputer, przegladarka) | | |
| Kroki 1-9 (czas) | | |
| Kroki 10-12 (czas) | | |
| **Czas calkowity** | | |
| Kroki, ktore zawiodly (nr) | | |
| Rzeczy, ktore trzeba bylo poprawiac recznie | | |

Cel jakosciowy: proba 2 bez recznego poprawiania danych i bez kroku, ktory zawodzi; czas calkowity <= 10 min.

## 5. Nagranie zapasowe i zrzuty (kryterium #57)

- [ ] Nagranie pelnego przebiegu (sekcja 2, kroki 1-10), najlepiej z proby 2, zapisane poza repozytorium (np. dysk zewnetrzny + chmura zespolu).
- [ ] Zrzuty: dashboard z licznikiem `82`, dashboard po kliknieciu z `83`, mapa PULSE, alert `ReturnGap`, `/client` (lista, „Ide", trasa, Crew), karta jakosci powietrza (jeden stan).
- [ ] Nie umieszczaj nagran ani zrzutow w repozytorium (rozmiar, ewentualne dane); w runbooku zapisz tylko miejsce ich przechowywania.

## 6. Po probach

- [ ] Przepisz wyniki (sekcja 4) do `docs/DEMO_RUNBOOK.md` jako nowa sekcja „Proby z timerem (#57)" oraz odhacz ostatni punkt sekcji 9.
- [ ] Kazda rozbieznosc i awaria: **osobne issue** (numer wpisz tutaj): ____________________
- [ ] Komentarz w #57 z linkiem do PR z wynikami, potem zamkniecie #57 i odblokowanie #93.
- [ ] Sprzatanie: `docker compose -f infra/docker-compose.yml --env-file .env --profile local-db down -v` (dodaj `--profile real-routing`, jesli uzywales).
