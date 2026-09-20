# Neo4j w Docker Compose

Fragment [compose.neo4j.yml](compose.neo4j.yml) definiuje lokalna usluge Neo4j: obraz `neo4j:5.26.30-community`, health check, trwaly wolumen `neo4j-data` i konfiguracje wylacznie ze zmiennych `NEO4J_*`. Nie zawiera sekretow. Glowny `infra/docker-compose.yml` nalezy do Core Backend Ownera i nie zostal zmieniony.

## Zmienne (`.env`)

| Zmienna | Wymagana | Uwagi |
|---|---|---|
| `NEO4J_USERNAME` | tak | lokalnie musi byc `neo4j` (kontener odrzuca inna nazwe: `Invalid admin username, it must be neo4j`) |
| `NEO4J_PASSWORD` | tak | brak wartosci przerywa `docker compose` czytelnym bledem `Set NEO4J_PASSWORD in .env` |
| `NEO4J_DATABASE` | dla API | lokalnie `neo4j`: Community ma jedna baze uzytkownika i nie pozwala utworzyc drugiej |
| `NEO4J_URI` | dla API | z kontenera API: `neo4j://neo4j:7687`; z hosta: `neo4j://127.0.0.1:7687` |
| `NEO4J_HTTP_PORT`, `NEO4J_BOLT_PORT` | nie | porty na `127.0.0.1` hosta, domyslnie 7474 i 7687 |

Przyklad dla `.env.example` (do przekazania Core Backendowi, ten plik nie jest zmieniany tutaj):

```env
# Lokalny kontener Neo4j (Community): uzytkownik i baza to zawsze neo4j.
# NEO4J_URI=neo4j://neo4j:7687
# NEO4J_DATABASE=neo4j
# NEO4J_USERNAME=neo4j
# NEO4J_PASSWORD=replace-with-a-strong-password
# NEO4J_HTTP_PORT=7474
# NEO4J_BOLT_PORT=7687
```

## Jak wlaczyc do glownego Compose (Core Backend)

Obie metody sprawdzono poleceniem `docker compose config`.

**A. `extends` z profilem `local-db`** (zachowuje dzisiejsze zachowanie: bez profilu startuje tylko API i laczy sie z Aura). Zastapic w `infra/docker-compose.yml` szkic usluge `neo4j`:

```yaml
  neo4j:
    extends:
      file: neo4j/compose.neo4j.yml
      service: neo4j
    profiles: ["local-db"]
```

`extends` nie przenosi wolumenow, wiec sekcja `volumes: neo4j-data:` zostaje w glownym pliku.

**B. `include`** (Compose 2.20+): usluga startuje zawsze, bez profilu. Na gorze pliku:

```yaml
include:
  - path: neo4j/compose.neo4j.yml
```

W obu wariantach API moze czekac na gotowa baze: `depends_on: { neo4j: { condition: service_healthy } }`. Przy profilu `local-db` (wariant A) `depends_on` bez profilu zepsuje uruchomienie z Aura, wiec wymaga wtedy `required: false`.

## Uruchomienie samodzielne

Z katalogu glownego repozytorium, ze zmiennymi wczytanymi do powloki (`set -a; source .env; set +a`):

```bash
docker compose -f infra/neo4j/compose.neo4j.yml up -d --wait
docker compose -f infra/neo4j/compose.neo4j.yml exec -T neo4j \
  cypher-shell -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" < database/schema.cypher
docker compose -f infra/neo4j/compose.neo4j.yml exec -T neo4j \
  cypher-shell -u "$NEO4J_USERNAME" -p "$NEO4J_PASSWORD" < database/flowbb-queries.cypher
```

Schemat i seed: [../../database/README.md](../../database/README.md). `down` zachowuje dane, `down -v` kasuje wolumen.

## Relacja do Neo4j Aura

`backend/README.md` opisuje dzis uruchomienie API z Aura. Fragment jest alternatywa lokalna, na przyklad do pracy offline i do testow adapterow na prawdziwej bazie. API laczy sie z obiema tak samo (te same zmienne `NEO4J_*`); rozni sie tylko `NEO4J_URI` (`neo4j+s://...` dla Aura) i `NEO4J_DATABASE` (nazwa bazy z Aura albo `neo4j`).

## Weryfikacja (Neo4j 5.26.30 Community, Docker Compose 5.5.1)

- `docker compose config` przechodzi przy ustawionych zmiennych; bez `NEO4J_PASSWORD` konczy sie czytelnym bledem.
- Start od pustego srodowiska: kontener osiaga stan `healthy`.
- Po `down` (bez `-v`) i ponownym `up` seed nadal ma 20 wezlow: dane przetrwaly restart.
- Zasoby: Neo4j prealokuje logi transakcji, wiec pusta baza zajmuje okolo 500 MB dysku.
