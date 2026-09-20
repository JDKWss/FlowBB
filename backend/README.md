# Konfiguracja Neo4j Aura

Plik ze zmiennymi połączenia należy umieścić w katalogu:

```text
FlowBB/
└── backend/
    └── Neo4j-46e86353-Created-2026-09-19.txt
```

## Uruchomienie lokalne

Skopiuj `.env.example` do `.env` i uzupelnij wartosci. Plik `.env` jest lokalny
i nie moze trafic do repozytorium.

### API z Neo4j Aura

Ustaw w `.env` dane polaczenia otrzymane z Aura, a nastepnie uruchom API razem z Seq:

```powershell
docker compose -f infra/docker-compose.yml --env-file .env up --build api seq
```

Profil `local-db` nie jest wtedy wlaczony, a API laczy sie z adresem podanym w
`NEO4J_URI`. Seq jest dodatkiem: mozesz uruchomic samo API (`up --build api`), wtedy logi trafiaja tylko do konsoli.

### API z lokalnym Neo4j

W `.env` ustaw `NEO4J_URI=neo4j://neo4j:7687`, pozostaw uzytkownika `neo4j`
i ustaw lokalne haslo o dlugosci co najmniej 8 znakow. Uruchom profil bazy:

```powershell
docker compose -f infra/docker-compose.yml --env-file .env --profile local-db up --build
```

API jest dostepne na porcie `8080`, Neo4j Browser na `http://localhost:7474`,
a Bolt na porcie `7687`. Stan API sprawdzisz poleceniem:

```powershell
Invoke-RestMethod http://localhost:8080/health
```

Oczekiwana odpowiedz to `{"status":"Healthy","timestamp":"..."}`.

### Zywotnosc i gotowosc

| Endpoint | Znaczenie | Odpowiedz |
|---|---|---|
| `GET /health` | zywotnosc procesu, nie zalezy od bazy | zawsze `200 {"status":"Healthy",...}` |
| `GET /health/ready` | gotowosc: API laczy sie z Neo4j | `200 {"status":"Healthy",...}` albo `503 {"status":"Unhealthy",...}` |

Odpowiedz gotowosci nie zawiera szczegolow bledu (host, komunikat wyjatku, dane logowania); przyczyna trafia tylko do logu
(ostrzezenie `Neo4j readiness check failed`). Sprawdzenie ma limit 5 s, a sterownik Neo4j limity 5 s na polaczenie, pule i ponowienia,
wiec przy niedostepnej bazie zadanie konczy sie bledem po kilku sekundach (wczesniej po ok. 37 s). Kontener `api` w Compose ma healthcheck
oparty na `/health/ready` (`docker compose ps` pokazuje `healthy` albo `unhealthy`).

```powershell
Invoke-WebRequest http://localhost:8080/health/ready -SkipHttpErrorCheck | Select-Object StatusCode, Content
```

### Logi w Seq (dodatek developerski)

Compose uruchamia Seq (`datalust/seq`, obraz przypiety do konkretnej wersji) razem z API. Interfejs jest dostepny pod
`http://localhost:5341` i publikowany wylacznie na `127.0.0.1`. Jesli port 5341 jest zajety (np. przez lokalnie zainstalowany Seq), ustaw w `.env` `SEQ_HOST_PORT` na inny; wtedy API uruchomione poza Compose musi dostac ten adres w `Serilog__WriteTo__1__Args__serverUrl`. Domyslnie Seq startuje bez logowania (tylko lokalnie);
na wspolnej maszynie ustaw `SEQ_FIRSTRUN_NOAUTHENTICATION=false` w `.env` przy pierwszym uruchomieniu i skonfiguruj haslo w UI.
Dane Seq sa trzymane w wolumenie `seq-data`.

- **API w Compose** loguje do `http://seq:5341` (zmienna `Serilog__WriteTo__1__Args__serverUrl`, nadpisywana przez `SEQ_SERVER_URL`).
  `localhost` w kontenerze API nie wskazuje kontenera Seq. Indeks `1` odpowiada wpisowi `Seq` w `Serilog:WriteTo`
  w `appsettings.json` (0 = Console); po zmianie kolejnosci tych wpisow trzeba zmienic nazwe zmiennej w `infra/docker-compose.yml`.
- **API uruchomione poza Compose** (`dotnet run`) uzywa adresu z `appsettings.json`, czyli `http://localhost:5341`.
  Wystarczy, ze Seq dziala w Compose (`docker compose -f infra/docker-compose.yml --env-file .env up seq`).
- **Brak Seq nie blokuje API.** Usluga `api` nie zalezy od `seq`, a sink Seq wysyla logi asynchronicznie w paczkach,
  wiec niedostepny Seq nie zatrzymuje startu ani zadan. Sink Console dziala zawsze.
- Konwencje logowania (czego nie logujemy): `docs/LOGGING.md`.
