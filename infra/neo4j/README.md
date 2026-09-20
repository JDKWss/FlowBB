# Neo4j w Docker Compose

Fragment [compose.neo4j.yml](compose.neo4j.yml) jest starszym, samodzielnym
wariantem uruchamiania samego Neo4j. Domyslny stack korzysta bezposrednio z
`infra/docker-compose.yml`, uruchamia Neo4j bez profilu i nie wymaga tych
zmiennych.

## Zmienne (`.env`)

| Zmienna | Wymagana | Uwagi |
|---|---|---|
| `NEO4J_USERNAME` | tak | lokalnie musi byc `neo4j` (kontener odrzuca inna nazwe: `Invalid admin username, it must be neo4j`) |
| `NEO4J_PASSWORD` | tak | brak wartosci przerywa `docker compose` czytelnym bledem `Set NEO4J_PASSWORD in .env` |
| `NEO4J_DATABASE` | dla API | lokalnie `neo4j`: Community ma jedna baze uzytkownika i nie pozwala utworzyc drugiej |
| `NEO4J_URI` | dla API | z kontenera API: `neo4j://neo4j:7687`; z hosta: `neo4j://127.0.0.1:7687` |
| `NEO4J_HTTP_PORT`, `NEO4J_BOLT_PORT` | nie | porty na `127.0.0.1` hosta, domyslnie 7474 i 7687 |

Przyklad jawnej konfiguracji wymaganej tylko przez samodzielny fragment:

```env
# Lokalny kontener Neo4j (Community): uzytkownik i baza to zawsze neo4j.
# NEO4J_URI=neo4j://neo4j:7687
# NEO4J_DATABASE=neo4j
# NEO4J_USERNAME=neo4j
# NEO4J_PASSWORD=replace-with-a-strong-password
# NEO4J_HTTP_PORT=7474
# NEO4J_BOLT_PORT=7687
```

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

## Weryfikacja (Neo4j 5.26.30 Community, Docker Compose 5.5.1)

- `docker compose config` przechodzi przy ustawionych zmiennych; bez `NEO4J_PASSWORD` konczy sie czytelnym bledem.
- Start od pustego srodowiska: kontener osiaga stan `healthy`.
- Po `down` (bez `-v`) i ponownym `up` seed nadal ma 20 wezlow: dane przetrwaly restart.
- Zasoby: Neo4j prealokuje logi transakcji, wiec pusta baza zajmuje okolo 500 MB dysku.
