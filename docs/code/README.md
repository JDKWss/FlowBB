# Dokumentacja kodu backendu FlowBB

Opis tego, co **faktycznie jest w kodzie**, odczytany z zrodel, a nie z planow.
Uzupelnia dokumenty kierunkowe: `AGENTS.md` (zasady), [MVP_WORK_PLAN.md](../MVP_WORK_PLAN.md) (plan),
[NEO4J_CONTRACT.md](../NEO4J_CONTRACT.md) (dane), [DEMO_RUNBOOK.md](../DEMO_RUNBOOK.md) (uruchomienie),
`contracts/openapi.yaml` (kontrakt HTTP, zrodlo prawdy).

**Gdy ten opis rozjedzie sie z kodem, prawde ma kod; gdy rozjedzie sie z kontraktem, prawde ma kontrakt.**

## Stan odniesienia

Zrodlo: `origin/develop` na commicie `a77ae09`, stan na 2026-09-20.
Moduly Events, Crew i Routing opisane sa z lokalnych branchy, ktorych **nie ma jeszcze na `origin`**
(patrz kolumna "Gdzie jest kod").

## Spis

| Dokument | Zawartosc |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | warstwy, projekty, kierunek zaleznosci, stan podpiecia w `Program.cs`, komendy build/test |
| [PERSISTENCE_PORTS.md](PERSISTENCE_PORTS.md) | komplet portow, ktore musi zaimplementowac adapter Neo4j - lista zadan dla Data/Neo4j |
| [MODULE_ATTENDANCE.md](MODULE_ATTENDANCE.md) | deklaracja "Ide", idempotencja, publikacja `PulseUpdated` |
| [MODULE_PULSE.md](MODULE_PULSE.md) | agregacja, siatka heksagonalna, reguly prywatnosci |
| [MODULE_EVENTS.md](MODULE_EVENTS.md) | lista i szczegoly wydarzen, `IEventLookup` |
| [MODULE_CREW.md](MODULE_CREW.md) | mikrogrupy, dolaczanie i opuszczanie |
| [MODULE_ROUTING.md](MODULE_ROUTING.md) | `IRoutePlanner` i deterministyczny `DemoRoutePlanner` |
| [TESTS.md](TESTS.md) | mapa testow i zmierzone wyniki uruchomien |

## Legenda statusow

| Status | Znaczenie |
|---|---|
| **Na develop** | kod jest na `origin/develop` |
| **Na branchu** | kod istnieje, ale tylko na lokalnym branchu; nie ma go na `origin` |
| **Podpiete** | endpoint jest zarejestrowany w `Program.cs` i odpowiada po uruchomieniu API |
| **Brak adaptera** | logika jest gotowa, ale nie ma implementacji portu persystencji, wiec nie da sie tego uruchomic z prawdziwymi danymi |

Dzis **kazdy** modul poza `/health` i hubem SignalR ma status "Brak adaptera", a Events, Crew
i Routing dodatkowo "Na branchu". Nic z warstwy HTTP nie jest jeszcze podpiete - szczegoly
w [ARCHITECTURE.md](ARCHITECTURE.md#stan-podpiecia).
