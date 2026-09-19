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

Ustaw w `.env` dane polaczenia otrzymane z Aura, a nastepnie uruchom tylko API:

```powershell
docker compose -f infra/docker-compose.yml --env-file .env up --build api
```

Profil `local-db` nie jest wtedy wlaczony, a API laczy sie z adresem podanym w
`NEO4J_URI`.

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

Oczekiwana odpowiedz to `{"status":"ok"}`.
