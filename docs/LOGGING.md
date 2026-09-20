# Konwencje logowania backendu

Dotyczy `backend/`. Wlasciciel dokumentu: Core Backend Owner. Issue: #48.

## Zasady

- Uzywaj `ILogger<T>` wstrzyknietego przez DI. Nie uzywaj statycznego `Log.*` ani `Console.WriteLine`.
- Zwykle zadania loguje `UseSerilogRequestLogging` (jedno podsumowanie na zadanie). Nie loguj kazdego poprawnego wywolania
  repozytorium ani handlera.
- Loguj zdarzenia, ktore ktos musi zobaczyc: start aplikacji, problemy z polaczeniem Neo4j, bledy adapterow, niepowodzenie
  publikacji SignalR, uzycie zastepczego planera trasy (`DemoRoutePlanner`), nieoczekiwane wyjatki.
- Uzywaj szablonow wiadomosci z nazwanymi wlasciwosciami (`"... {FlowEventId}"`), a nie interpolacji tekstu.

## Czego NIE logujemy

- wspolrzednych uzytkownikow (punkt startu, `OriginLatitude`/`OriginLongitude`, `DefaultOrigin*`),
- hasel, tokenow, `NEO4J_PASSWORD`, nazwy uzytkownika i pelnego adresu polaczenia Neo4j (moze niesc dane logowania),
- pelnych profili uzytkownikow i tresci zadan (body),
- tresci przyszlych wiadomosci i czatow,
- parametrow zapytan Cypher zawierajacych wspolrzedne.

Do logu startowego trafia wylacznie nazwa hosta Neo4j (bez schematu, portu i danych logowania) oraz nazwa bazy.

## Korelacja

`RequestLogContextMiddleware` dodaje do kontekstu kazdego zadania (a wiec do logow z handlerow, adapterow i handlera wyjatkow):

| Wlasciwosc | Zrodlo | Uwagi |
|---|---|---|
| `TraceId` | `Activity.Current.Id` (W3C), a bez aktywnosci `HttpContext.TraceIdentifier` | ta sama wartosc jest w polu `traceId` odpowiedzi `ProblemDetails` |
| `FlowEventId` | parametr trasy `eventId` lub query `eventId` (mapa PULSE) | tylko poprawny, niepusty `Guid` |
| `FlowCrewId` | parametr trasy `groupId` | tylko poprawny, niepusty `Guid` |

`EventId` jest zarezerwowana przez Microsoft.Extensions.Logging, dlatego identyfikator wydarzenia FlowBB nazywa sie `FlowEventId`.
Identyfikator uzytkownika nie jest dodawany do kontekstu.

Ograniczenie: podsumowanie zadania Serilog zawiera `RequestPath`, wiec dla `DELETE /api/events/{id}/attendance/{userId}` identyfikator
uzytkownika jest czescia sciezki w logu. Jesli to bedzie problem prywatnosci, `RequestPath` mozna zastapic szablonem trasy (osobne issue).

## Poziomy

| Poziom | Kiedy |
|---|---|
| Information | start aplikacji (srodowisko, host i baza Neo4j, dozwolone origin CORS), podsumowanie zadania |
| Warning | niepowodzenie publikacji `PulseUpdated` (zapis jest juz zatwierdzony), przejsciowe problemy zewnetrzne |
| Error | nieoczekiwany wyjatek obsluzony przez `GlobalExceptionHandler` (logowany dokladnie raz, z wyjatkiem) |
| Debug | anulowanie zadania przez klienta |

## Decyzja: blad publikacji SignalR

`SignalRPulseNotifier` jest wywolywany po zatwierdzeniu zapisu Attendance. Niepowodzenie publikacji jest **logowane (Warning, `FlowEventId`,
`TraceId`) i nie przerywa zadania**: zapis jest idempotentny i zatwierdzony, wiec klient nie powinien dostac bledu za operacje, ktora sie
udala. Klient odzyskuje stan przez `GET /api/pulse/events/{id}`. Anulowanie (`OperationCanceledException`) nie jest polykane.

## Konfiguracja

- Sinki i poziomy: sekcja `Serilog` w `appsettings*.json` (Console zawsze dostepny; Seq jest dodatkiem developerskim, issue "Add Seq to Docker Compose").
- Host uzywa `UseSerilog(..., preserveStaticLogger: true)` i `UseSerilogRequestLogging(options => options.Logger = ...)`: logowanie nie zalezy
  od globalnego `Log.Logger`, wiec kilka hostow w jednym procesie (testy) nie miesza swoich logow.
- Adaptery Neo4j loguja przez wlasne `ILogger<T>` (osobni wlasciciele, zadania #15-#18); ten dokument okresla, co wolno w nich logowac.

## Testowanie logow

Host w `WebApplicationFactory<Program>` czyta `ILogEventSink` z DI (`ReadFrom.Services`), wiec test rejestruje wlasny sink
(`CapturingSink` w `Api.IntegrationTests/Infrastructure`) i sprawdza zdarzenia. Dla klas z wstrzyknietym `ILogger<T>` sluzy `ListLogger<T>`.
