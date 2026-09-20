# Produkcyjne obrazy frontendow

Client i Dashboard sa budowane jako statyczne aplikacje Vite i serwowane przez
Nginx na porcie `8080`. Oba obrazy maja ten sam sposob konfiguracji.

## Budowanie

Domyslny adres API jest build argumentem. Nie jest sekretem i sluzy jako
fallback, gdy kontener nie otrzyma konfiguracji runtime.

```bash
docker build \
  --build-arg VITE_API_URL=http://localhost:8080 \
  -t flowbb-client:local client

docker build \
  --build-arg VITE_API_URL=http://localhost:8080 \
  -t flowbb-dashboard:local dashboard
```

Kontekst builda jest ograniczony do katalogu aplikacji. Lokalne pliki `.env`,
`node_modules` i `dist` sa wykluczone przez `.dockerignore`.

## Konfiguracja przy uruchomieniu

`VITE_API_URL` przekazane do kontenera nadpisuje wartosc z build argumentu.
Skrypt startowy zapisuje ja do `runtime-config.js`, zanim Nginx zacznie
serwowac aplikacje. Pozwala to wdrazac ten sam obraz w roznych srodowiskach bez
ponownego budowania.

```bash
docker run --rm -p 5173:8080 \
  -e VITE_API_URL=https://api.example.test \
  flowbb-client:local

docker run --rm -p 5174:8080 \
  -e VITE_API_URL=https://api.example.test \
  flowbb-dashboard:local
```

Endpoint healthcheck obu obrazow: `GET /healthz`.

## Domyslny Compose

Client i Dashboard sa czescia domyslnego `infra/docker-compose.yml`. Pelny
lokalny stack uruchamia jedna komenda:

```bash
cd infra
docker compose up --build -d
```

Compose przekazuje obu aplikacjom publiczny adres
`http://localhost:8080`. Jest to adres widoczny z przegladarki, a nie nazwa
uslugi w prywatnej sieci Compose. Frontendy czekaja na zdrowe API przed
startem.

## Zachowanie SPA i cache

- Nieznane sciezki wracaja do `index.html`, wiec routing po stronie klienta
  dziala po odswiezeniu strony.
- Haszowane assety Vite maja dlugi cache.
- `index.html` oraz `runtime-config.js` nie sa trwale cache'owane, aby zmiana
  konfiguracji byla widoczna po restarcie kontenera.
