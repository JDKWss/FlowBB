# Repozytorium Neo4j — metody dla Service / API

Interfejs: `IFlowBbGraphRepository` w Domain. Implementacja: `Neo4jFlowBbGraphRepository` w Infrastructure.
Nie dodano endpointów ani zmian w Application/API. Serwis może korzystać z poniższych metod po podłączeniu repozytorium.

## Rekomendacje i odczyty

| Metoda | Wynik i reguły |
|---|---|
| `GetRecommendedEventsAsync(userId, limit = 5)` | `EventRecommendation`: wydarzenie i `FriendsGoingCount`. Tylko przyszłe wydarzenia, na które idzie przynajmniej jeden znajomy; bez wydarzeń, na które użytkownik już idzie. |
| `GetRecommendedCrewsAsync(userId, limit = 5)` | `CrewRecommendation`: CREW, `EventId`, `FriendsCount`, `MembersCount`. Tylko przyszłe wydarzenia, grupy ze znajomymi, wolnym miejscem i bez obecnego użytkownika. |
| `GetPeopleYouMayKnowAsync(userId, limit = 10)` | `PersonRecommendation`: `UserId`, `Name`, `MutualFriendsCount`. Znajomi znajomych, bez siebie i bez bezpośrednich znajomych. Nie zwraca haseł, emaili ani lokalizacji. |
| `GetEventTagsAsync(eventId)` | Lista unikalnych tagów wydarzenia, posortowana po nazwie i identyfikatorze. |
| `GetManagedVenuesAsync(userId)` | Unikalne lokale zarządzane przez organizacje, do których należy użytkownik. |

Rankingi liczą **unikalnych znajomych**, również gdy `FRIENDS_WITH` zapisano w obu kierunkach. Kolejność: liczba znajomych malejąco, następnie data rozpoczęcia (wydarzenia/CREW) lub nazwa (osoby), następnie identyfikator. Parametr `limit` musi być w zakresie 1–100. Nieznany użytkownik lub brak pasujących danych daje pustą listę, bez losowego uzupełniania wyników.

„Przyszłe” oznacza `StartAt >= teraz (UTC)`; trwające wydarzenia nie są uwzględniane. Ranking CREW nie rezerwuje miejsca — serwis dołączający do grupy musi osobno sprawdzić pojemność przy zapisie.

```csharp
var events = await repository.GetRecommendedEventsAsync(userId);
var crews = await repository.GetRecommendedCrewsAsync(userId);
var people = await repository.GetPeopleYouMayKnowAsync(userId);
var tags = await repository.GetEventTagsAsync(eventId);
```

## Domyślny punkt startowy

`SetDefaultOriginAsync(userId, latitude, longitude)` aktualizuje tylko `DefaultOriginLatitude` i `DefaultOriginLongitude`. Zwraca `true` po aktualizacji, `false` dla nieistniejącego użytkownika. Nie tworzy użytkownika.

Waliduje szerokość `[-90, 90]` i długość `[-180, 180]`, odrzuca `NaN` i nieskończoności przez `ArgumentOutOfRangeException`. Są to prywatne dane: API powinno umożliwić zmianę właścicielowi konta i nie udostępniać ich publicznie.

## Hasło i weryfikacja logowania

- `SetUserPasswordAsync(userId, password)` zapisuje hash, nigdy czyste hasło. Zwraca `false`, gdy użytkownik nie istnieje. Przy ustawianiu wymaga 12–1024 znaków.
- `VerifyLoginAsync(email, password)` zwraca `bool`. Loginem jest email; ignoruje wielkość liter i zewnętrzne spacje emaila. Hasła nie przycina ani nie zmienia jego wielkości liter.
- Używany jest standardowy `PasswordHasher` ASP.NET Core Identity (PBKDF2, losowa sól). Infrastructure korzysta z frameworka `Microsoft.AspNetCore.App`; nie dodano pakietu NuGet. Starszy obsługiwany hash wymagający aktualizacji jest wymieniany po poprawnym logowaniu.
- Nieistniejący użytkownik, złe hasło, syntetyczny placeholder lub nieobsługiwany hash zwracają `false`. Stare hashe z innego algorytmu wymagają osobnej migracji lub resetu — metoda nie próbuje zgadywać formatu i nie porównuje haseł jawnych.
- Jeśli istnieją dwa konta o emailach różniących się tylko wielkością liter/spacjami, logowanie zwraca `false`. Istniejący constraint `User.Email` rozróżnia wielkość liter; rejestracja musi ustalić i stosować jedną normalizację emaila.
- Błąd połączenia z bazą jest wyjątkiem, a nie wynikiem `false`.

