# Modul PULSE

Widok miasta: zagregowany popyt transportowy. Wlasciciel: Core Backend (Kuba). Issues #5, #20.
**Status: kod na `develop`, nie podpiety w `Program.cs`, brak adaptera Neo4j (#15).**

## Endpointy

| Metoda | Sciezka | Zwraca |
|---|---|---|
| `GET` | `/api/pulse/summary` | KPI calego miasta |
| `GET` | `/api/pulse/events/{eventId}` | KPI wydarzenia, 404 gdy nie istnieje |
| `GET` | `/api/pulse/hexagons?eventId=...` | GeoJSON `FeatureCollection`, `application/geo+json` |

`hexagons` bez `eventId` zwraca 400, a nie pusta liste - brak parametru to blad klienta.

## Reguly prywatnosci

Te trzy zasady sa powodem istnienia calego modulu i nie wolno ich obejsc "na potrzeby demo":

1. **Prog 10 osob.** `GetActivityMapHandler.MinCellParticipants = 10`. Komorka z 9 osobami
   nie jest zwracana wcale - nie jest maskowana, nie jest zaokraglana, po prostu jej nie ma.
2. **Zero identyfikatorow.** Zaden DTO w `PulseResponses.cs` nie ma pola `userId`.
   `PulsePoint` (wspolrzedne + tryb) jest typem wewnetrznym i nie ma odpowiednika w odpowiedzi.
3. **Zero surowych punktow.** Na zewnatrz wychodza tylko wierzcholki heksagonow i liczniki.

Osoby nieprzypisane do zadnej komorki (bo ich komorka ma < 10 osob) **nadal licza sie**
do `participantsCount` calosci. Suma z komorek moze wiec byc mniejsza niz licznik glowny -
to zamierzone, nie blad zaokraglenia.

## Siatka heksagonalna

`Application/Pulse/GetActivityMap/HexGrid.cs`. Heksagony pointy-top we wspolrzednych osiowych
(`q`, `r`), identyfikator komorki `hex-{q}-{r}`.

| Parametr | Wartosc |
|---|---|
| Szerokosc komorki | 900 m (`DefaultCellWidthMeters`) |
| Punkt odniesienia | 49.8225 N, 19.0444 E (Rynek w Bielsku-Bialej) |
| Uklad obliczen | lokalny rzut rownoodlegly wokol punktu odniesienia |
| Uklad wyjscia | EPSG:4326, GeoJSON zgodny z RFC 7946 (`[lon, lat]`, pierscien zamkniety) |

**Odstepstwo od `AGENTS.md`:** sekcja 8 mowi o EPSG:2180 "lub rownowaznym". Kod uzywa wlasnego
rzutu metrycznego zamiast prawdziwego ukladu 2180. Dla skali miasta blad jest pomijalny wobec
komorki 900 m, ale warto o tym wiedziec, zanim ktos porowna wyniki z danymi GUGiK.

Zaokraglanie do komorki (`HexGrid.Round`) uzywa standardowego algorytmu cube-rounding:
zaokragla `q`, `r`, `s = -q-r` niezaleznie, a nastepnie koryguje te wspolrzedna, ktora
przesunela sie najbardziej. Bez tej korekty punkty przy krawedziach ladowalyby w zlych komorkach.

## Handlery

| Handler | Robi | Uwagi |
|---|---|---|
| `GetActivityMapHandler` | grupuje punkty po komorce, odrzuca `< 10`, sortuje po `q`, potem `r` | deterministyczna kolejnosc wyniku |
| `GetPulseHexagonsHandler` | sprawdza istnienie wydarzenia, potem deleguje do powyzszego | rozroznia "brak wydarzenia" (404) od "brak komorek" (pusta kolekcja) |
| `GetEventPulseHandler` | KPI wydarzenia | `participantsWithoutReturn` = stala `0` do czasu polityki z #85; regula jest zamrozona ponizej (#84) |
| `GetPulseSummaryHandler` | KPI calego miasta | patrz nizej |

`ModalSplit.From(points)` liczy rozklad srodkow transportu jednym przebiegiem; wszystko,
co nie jest znanym trybem, laduje w `Unknown`.

**Dlug techniczny w `GetPulseSummaryHandler`:** wola `GetEventsAsync`, a potem `GetPointsAsync`
w petli po kazdym wydarzeniu - klasyczne N+1. Przy kilkunastu wydarzeniach demo to nie problem,
ale to pierwsze miejsce do poprawy, jesli seed urosnie.

## Alerty i luka powrotowa

Regula demo jest **zamrozona** (#84), zeby Data (odczyt `EndAt`, #86) i Backend (polityka, #85) pracowali rownolegle.
Logika jeszcze nie istnieje: do czasu #85 `participantsWithoutReturn` jest stala `0`
(`GetEventPulseHandler.ParticipantsWithoutReturnInMvp`), `EventPulseResponse.Alerts` jest pusta (`[]`),
a trasa zwraca `returnGap: false`. Dashboard musi umiec pokazac pusty stan alertow.

**Regula (MVP, symulacja `DEMO DATA / SYMULACJA`):**

| Element | Ustalenie |
|---|---|
| Kto nie ma dogodnego powrotu | uczestnik z `TransportMode = PublicTransport` |
| Kiedy | wydarzenie konczy sie o **22:00 lub pozniej** czasu lokalnego `Europe/Warsaw` |
| Wejscie | `PulseEventInfo.EndAt` (`DateTimeOffset?`); `EndAt` jest przeliczany na `Europe/Warsaw` (z czasem letnim i zimowym), a porownuje sie godzine lokalna z progiem 22:00 |
| Brak `EndAt` | brak luki (`participantsWithoutReturn = 0`) |
| Inne srodki transportu | nigdy nie licza sie do luki |
| Dane rozkladowe MZK | nie uzywane; to nie jest analiza rozkladow jazdy |
| Skad `EndAt` | Data (#86): `Neo4jPulseDataReader` czyta pole `EndAt` wezla `Event`; do tego czasu jest `null` |

**Alert:** `ReturnGap`, severity `Warning`, dodawany do `alerts` tylko gdy `participantsWithoutReturn > 0`;
komunikat zawiera liczbe osob i godzine, np. `21 osob nie ma dogodnego powrotu po 22:00.` (zgodnie z przykladem w OpenAPI).
W MVP nie ma alertow `HighDemand` i `LowCoverage`, choc kod `enum` je dopuszcza.

**Poza regula MVP (do decyzji, jesli pojawi sie taki przypadek):** wydarzenie konczace sie po polnocy
(godzina lokalna po `00:00`) jest porownywane z progiem 22:00 wg godziny lokalnej, wiec **nie** zostanie uznane za pozne.
Seed nie zawiera takiego wydarzenia (najpozniejszy koniec: 23:15).

**Kontrakt OpenAPI bez zmian:** pola `participantsWithoutReturn`, `alerts` (`ReturnGap`), `returnGap` i `returns` juz istnieja.

## Pliki

```text
Application/Pulse/{PulsePoint,ModalSplit,PulseEventInfo}.cs
Application/Pulse/GetActivityMap/{HexGrid,ActivityMap,GetActivityMapHandler}.cs
Application/Pulse/GetPulseHexagons/GetPulseHexagonsHandler.cs
Application/Pulse/GetEventPulse/GetEventPulseHandler.cs
Application/Pulse/GetPulseSummary/GetPulseSummaryHandler.cs
Application/Abstractions/Persistence/IPulseDataReader.cs
Api/Endpoints/Pulse/{PulseEndpoints,PulseResponses}.cs
```
