# PULSE w czasie rzeczywistym (SignalR): zasady dla frontendu

Dotyczy `/dashboard` (i kazdego klienta, ktory chce widziec licznik `82 -> 83` bez odswiezania). Zrodlem prawdy dla ksztaltu
komunikatu jest `contracts/openapi.yaml` (`x-signalr`, schemat `PulseUpdatedMessage`). Testy: `PulseHubTests`,
`AttendanceSignalRFlowTests`, `PulseReconnectTests`, `PulseHubCorsTests` (backend/tests/FlowBB.Api.IntegrationTests) oraz
`PulseUpdatedSmokeTests` (na prawdziwym stosie, patrz `Smoke/README.md`).

## Polaczenie

| | |
|---|---|
| Hub | `{API}/hubs/pulse` (np. `http://localhost:8080/hubs/pulse`) |
| Zdarzenie serwer -> klient | `PulseUpdated` |
| Metody klient -> serwer | brak (hub jest tylko do nasluchu) |
| Biblioteka | `@microsoft/signalr` (zatwierdzona w stacku) |
| Uwierzytelnianie | brak w MVP |
| CORS | dozwolone originy z `Cors:AllowedOrigins` (domyslnie `http://localhost:5173` i `:5174`), z `AllowCredentials`, wiec domyslne `withCredentials` klienta dziala |

Zmiana adresu lub portu dashboardu wymaga dopisania originu w `.env` (`Cors__AllowedOrigins__N`), inaczej negocjacja zostanie
odrzucona przez przegladarke.

## Komunikat `PulseUpdated`

Zawiera **bezwzgledny** stan wydarzenia po zmianie (nie przyrost) i wylacznie agregaty:

```json
{
  "eventId": "11111111-1111-1111-1111-111111111111",
  "participantsCount": 83,
  "modalSplit": { "publicTransport": 49, "walking": 18, "bike": 6, "car": 7, "unknown": 3 },
  "participantsWithoutReturn": 21,
  "changedAt": "2026-09-19T17:20:00+00:00"
}
```

Nie ma w nim `userId`, wspolrzednych ani danych mapy. Po kazdym komunikacie odswiez heksagony
(`GET /api/pulse/hexagons?eventId=...`), najlepiej z krotkim debounce (ok. 500 ms).

## Co dostajesz, a czego nie

- **Broadcast do wszystkich klientow.** Komunikat dotyczacy dowolnego wydarzenia trafia do kazdego polaczonego klienta
  (grupy per wydarzenie nie sa w kontrakcie). Filtruj po `eventId` wybranego wydarzenia i ignoruj reszte.
- **Brak historii i replayu.** Komunikat opublikowany, gdy klient nie byl polaczony, przepada. Serwer niczego nie ponawia po
  reconnect.
- **Powtorzony `POST attendance` publikuje ten sam stan** (idempotentny zapis, ten sam licznik). Nie animuj `+1`, dopoki
  `participantsCount` sie nie zmieni. Powtorzony `DELETE` niczego nie publikuje.
- **Blad publikacji nie psuje zapisu.** Serwer tylko go loguje (zapis Attendance jest juz zatwierdzony), wiec sporadycznie
  klient moze nie dostac komunikatu mimo aktywnego polaczenia.

## Co robic po reconnect (i przy starcie)

Stan odzyskujesz zawsze przez REST, nie z huba:

1. Zarejestruj `connection.on('PulseUpdated', ...)` **przed** `start()`.
2. Po `start()` i **po kazdym `onreconnected`** pobierz `GET /api/pulse/events/{id}` (oraz heksagony) i nadpisz nim stan.
3. Komunikaty sa bezwzgledne, wiec wygrywa najnowszy: porzuc komunikat, ktorego `changedAt` jest starszy od
   `generatedAt` ostatnio zastosowanego `GET`-a (`GET` juz go uwzglednia). Komunikaty, ktore przyszly w trakcie `GET`-a,
   buforuj i stosuj po nim.
4. Po `onclose` (klient poddal sie po serii prob) pokaz stan "brak polaczenia" i pozwol uzytkownikowi ponowic; dane z ostatniego
   `GET` moga byc nieaktualne.
5. Opcjonalna siatka bezpieczenstwa: odswiezaj `GET` co 30-60 s, gdy dashboard jest otwarty (pokrywa sporadycznie zgubiony
   komunikat).

```ts
import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'

const connection = new HubConnectionBuilder()
  .withUrl(`${API_URL}/hubs/pulse`)
  // Domyslnie klient probuje po 0, 2, 10 i 30 s i rezygnuje; dla dashboardu na pokazie warto probowac dluzej.
  .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 10000, 10000])
  .build()

connection.on('PulseUpdated', (message) => applyIfNewer(message)) // filtr po eventId i changedAt

connection.onreconnected(() => void reloadFromRest())            // GET pulse + hexagons
connection.onclose(() => showOffline())

await connection.start()
await reloadFromRest()
```

Przy zamknieciu widoku wywolaj `connection.stop()`, zeby nie zostawiac polaczen.

## Transport

Klient domyslnie negocjuje WebSockets i wraca do Server-Sent Events / long pollingu. Za proxy dev serwera (Vite) wlacz
przekazywanie WebSocketow (`ws: true`). Backend dziala jako pojedyncza instancja (bez backplane), co wystarcza dla MVP.

## Weryfikacja tego zachowania

- `PulseReconnectTests` symuluje utrate sieci (klient przechodzi w `Reconnecting`, potem `Reconnected`), sprawdza brak replayu
  komunikatow z czasu awarii oraz to, ze `GET /api/pulse/events/{id}` zwraca aktualny stan takze wtedy, gdy hub jest niedostepny.
- Kolejnosc kolejnych komunikatow po ponownym polaczeniu, broadcast do dwoch klientow i rozroznianie wydarzen po `eventId`
  sa w tym samym pliku oraz w `PulseHubTests`.
- `PulseHubCorsTests` sprawdza preflight i negocjacje z originu dashboardu.
