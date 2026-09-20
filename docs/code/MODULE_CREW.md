# Modul Crew

Mikrogrupy zwiazane z wydarzeniem. Wlasciciel: Core Backend do czasu wskazania innego.
Issues #3, #12.

**Status: kod istnieje wylacznie na lokalnych branchach `feature/crew-domain-integrate`
i `feature/crew-application-api`. Nie ma go na `origin/develop`.** Brak adaptera Neo4j (#18).

W API modul nazywa sie "groups", a w kodzie "Crew" - to swiadoma roznica, bo `contracts/openapi.yaml`
uzywa `GroupSummary` i sciezek `/groups`. Nie ujednolicaj jednej strony bez drugiej.

## Endpointy

| Metoda | Sciezka | Odpowiedzi |
|---|---|---|
| `GET` | `/api/events/{eventId}/groups?userId=` | 200 lista `GroupSummary`, 400, 404 |
| `POST` | `/api/groups/{groupId}/members` | 200 `GroupSummary`, 400, 404, 409 |
| `DELETE` | `/api/groups/{groupId}/members/{userId}` | 204, 400 |

`userId` w `GET` jest opcjonalny i sluzy wylacznie do wyliczenia flagi `joinedByCurrentUser`.
Bez niego lista jest ta sama, tylko flaga jest `false`.

## Mapowanie wynikow dolaczenia

`JoinCrewOutcome` w Application ma szesc wartosci i kazda ma inny kod HTTP:

| Wynik | HTTP | Kiedy |
|---|---|---|
| `Joined` | 200 | dopisano czlonka |
| `AlreadyMember` | 200 | juz byl czlonkiem - **to samo 200, bo operacja jest idempotentna** |
| `Full` | 409 | grupa osiagnela `MaxMembers` |
| `InAnotherCrew` | 409 | uzytkownik nalezy juz do innej grupy tego wydarzenia |
| `CrewNotFound` | 404 | nie ma takiej grupy |
| `UserNotFound` | 404 | nie ma takiego uzytkownika |

`Joined` i `AlreadyMember` celowo daja ten sam kod i to samo cialo odpowiedzi - klient nie
musi ich rozrozniac, a demo wymaga, by drugie klikniecie "Dolacz" nie zmienilo licznika.

## Gdzie naprawde dzieje sie dolaczanie

To jest najwazniejsza rzecz w tym module.

`Domain/Crews/Crew.cs` ma metody `Join`, `Leave` i `HasMember` operujace na liscie w pamieci.
**Sciezka zapisu ich nie uzywa.** `JoinCrewHandler` wola bezposrednio
`ICrewRepository.TryJoinAsync`, bo sprawdzenie limitu i zapis musza byc jedna operacja atomowa.
Wczytanie agregatu, wywolanie `Crew.Join` i zapisanie z powrotem daloby wyscig, w ktorym dwie
osoby wchodza jako ostatnie do grupy na 8 miejsc.

`Crew` jest wiec dzis modelem regul i walidacji (oraz przedmiotem testow jednostkowych),
a nie agregatem zapisu. Warto o tym pamietac, zanim ktos "uporzadkuje" handler tak,
by uzywal metody domenowej.

## Reguly domenowe

| Regula | Wartosc |
|---|---|
| Nazwa | wymagana, do 100 znakow, przycinana |
| Opis | opcjonalny, do 500 znakow, `null` staje sie pustym tekstem |
| `MaxMembers` | 2-12 |
| Tagi | do 10, kazdy do 40 znakow, przycinane, duplikaty odrzucane bez wzgledu na wielkosc liter |
| `MeetingPoint` | nazwa do 120 znakow + wspolrzedne w poprawnych zakresach |

`Crew.Leave` zwraca `bool` i nie jest bledem przy braku czlonkostwa - stad 204 z endpointu
niezaleznie od stanu wyjsciowego.

## Prywatnosc

`CrewSummary` **nie zawiera listy czlonkow**. Domenowy `Crew` ma `Members` jako
`IReadOnlyList<Guid>`, ale ta lista nie przechodzi przez warstwe Application do DTO.
Na zewnatrz ida tylko `currentMembers`, `maxMembers` i `joinedByCurrentUser`.

## Pliki

```text
Domain/Crews/{Crew,MeetingPoint,JoinCrewResult}.cs
Application/Crews/{CrewSummary,CrewJoinResult}.cs
Application/Crews/GetEventGroups/GetEventGroupsHandler.cs
Application/Crews/JoinCrew/JoinCrewHandler.cs
Application/Crews/LeaveCrew/LeaveCrewHandler.cs
Application/Abstractions/Persistence/ICrewRepository.cs
Api/Endpoints/Crews/{CrewsEndpoints,CrewsResponses}.cs
```