**To nie jest pełny system autoryzacji.** Tokeny, ograniczanie prób logowania, reset hasła i sprawdzanie tożsamości wywołującego należą do Service/API. `SetUserPasswordAsync` jest operacją zaufaną — nie wystawiaj jej jako niezabezpieczonego endpointu. `GetUserAsync` zwraca model wewnętrzny z hashem i współrzędnymi: mapuj go na bezpieczny DTO zamiast serializować bezpośrednio.

```csharp
// Po autoryzacji zmiany hasła / potwierdzeniu resetu:
bool changed = await repository.SetUserPasswordAsync(userId, newPassword);
bool valid = await repository.VerifyLoginAsync(email, password);
```

## Organizacje i publikowanie wydarzeń

Nowe relacje:

```text
(User)-[:IS_ORGANIZATION_MEMBER]->(BusinessOwner)
(BusinessOwner)-[:CREATED_EVENT]->(Event)-[:HOSTED_AT]->(Venue)
(BusinessOwner)-[:MANAGES]->(Venue)
```

`BusinessOwner` reprezentuje organizację; konto logowania to `User` powiązany z organizacją. Email firmy nie tworzy automatycznie uprawnień.

| Metoda | Zachowanie |
|---|---|
| `AddOrganizationMemberAsync(userId, ownerId)` | Dodaje członkostwo przez `MERGE`. Ponowienie nie dubluje relacji; brak użytkownika/organizacji oznacza `InvalidOperationException`. |
| `RemoveOrganizationMemberAsync(userId, ownerId)` | Usuwa członkostwo; brak relacji nie jest błędem. |
| `IsOrganizationMemberAsync(userId, ownerId)` | Sprawdza członkostwo, zwraca `bool`. |
| `CreateEventForBusinessOwnerAsync(actorUserId, ownerId, venueId, event)` | W jednym zapytaniu sprawdza członkostwo i zarządzanie lokalem, tworzy wydarzenie oraz relacje autora i miejsca. |

Publikacja zwraca `false`, jeśli użytkownik nie należy do organizacji lub organizacja nie zarządza wskazanym lokalem (także gdy któryś węzeł nie istnieje). W takim przypadku nie zapisuje wydarzenia. `IsVerified` nie jest wymagane — oznacza odznakę zaufania, a nie prawo publikowania.

Wydarzenie musi mieć niepusty `EventId` i nazwę, a podane `EndAt` nie może być wcześniejsze od `StartAt`. `EndAt` może być `null`. Metoda **tworzy**, nie aktualizuje: istniejący `EventId` powoduje wyjątek constraint Neo4j i wycofanie zapisu. Przed użyciem repozytorium inicjalizacja backendu powinna wywołać `EnsureSchemaAsync()`, żeby constraints identyfikatorów były obecne. Tagi można przypisać istniejącą metodą `TagEventAsync`; nie są częścią transakcji publikacji.

```csharp
// actorUserId musi pochodzić z uwierzytelnionego kontekstu, nie z dowolnego pola requestu.
bool created = await repository.CreateEventForBusinessOwnerAsync(
    actorUserId, ownerId, venueId, newEvent);
```

**Ważna granica bezpieczeństwa:** nadawanie/odbieranie członkostwa oraz `AssignVenueManagerAsync` wymagają autoryzacji w zaufanym serwisie administracyjnym. Nie wolno pozwolić użytkownikowi samodzielnie przypisać się do dowolnej firmy. W obecnym uproszczonym modelu każdy członek organizacji może publikować w jej lokalach. Nie wprowadzono dodatkowych ról.

Istniejące `UpsertEventAsync`, `HostEventAtVenueAsync`, `TagEventAsync` i inne niskopoziomowe zapisy nie sprawdzają aktora — służą zaufanym serwisom/importerom. Endpoint publikacji powinien używać nowej metody, a edycja/tagowanie przez API wymaga osobnego sprawdzenia uprawnień.

## Weryfikacja

Standardowo `dotnet test backend/FlowBB.sln` pomija testy wymagające prawdziwej bazy. Aby świadomie uruchomić je na instancji wskazanej przez istniejącą konfigurację Infrastructure:

```powershell
$env:FLOWBB_NEO4J_TESTS = "1"
dotnet test backend/tests/FlowBB.Api.IntegrationTests --filter FullyQualifiedName~GraphRepositoryTests
Remove-Item Env:FLOWBB_NEO4J_TESTS
```

Testy zapisują syntetyczne węzły z nowymi losowymi identyfikatorami i usuwają wyłącznie te węzły w `finally`/`DisposeAsync`. Test publikacji również zapewnia istniejące constraints przez `EnsureSchemaAsync`. Używaj bazy testowej; przerwanie procesu lub awaria sieci podczas sprzątania może pozostawić dane testowe. Testy nie wdrażają endpointów ani nie zmieniają istniejących użytkowników.
