# Seed Neo4j Aura

`flowbb-demo-seed.cypher` zawiera idempotentne dane syntetyczne zgodne z mockiem `client/src/mocks/data.ts`. Seed nie wymaga kontenera ani wolumenu i jest osadzany w assembly Infrastructure podczas buildu.

## Automatyczne uruchomienie

Backend wykonuje constraints i seed przed wystartowaniem serwera, gdy ustawiono:

```text
NEO4J_SEED_ON_STARTUP=true
```

Lokalne profile `dotnet run` (`http` i `https`) mają tę opcję włączoną. W innym środowisku, np. po wdrożeniu backendu, ustaw zmienną samodzielnie. Brak wartości lub `false` pomija inicjalizację, dzięki czemu zwykłe testy i środowiska bez Neo4j nie łączą się z Aurą.

```powershell
dotnet run --project backend/src/FlowBB.Api
```

Startup czeka na zakończenie inicjalizacji. Błąd połączenia albo błąd Cypher zatrzymuje uruchomienie, zamiast wystartować z niepełnymi danymi. Wszystkie zapytania seedujące są wykonywane w jednej transakcji; błąd wycofuje cały seed. Kolejne uruchomienie aktualizuje te same węzły i nie dubluje relacji.

## Dane zgodne z frontendem

- 4 wydarzenia z `client/src/mocks/data.ts` wraz z tymi samymi identyfikatorami, nazwami, terminami, miejscami i współrzędnymi;
- 82 syntetycznych użytkowników, w tym `DEMO_USER_ID` klienta, oraz liczniki uczestników `82`, `46`, `28`, `64` odwzorowane relacjami `IS_GOING_TO`;
- 2 grupy CREW z tymi samymi identyfikatorami, limitami, tagami, punktami spotkania i liczbą członków `4` oraz `6`;
- dodatkowe swobodne tagi, miejsca i syntetyczny organizator.

Każdy `Event` ma kanoniczne pola `Category` i `Source`, mapowane 1:1 na enumy backendu. Węzły `Tag` połączone przez `HAS_TAG` są dodatkowymi zainteresowaniami i nie zastępują tych pól. Każda relacja `IS_GOING_TO` ma snapshot `TransportMode`, `OriginLatitude`, `OriginLongitude` i `UpdatedAt`, z którego backend wylicza agregaty PULSE. Pole `DemoData` nie występuje w modelu.

Użytkownicy mają nazwy `Uzytkownik XXX`, adresy email w zarezerwowanej domenie `.invalid` i placeholder `PasswordHash`, który nie umożliwia logowania. Nie ma prawdziwych danych osobowych.

## Uruchomienie ręczne i kontrola

W Aura Query można nadal wkleić kolejne ponumerowane bloki z pliku. Backend automatycznie wykonuje tylko bloki przed znacznikiem `__FLOWBB_SEED_END__`; zapytania 16-21 są kontrolne.

Oczekiwane wyniki:

- liczba węzłów seedu: `User=82`, `Event=4`, `Venue=4`, `BusinessOwner=1`, `Tag=4`, `Crew=2`;
- uczestnicy wydarzeń: `82`, `46`, `28`, `64`;
- kontrola `HOSTED_AT`: zero wierszy;
- `InvalidCoordinates=0`;
- `InvalidEvents=0` i `InvalidAttendanceSnapshots=0`.

## PULSE

PULSE nie jest osobną bazą ani zapisanym licznikiem. API odczytuje snapshoty relacji `IS_GOING_TO`, a istniejąca logika Application wylicza liczniki, podział środków transportu i komórki mapy. Publiczna mapa zwraca wyłącznie zagregowane komórki z `count >= 10`; identyfikatory i dokładne punkty użytkowników nie opuszczają backendu.
