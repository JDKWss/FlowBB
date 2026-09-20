# Backend lokalnie

Domyslny stack developerski zawiera Client, Dashboard, API, lokalny Neo4j i
Seq. Nie wymaga pliku `.env`, hasel ani profili:

```bash
cd infra
docker compose up --build -d
docker compose ps
```

Zatrzymanie i pelny reset danych:

```bash
docker compose down
docker compose down -v
```

API jest dostepne na porcie `8080`, Neo4j Browser na `http://localhost:7474`,
a Bolt na porcie `7687`. Przy starcie API automatycznie wykonuje idempotentny
seed demo. Stan API sprawdzisz poleceniem:

- Client: `http://localhost:5173`
- Dashboard: `http://localhost:5174`
- API: `http://localhost:8080`
- Seq: `http://localhost:5341`

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
`http://localhost:5341` i publikowany wylacznie na `127.0.0.1`. Domyslnie Seq
startuje lokalnie bez logowania. Dane Seq sa trzymane w wolumenie `seq-data`.

- **API w Compose** loguje do `http://seq:5341` przez ustawienie `Serilog__WriteTo__1__Args__serverUrl`.
  `localhost` w kontenerze API nie wskazuje kontenera Seq. Indeks `1` odpowiada wpisowi `Seq` w `Serilog:WriteTo`
  w `appsettings.json` (0 = Console); po zmianie kolejnosci tych wpisow trzeba zmienic nazwe zmiennej w `infra/docker-compose.yml`.
- **API uruchomione poza Compose** (`dotnet run`) uzywa adresu z `appsettings.json`, czyli `http://localhost:5341`.
  Wystarczy uruchomic Seq z katalogu `infra` (`docker compose up -d seq`).
- **Brak Seq nie blokuje API.** Usluga `api` nie zalezy od `seq`, a sink Seq wysyla logi asynchronicznie w paczkach,
  wiec niedostepny Seq nie zatrzymuje startu ani zadan. Sink Console dziala zawsze.
- Konwencje logowania (czego nie logujemy): `docs/LOGGING.md`.
